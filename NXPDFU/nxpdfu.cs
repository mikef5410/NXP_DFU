//
//
// NXP Cortex-M microcontroller firmware update with USB-DFU
//
// Copyright (c) 2014 Tektronix, Inc
//
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.IO;
using System.Threading;
using System.Security.Cryptography;
using LibUsbDotNet;
using LibUsbDotNet.Main;
using LibUsbDotNet.Info;
using FirmwareUpdater.DFU;
using Mono; //DataConverter

namespace FirmwareUpdater.NXPDFU
{
  public class NXPDFUMachine : DFUMachine
  {
    public bool verbose = false;

    public NXPDFUMachine(UsbDevice thisDevice)  :base(thisDevice)
    {
      
    }

    private void DEBUG(String s)
    {
      if (verbose)
        {
          Console.WriteLine(s);
        }
    }
    
    public bool set_debug(uint addr, uint size)
    {
      DEBUG("set_debug");
      issue_command(DFUCommands.HOSTCMD_SETDEBUG, addr, size);
      wait_nxpidle();
      return(true);
    }

    public bool erase_all()
    {
      DEBUG("erase_all");
      issue_command(DFUCommands.HOSTCMD_ERASE_ALL, 0, 0);
      //While erasing, status will return OPSTS_ERASE
      wait_nxpidle();
      return(true);
    }

    public bool erase_region(uint addr, uint size)
    {
      DEBUG("erase_region");
      issue_command(DFUCommands.HOSTCMD_ERASE_REGION, addr, size);
      //While erasing, status will return OPSTS_ERASE
      wait_nxpidle();     
      return(true);
    }

    //For programming a region less than our buffer size use this
    public bool program_region(ref byte[] buffer, uint addr, uint size)
    {
      DEBUG("program_region");
      if (size > buffer.Length) throw new ArgumentException("program region called with size bigger than buffer!");
      if (size > wTransferSize) throw new ArgumentException("size greater than wTransferSize!");

      issue_command(DFUCommands.HOSTCMD_PROGRAM, addr, size);
      wait_nxpProgStream();
      download(ref buffer, (int)size);
      wait_idle();
      download(ref buffer, 0); //signal completion
      wait_idle();
      //If we had a stream o'bytes ... we'd waitProgStream, then download another block, and another 0, and loop.
      //when all done we wait for nxp idle
      wait_nxpidle();
      return(true);
    }

    public bool program_region(Stream s, uint addr)
    {
      DEBUG("program_region");
      //First we need to get the size of this stream (file)...
      s.Seek(0,SeekOrigin.Begin);
      uint size=(uint)s.Length;
      int actual=0;
      bool loop=true;
      int total=0;
      
      issue_command(DFUCommands.HOSTCMD_PROGRAM, addr, size); 

      do {
        actual=getBytes(s, ref iobuf, 0, wTransferSize);
        if (!wait_nxpProgStream()) break;
        download(ref iobuf, (int)actual);
        wait_idle();
        download(ref iobuf, 0);
        wait_idle();
        total += actual;
        if (actual==0 || total>=size) loop=false;
      } while(loop);
      wait_nxpidle();

      return(true);
    }

    public byte[] get_sha(Stream s)
    {
      SHA1 mysha = SHA1.Create();
      byte[] hashValue;
      s.Seek(0,SeekOrigin.Begin);
      hashValue=mysha.ComputeHash(s);
      return(hashValue);
    }

    public byte[] get_sha(ref byte[] b)
    {
      SHA1 mysha = SHA1.Create();
      byte[] hashValue;
      hashValue=mysha.ComputeHash(b);
      return(hashValue);
    }

    // Print the byte array in a readable format. 
    public static void PrintByteArray(byte[] array)
    {
      int i;
      for (i = 0; i < array.Length; i++)
        {
          Console.Write(String.Format("{0:X2}", array[i]));
          if ((i % 4) == 3) Console.Write(" ");
        }
      Console.WriteLine();
    }


    private static int getBytes(Stream s, ref byte[] buffer, int offset, int length)
    {
      if ((offset + length) > buffer.Length) throw new ArgumentException("Buffer too small");
      int bytesReadSoFar=0;
      while (bytesReadSoFar < length)
        {
          int bytes=s.Read(buffer, offset+bytesReadSoFar, length-bytesReadSoFar);
          if (bytes==0) break;
          bytesReadSoFar += bytes;
        }
      return(bytesReadSoFar);
    }
    
    public bool read_region(ref byte[] buffer, uint addr, uint size, out int actual)
    {
      int pos=0;
      bool rval;
      
      DEBUG("read_region");
      if (size > buffer.Length) Array.Resize<byte>(ref buffer, (int)size + 256);
      issue_command(DFUCommands.HOSTCMD_READBACK, addr, size);
      if (!wait_nxpReadTrig()) throw new Exception("READBACK command transitioned to OPSTS_IDLE!");
      if (size <= wTransferSize)
        {
          rval=transfer_in((byte)DFU.DFUCommands.DFU_UPLOAD, 0, ref buffer, (int)size, out actual);
          return(true);
        }
      else
        {
          byte[] mybuf = new byte[wTransferSize];
          do {
            rval=transfer_in((byte)DFU.DFUCommands.DFU_UPLOAD, 0, ref mybuf, (int)wTransferSize, out actual);
            Array.Copy(mybuf,0,buffer,pos,actual);
            pos += actual;
            if (!wait_nxpReadTrig()) break;
          } while (pos<=size);
        }
      actual=pos;
      return(true);
    }

