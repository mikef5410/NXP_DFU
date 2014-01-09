//

using System;
using System.Runtime.InteropServices;

namespace FirmwareUpdater.NXPDFU
{

  static class Constants
  {
    /**
     * Magic value used to indicate DFU programming algorithm and DFU Utility
     * support. This is used to lock algorithm support to DFU Utility tool
     * version to prevent issues with non-compatible versions. The upper
     * 16 bits contain a magic number and the lower 16 bits contain the
     * version number in x.y format (1.10 = 0x010A).
     */
    const uint DFUPROG_VALIDVAL = (0x18430000 | (0x010A));
  }

  
  public enum DFUCommands : uint  //32 bit field
  {
    HOSTCMD_SETDEBUG,       /* Enables/disables debug output */
    HOSTCMD_ERASE_ALL,      /* Erase the entire device */
    HOSTCMD_ERASE_REGION,   /* Erase a region defined with addr/size */
    HOSTCMD_PROGRAM,        /* Program a region defined with addr/size */
    HOSTCMD_READBACK,       /* Read a region defined with addr/size */
    HOSTCMD_RESET,          /* Reset the device/board */
    HOSTCMD_EXECUTE         /* Execute code at address addr */

  }
  
  public enum DFUStatusVals : uint  //32 bit field
  {
    OPSTS_IDLE,             /* Idle, can accept new host command */
    OPSTS_ERRER,            /* Erase error */
    OPSTS_PROGER,           /* Program error */
    OPSTS_READER,           /* Readback error */
    OPSTS_ERRUN,            /* Unknown error */
    OPSTS_READBUSY,         /* Device is busy reading a block of data */
    OPSTS_READTRIG,         /* Device data is ready to read */
    OPSTS_READREADY,        /* Block of data is ready */
    OPSTS_ERASE_ALL_ST,     /* Device is about to start full erase */
    OPSTS_ERASE_ST,         /* Device is about to start region erase */
    OPSTS_ERASE,            /* Device is currently erasing */
    OPSTS_PROG,             /* Device is currently programming a range */
    OPSTS_PROG_RSVD,        /* Reserved state, not used */
    OPSTS_PROG_STREAM,      /* Device is in buffer streaming mode */
    OPSTS_RESET,            /* Will shutdown and reset */
    OPSTS_EXEC,             /* Will shutdown USB and start execution */
    OPSTS_LOOP              /* Loop on error after DFU status check */
  }

  // Host DFU download packet header. This is appended to a data packet when
  // programming a region.
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct DLPktHdr
    {
    public DFUCommands hostCmd;
    public uint addr;
    public uint size;
    public uint magic;
    }
    

/**
 * When sending data to the host machine, a packet header is appended to
 * the data payload to indicate the state of the target and any debug or
 * error messages.
 */
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct ULPktHdr { //16 bytes
    public DFUCommands cmdResponse;       /* Command responding from host */
    public DFUStatusVals progStatus;        /* Current status of system */
    uint strBytes;          /* Number of bytes in string field */
    uint reserved;
  }

}
