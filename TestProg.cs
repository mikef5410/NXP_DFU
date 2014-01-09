using System;
using System.IO;
using LibUsbDotNet;
using LibUsbDotNet.Main;
using LibUsbDotNet.Info;
using FirmwareUpdater.DFU;

namespace FirmwareUpdater
{
  public static class Program
  {
    public static UsbDevice ourDevice;
    public static UsbDeviceFinder ourFinder = new UsbDeviceFinder(0x1fc9,0x000c);
    public static DFUMachine dfu;
    public static void Main(string[] args)
    {
      UsbRegDeviceList allDevices = UsbDevice.AllDevices.FindAll(ourFinder) ;
      if (allDevices.Count > 1)
        {
          Console.WriteLine("I see too many devices. Make sure only ONE is connected to this machine!");
          if (ourDevice.IsOpen) ourDevice.Close();
          UsbDevice.Exit();
          Environment.Exit(1);
        }
      if (allDevices.Count == 0)
        {
          Console.WriteLine("No device is connected to this machine!");
          if (ourDevice.IsOpen) ourDevice.Close();
          UsbDevice.Exit();
          Environment.Exit(1);
        }
      allDevices[0].Open(out ourDevice);
      dfu = new DFUMachine(ourDevice);
      
      if (dfu.findInterface())
        {
          Console.WriteLine("Found DFU interface");
          Console.WriteLine(dfu.ToString());
          dfu.claimIface();
          Console.WriteLine("wTransferSize = "+dfu.wTransferSize.ToString());
        }
      //Hack
      if (dfu.wTransferSize == 0) dfu.wTransferSize=2048;
      phaseOne(@"SECLOAD.bin.hdr", dfu);
           
      if (ourDevice.IsOpen) ourDevice.Close();
      UsbDevice.Exit();
      Environment.Exit(0);   
    } //Main

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
          Console.WriteLine("Got " + nbytes.ToString() + "bytes, total:" + totalbytes.ToString());
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
    
  } //class
  
} //namespace