    public bool verify_read(Stream s, uint addr)
    {
      uint size = (uint)s.Length;
      int actual;
      int k;
      
      //First get the SHA1 of our Stream ....
      byte[] fileHash=get_sha(s);
      // Now read back the flash contents and compare ...
      byte[] bits = new byte[size];
      read_region(ref bits, addr, size, out actual);
      if (actual != size) throw new Exception("Readback didn't return the number of bytes we expected! ("
                                              + actual.ToString() + " != " + size.ToString() + ")");
      byte[] flashHash=get_sha(ref bits);
      
     // Now compare ... SHA1 is 20 bytes
      for (k=0; k<20; k++)
        {
          if (fileHash[k] != flashHash[k]) return(false);
        }
      return(true);
    }
    

    public bool reset()
    {
      DEBUG("reset");
      issue_command(DFUCommands.HOSTCMD_RESET,0,0);
      return(true);
    }

    public bool execute(uint addr)
    {
      DEBUG("execute");
      issue_command(DFUCommands.HOSTCMD_EXECUTE, addr, 0);
      return(true);
    }

    /// <summary>
    ///   Get the current status of the NXP DFU engine
    /// </summary>
    //We get the status by doing a DFU_UPLOAD IN transaction, wValue=0, wIndex=0, wLength=0x100
    //16 bytes come back (4 ints, little-endian)
    public ULPktHdr status()
    {
      ULPktHdr h = new ULPktHdr();
      int actual;
      byte[] buf = new byte[256];

      bool rval=transfer_in((byte)DFU.DFUCommands.DFU_UPLOAD, 0, ref buf, 256, out actual);
      if (actual < 16) throw new Exception("NXPDFU status returned the wrong size packet! (" + actual.ToString() +" bytes, should be at least 16 )");
      h.cmdResponse=(DFUCommands) Mono.DataConverter.UInt32FromLE(buf, 0);
      h.progStatus=(DFUStatusVals) Mono.DataConverter.UInt32FromLE(buf,4);
      h.strBytes=Mono.DataConverter.UInt32FromLE(buf,8);
      h.reserved=Mono.DataConverter.UInt32FromLE(buf,12);
      if (actual > 16)
        {
          Array.Copy(buf,16,h.str,0,actual-16);
        }
      return(h);
    }

    public bool issue_command(DFUCommands cmd, uint addr, uint size)
    {
      byte[] buf = new byte[16];
      Array.Copy(Mono.DataConverter.GetBytesLE((uint)cmd),0,buf,0,4);
      Array.Copy(Mono.DataConverter.GetBytesLE(addr),0,buf,4,4);
      Array.Copy(Mono.DataConverter.GetBytesLE(size),0,buf,8,4);
      Array.Copy(Mono.DataConverter.GetBytesLE(Constants.DFUPROG_VALIDVAL),0,buf,12,4);
      
      transaction=0;
      download(ref buf, 16);
      wait_idle();
      Array.Clear(buf,0,16);
      download(ref buf, 0);
      wait_idle();
      return(true);
    }

    public bool wait_nxpidle()
    {
      ULPktHdr h;
      bool loop = true;
      
      DEBUG("wait_nxpidle");
      while (loop) {
        h=status();
        if (h.progStatus == DFUStatusVals.OPSTS_ERRER  ||
            h.progStatus == DFUStatusVals.OPSTS_PROGER ||
            h.progStatus == DFUStatusVals.OPSTS_READER ||
            h.progStatus == DFUStatusVals.OPSTS_ERRUN)  throw new Exception("Error waiting for Idle state");

        if (h.progStatus == DFUStatusVals.OPSTS_IDLE)
          {
            loop=false;
            break;
          } 
        Thread.Sleep(10);
      } 
      return(true);
    }

    /// <summary>
    ///   Wait for a PROG_STREAM and return true. If return IDLE, return false
    /// </summary>
    public bool wait_nxpProgStream()
    {
     ULPktHdr h;
     bool loop = true;
     
     DEBUG("wait_nxpProgStream");
     while (loop) {
       h=status();
       if (h.progStatus == DFUStatusVals.OPSTS_ERRER  ||
           h.progStatus == DFUStatusVals.OPSTS_PROGER ||
           h.progStatus == DFUStatusVals.OPSTS_READER ||
           h.progStatus == DFUStatusVals.OPSTS_ERRUN)  throw new Exception("Error waiting for ProgStream state");
       
       if (h.progStatus == DFUStatusVals.OPSTS_IDLE) return(false);

       if (h.progStatus == DFUStatusVals.OPSTS_PROG_STREAM)
         {
           loop=false;
           break;
         }
       Thread.Sleep(10);
     }
     return(true);      
    }

    /// <summary>
    ///   Wait for a Read Trigger, and return true. If return IDLE, return false
    /// </summary>
    public bool wait_nxpReadTrig()
    {
      ULPktHdr h;
      bool loop = true;
      
      DEBUG("wait_nxpReadTrig");
      while (loop) {
        h=status();
        if (h.progStatus == DFUStatusVals.OPSTS_ERRER  ||
            h.progStatus == DFUStatusVals.OPSTS_PROGER ||
            h.progStatus == DFUStatusVals.OPSTS_READER ||
            h.progStatus == DFUStatusVals.OPSTS_ERRUN)  throw new Exception("Error waiting for ReadTrig state");

        if (h.progStatus == DFUStatusVals.OPSTS_IDLE) return(false);

        if (h.progStatus == DFUStatusVals.OPSTS_READTRIG)
          {
            loop=false;
            break;
          }
        Thread.Sleep(10);
      }
      return(true);     
    }
    
  } //class
} //namespace
