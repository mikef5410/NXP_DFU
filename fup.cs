//
// fup.cs
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
using System.Collections.Generic;
using NDesk.Options;
using LibUsbDotNet;
using LibUsbDotNet.Main;
using LibUsbDotNet.Info;
using FirmwareUpdater.DFU;
using FirmwareUpdater.NXPDFU;

namespace FirmwareUpdater
{
  public static class Program
  {
    public static UsbDevice ourDevice;
    public static DFUMachine dfu;
    public static NXPDFUMachine nxpdfu;
    public static bool chatty = false;
    public static bool debug = false;
    
    public static void Main(string[] args)
    {
      bool show_help = false;
      String secloader = "";
      String binfile = "";
      int exitCode=1;
      
      var p = new OptionSet () 
        {
          { "v|verbose", "tell me what's happening during program.", v => chatty = (v!=null) },
          { "d|debug", "more debugging output.", v => debug = (v!=null) },
          { "s|secondary_loader=", "filename of secondary loader", v => secloader=v },
          { "b|binfile=", "filename of bin flash image", v => binfile=v },
          { "h|help", "show this message and exit", v => show_help = (v!=null) },
        };

      List<string> extra;
      try {
        extra = p.Parse (args);
      }
      catch (OptionException e) {
        Console.Write ("fup: ");
        Console.WriteLine (e.Message);
        Console.WriteLine ("Try `fup --help' for more information.");
        return;
      }

      if (secloader.Length == 0  || binfile.Length == 0) show_help=true;
      

      if (show_help) {
        ShowHelp (p);
        return;
      }
      
      if (debug) chatty=true;
   
      ourDevice=findOurDevice();
      dfu = new DFUMachine(ourDevice);
      
      if (dfu.findInterface())
        {
          verboseOut("Found DFU interface");
          dfu.claimIface();
          DEBUG("wTransferSize = "+dfu.wTransferSize.ToString());
        }
      //Hack
      if (dfu.wTransferSize == 0) dfu.wTransferSize=2048;
      phaseOne(secloader, dfu);

      verboseOut("Wait 5 sec for next phase ...");
      Thread.Sleep(5000);
      if (ourDevice.IsOpen) ourDevice.Close();
      ourDevice=findOurDevice();
      nxpdfu = new NXPDFUMachine(ourDevice);
      if (nxpdfu.findInterface())
        {
          verboseOut("Found DFU interface");
          nxpdfu.claimIface();
          DEBUG("wTransferSize = "+nxpdfu.wTransferSize.ToString());
        }
      //Hack
      if (nxpdfu.wTransferSize == 0) nxpdfu.wTransferSize=2048;
      if ( phaseTwo(binfile, nxpdfu) ) exitCode=0;

      if (ourDevice.IsOpen) ourDevice.Close();
      UsbDevice.Exit();
      Environment.Exit(exitCode);   
    } //Main

    
    static void ShowHelp (OptionSet p)
    {
      Console.WriteLine ("Usage: fup -s secloader -b bin [OPTIONS]+");
      Console.WriteLine ("Do a USB-DFU firmware update of an NXP Cortex-M processor");
      Console.WriteLine ();
      Console.WriteLine ("Options:");
      p.WriteOptionDescriptions (Console.Out);
    }

   
    public static UsbDevice findOurDevice()
    {
      UsbDeviceFinder ourFinder = new UsbDeviceFinder(0x1fc9,0x000c);
      UsbDevice d;
      UsbRegDeviceList allDevices = UsbDevice.AllDevices.FindAll(ourFinder) ;

      if (allDevices.Count > 1)
        {
          verboseOut("I see too many devices. Make sure only ONE is connected to this machine!");
          UsbDevice.Exit();
          Environment.Exit(1);
        }
      if (allDevices.Count == 0)
        {
          verboseOut("No device is connected to this machine!");
          UsbDevice.Exit();
          Environment.Exit(1);
        }
      allDevices[0].Open(out d);
      return(d);
    }
    
    public static bool phaseOne( String filename, DFUMachine dfu )
    {
      bool done=false;
      int transferSize = (dfu.wTransferSize < dfu.iobuf.Length) ? dfu.wTransferSize : dfu.iobuf.Length;
      int totalbytes=0;
      FileStream secloader = File.Open(filename, FileMode.Open);
      DFU_Status status;

      secloader.Seek(0,SeekOrigin.Begin);
      dfu.make_idle(false);
      dfu.transaction=0;
      while (!done)
        {
          int nbytes = getBytes(secloader, ref dfu.iobuf, 0, transferSize);
          totalbytes += nbytes;
          DEBUG("Got " + nbytes.ToString() + "bytes, total:" + totalbytes.ToString());
          if (nbytes>0)
            {
              dfu.download(ref dfu.iobuf, nbytes);
              dfu.wait_idle();
            }
          else
            {
              done=true;      
            }
        }
      dfu.download(ref dfu.iobuf, 0); //Signal we're done.
      do {
        dfu.get_status(out status);
      } while (status.State == DFUStateVals.STATE_DFU_MANIFEST_SYNC);
      return(true);
    }

    public static bool phaseTwo( String filename, NXPDFUMachine nxpdfu )
    {
      bool done=false;
      int transferSize = (dfu.wTransferSize < dfu.iobuf.Length) ? dfu.wTransferSize : dfu.iobuf.Length;
      int totalbytes=0;
      FileStream bin = File.Open(filename, FileMode.Open); 
      int actual;

      nxpdfu.set_debug(0,0);
      verboseOut("Erase...");
      nxpdfu.erase_all();
      verboseOut("Program...");
      nxpdfu.program_region(bin, 0);
      verboseOut("Verify Read back...");
      if (nxpdfu.verify_read(bin, 0))
        {
          verboseOut("Firmware update verified!");
        }
      else
        {
          Console.WriteLine("ERROR IN VERIFY! PLEASE TRY AGAIN!!");
          return(false);
        }
      
      verboseOut("Reboot.");
      nxpdfu.reset();
      
      return(true);
    }
    
    public static int getBytes(Stream s, ref byte[] buffer, int offset, int length)
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

    public static void DEBUG(String s)
    {
      if (debug)
        {
          Console.WriteLine(s);
        }
    }

    public static void verboseOut(String s)
    {
      if (chatty)
        {
          Console.WriteLine(s);
        }
    }
    
    
  } //class
  
} //namespace
