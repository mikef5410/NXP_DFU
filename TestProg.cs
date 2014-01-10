using System;
using System.IO;
using System.Threading;
using System.Security.Cryptography;
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
    public static bool verbose = false;
    
    public static void Main(string[] args)
    {
      ourDevice=findOurDevice();
      dfu = new DFUMachine(ourDevice);
      
      if (dfu.findInterface())
        {
          Console.WriteLine("Found DFU interface");
          Console.WriteLine(dfu.ToString());
          dfu.claimIface();
          DEBUG("wTransferSize = "+dfu.wTransferSize.ToString());
        }
      //Hack
      if (dfu.wTransferSize == 0) dfu.wTransferSize=2048;
      phaseOne(@"SECLOAD.bin.hdr", dfu);

      Console.WriteLine("Wait 5 sec for next phase ...");
      Thread.Sleep(5000);
      if (ourDevice.IsOpen) ourDevice.Close();
      ourDevice=findOurDevice();
      nxpdfu = new NXPDFUMachine(ourDevice);
      if (nxpdfu.findInterface())
        {
          Console.WriteLine("Found DFU interface");
          Console.WriteLine(nxpdfu.ToString());
          nxpdfu.claimIface();
          DEBUG("wTransferSize = "+nxpdfu.wTransferSize.ToString());
        }
      //Hack
      if (nxpdfu.wTransferSize == 0) nxpdfu.wTransferSize=2048;
      
      phaseTwo(@"LE320.bin",nxpdfu);

      
      if (ourDevice.IsOpen) ourDevice.Close();
      UsbDevice.Exit();
      Environment.Exit(0);   
    } //Main

    public static UsbDevice findOurDevice()
    {
      UsbDeviceFinder ourFinder = new UsbDeviceFinder(0x1fc9,0x000c);
      UsbDevice d;
      UsbRegDeviceList allDevices = UsbDevice.AllDevices.FindAll(ourFinder) ;

      if (allDevices.Count > 1)
        {
          Console.WriteLine("I see too many devices. Make sure only ONE is connected to this machine!");
          UsbDevice.Exit();
          Environment.Exit(1);
        }
      if (allDevices.Count == 0)
        {
          Console.WriteLine("No device is connected to this machine!");
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
      
      uint size=(uint)bin.Length;
      byte[] readbuf = new byte[size];
      
      nxpdfu.set_debug(0,0);
      Console.WriteLine("Erase...");
      nxpdfu.erase_all();
      Console.WriteLine("Program...");
      nxpdfu.program_region(bin, 0);
      Console.WriteLine("Read back...");
      nxpdfu.read_region(ref readbuf, 0, size, out actual);
      Console.WriteLine("Reboot.");
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
      if (verbose)
        {
          Console.WriteLine(s);
        }
    }
    
    
  } //class
  
} //namespace
