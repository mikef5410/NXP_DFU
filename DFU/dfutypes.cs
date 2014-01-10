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
//

using System;
using System.Runtime.InteropServices;

namespace FirmwareUpdater.DFU
{

  public enum DFUCommands : byte
  {
    DFU_DETACH = 0x0,
    DFU_DNLOAD = 0x1,
    DFU_UPLOAD = 0x2,
    DFU_GETSTATUS = 0x3,
    DFU_CLRSTATUS = 0x4,
    DFU_GETSTATE = 0x5,
    DFU_ABORT = 0x6
  }
  
  public enum DFUStateVals : byte
  {
    STATE_APP_IDLE = 0x00,
    STATE_APP_DETACH = 0x01,
    STATE_DFU_IDLE = 0x02,
    STATE_DFU_DOWNLOAD_SYNC = 0x03,
    STATE_DFU_DOWNLOAD_BUSY = 0x04,
    STATE_DFU_DOWNLOAD_IDLE = 0x05,
    STATE_DFU_MANIFEST_SYNC = 0x06,
    STATE_DFU_MANIFEST = 0x07,
    STATE_DFU_MANIFEST_WAIT_RESET = 0x08,
    STATE_DFU_UPLOAD_IDLE = 0x09,
    STATE_DFU_ERROR = 0x0a
  }

  public enum DFUStatusVals : byte
  {
    DFU_STATUS_OK = 0x00,
    DFU_STATUS_ERROR_TARGET = 0x01,
    DFU_STATUS_ERROR_FILE = 0x02,
    DFU_STATUS_ERROR_WRITE = 0x03,
    DFU_STATUS_ERROR_ERASE = 0x04,
    DFU_STATUS_ERROR_CHECK_ERASED = 0x05,
    DFU_STATUS_ERROR_PROG = 0x06,
    DFU_STATUS_ERROR_VERIFY = 0x07,
    DFU_STATUS_ERROR_ADDRESS = 0x08,
    DFU_STATUS_ERROR_NOTDONE = 0x09,
    DFU_STATUS_ERROR_FIRMWARE = 0x0a,
    DFU_STATUS_ERROR_VENDOR = 0x0b,
    DFU_STATUS_ERROR_USBR = 0x0c,
    DFU_STATUS_ERROR_POR = 0x0d,
    DFU_STATUS_ERROR_UNKNOWN = 0x0e,
    DFU_STATUS_ERROR_STALLEDPKT = 0x0f
  }

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public class DFU_Status
    {
    public DFUStatusVals Status;
    public int PollTimeout;
    public DFUStateVals State;
    public byte String;

      public DFU_Status()
      {
        Status = DFUStatusVals.DFU_STATUS_ERROR_UNKNOWN;
        PollTimeout = 0;
        State = DFUStateVals.STATE_DFU_ERROR;
        String = 0;
      }

  }
    
}
    