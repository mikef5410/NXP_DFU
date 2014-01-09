//
using System;
using LibUsbDotNet;
using LibUsbDotNet.Main;
using LibUsbDotNet.Info;


namespace FirmwareUpdater.DFU
{
  public class DFUMachine
  {
    private UsbDevice myUSBDevice;
    private UsbInterfaceInfo myDFUiface;
    private UsbConfigInfo myDFUconfig;
    public byte DFUconfig;
    public byte DFUinterface;
    public  int wTransferSize;
    public  byte[] iobuf = new byte[4096];
    public  int transaction = 0;
    public  int DFU_DETACH_TIMEOUT = 1000;
    public  int DFU_TIMEOUT = 20000;
    
    /// <summary>
    ///   Make a new DFUMachine for a given USB device
    /// </summary>
    public DFUMachine(UsbDevice thisDevice)
    {
      myUSBDevice=thisDevice;
    }

    /// <summary>
    ///   Find the interface that supports DFU. Return true if found.
    /// </summary>
    public bool findInterface()
    {
      byte currentConfig;
      
      foreach (UsbConfigInfo config in myUSBDevice.Configs)
        {
          currentConfig=config.Descriptor.ConfigID;
          foreach (UsbInterfaceInfo iface in config.InterfaceInfoList)
            {
              if (((byte)iface.Descriptor.Class == 0xFE /*Application Specific*/) &&
                  ((byte)iface.Descriptor.SubClass == 0x01 /* DFU */) ) {
                DFUinterface=iface.Descriptor.InterfaceID;
                DFUconfig=currentConfig;
                myDFUconfig=config;
                myDFUiface=iface;
                return(true);
              }
            }
        }
      return(false);
    }

    public bool claimIface()
    {
      IUsbDevice wholeUsbDevice = myUSBDevice as IUsbDevice;
      if (!ReferenceEquals(wholeUsbDevice, null))
        {
          // This is a "whole" USB device. Before it can be used, 
          // the desired configuration and interface must be selected.
          wholeUsbDevice.SetConfiguration(DFUconfig);
          wholeUsbDevice.ClaimInterface(DFUinterface);
        }
      //Now we need to get  wTransferSize and we get that
      //from a byte array returned by CustomDescriptors
      foreach (byte[] aCustomDesc in myDFUiface.CustomDescriptors)
        {
          if (aCustomDesc[1] == 33) //DFU Interface desc
            {
              //USB Bus format is LITTLE ENDIAN
              wTransferSize = ((aCustomDesc[6]*256) + aCustomDesc[5]);
              if (wTransferSize < myUSBDevice.Info.Descriptor.MaxPacketSize0) wTransferSize=myUSBDevice.Info.Descriptor.MaxPacketSize0;
            }
        }
      return(true);
  }

    public bool transfer_in(byte request, int value, ref byte[] data, int length, out int actual)
    {
      bool rval;
      UsbSetupPacket setup = new UsbSetupPacket(0xA1, request, value, DFUinterface, length);
      rval=myUSBDevice.ControlTransfer(ref setup, data, length, out actual);
      return(rval);
    }

    public bool transfer_in(byte request, int value, ref byte[] data, int length)
    {
      bool rval;
      int actual;
      rval=transfer_in(request, value, ref data, length, out actual);
      return(rval);
    }

    public bool transfer_out(byte request, int value, ref byte[] data, int length, out int actual)
    {
      bool rval;
      UsbSetupPacket setup = new UsbSetupPacket(0x21, request, value, DFUinterface, length);
      rval=myUSBDevice.ControlTransfer(ref setup, data, length, out actual);
      return(rval);
      
    }

    public bool transfer_out(byte request, int value, ref byte[] data, int length)
    {
      bool rval;
      int actual;
      rval=transfer_out(request, value, ref data, length, out actual);
      return(rval);
    }

    public bool abort()
    {
      bool rval;
      rval=transfer_out((byte)DFUCommands.DFU_ABORT, 0, ref iobuf, 0);
      return(rval);
    }

    public DFUStateVals get_state()
    {
      DFUStateVals state;
      bool rval;
      
      rval=transfer_in((byte)DFUCommands.DFU_GETSTATE, 0, ref iobuf, iobuf.Length);
      state=(DFUStateVals)iobuf[0];
      return(state);
    }

    public bool clear_status()
    {
      bool rval;
      rval=transfer_out((byte)DFUCommands.DFU_CLRSTATUS, 0, ref iobuf, 0);
      return(rval);
    }

    public bool get_status(out DFU_Status status)
    {
      bool rval;
      int actual;

      status = new DFU_Status();
      rval=transfer_in((byte)DFUCommands.DFU_GETSTATUS, 0, ref iobuf, iobuf.Length, out actual);
      if (rval && (actual == 6))
        {
          status.Status=(DFUStatusVals)iobuf[0];
          status.PollTimeout = iobuf[1] + 256*iobuf[2] + 65536*iobuf[3];
          status.State=(DFUStateVals)iobuf[4];
          status.String=iobuf[5];
        }
      else
        {
          return(false);
        }
      return(true);
    }

    public bool detach( int timeout )
    {
      bool rval;
      rval = transfer_out((byte)DFUCommands.DFU_DETACH, timeout, ref iobuf, 0);
      return(rval);
    }

    public bool wait_idle()
    {
      DFU_Status status;
      do {
        get_status(out status);
      } while (status.State != DFUStateVals.STATE_DFU_DOWNLOAD_IDLE);
      return(true);
    }

    public bool make_idle(bool initial_abort)
    {
      int retries = 4;
      DFU_Status status;
      IUsbDevice thisDev = myUSBDevice as IUsbDevice;
      
      if (initial_abort)
        {
          abort();
        }

      while (retries > 0)
        {
          if (!get_status(out status))
            {
              clear_status();
              continue;
            }
          switch (status.State)
            {
            case DFUStateVals.STATE_DFU_IDLE:
              if( DFUStatusVals.DFU_STATUS_OK == status.Status ) {
                return(true);
              }

              /* We need the device to have the DFU_STATUS_OK status. */
              clear_status();
              break;

            case DFUStateVals.STATE_DFU_DOWNLOAD_SYNC:   /* abort -> idle */
            case DFUStateVals.STATE_DFU_DOWNLOAD_IDLE:   /* abort -> idle */
            case DFUStateVals.STATE_DFU_MANIFEST_SYNC:   /* abort -> idle */
            case DFUStateVals.STATE_DFU_UPLOAD_IDLE:     /* abort -> idle */
            case DFUStateVals.STATE_DFU_DOWNLOAD_BUSY:   /* abort -> error */
            case DFUStateVals.STATE_DFU_MANIFEST:        /* abort -> error */
              abort();
              break;

            case DFUStateVals.STATE_DFU_ERROR:
              clear_status();
              break;

            case DFUStateVals.STATE_APP_IDLE:
              detach( DFU_DETACH_TIMEOUT );
              break;

            case DFUStateVals.STATE_APP_DETACH:
            case DFUStateVals.STATE_DFU_MANIFEST_WAIT_RESET:
              //DEBUG( "Resetting the device\n" );
              thisDev.ResetDevice();
              return(false);
            }
          retries--;
        }
      return(false);
    }

    public int download( ref byte[] data, int length)
    {
      int actual;
      bool rval;

      rval = transfer_out( (byte)DFUCommands.DFU_DNLOAD, transaction++, ref data, length, out actual);
      if (rval) return(actual);
      return(-1);
    }

    public int upload( ref byte[] data, int length)
    {
      int actual;
      bool rval;

      rval = transfer_in( (byte)DFUCommands.DFU_UPLOAD, transaction++, ref data, length, out actual);
      if (rval) return(actual);
      return(-1);
    }
    
  } //class
} //namespace