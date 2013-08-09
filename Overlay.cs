using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MumbleOverlayPlugin
{
    public class OverlayConstants {
        public static readonly UInt32 OVERLAY_STRUCT_LENGTH = 2060;
        public static readonly UInt32 OVERLAY_MAGIC_NUMBER = 0x00000005;
        public static readonly float OVERLAY_FPS_INTERVAL = 0.25f;
    }

    public enum OverlayMsgType
    {
        Init = 0,
        Shmem,
        Blit,
        Active,
        ProcessId,
        Fps,
        Interactive
    }
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct OverlayMsgHeader
    {
        public UInt32 magic;
        public Int32 length;
        [MarshalAs(UnmanagedType.U4)]
        public OverlayMsgType type;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct OverlayMsgInit
    {
        public UInt32 width;
        public UInt32 height;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 2048)]
    public unsafe struct OverlayMsgShmem
    {
        public fixed SByte name[2048];
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct OverlayMsgBlit
    {
        public UInt32 x;
        public UInt32 y;
        public UInt32 width;
        public UInt32 height;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct OverlayMsgActive
    {
        public UInt32 x;
        public UInt32 y;
        public UInt32 width;
        public UInt32 height;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct OverlayMsgPid
    {
        public UInt32 processId;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct OverlayMsgFps
    {
        public float fps;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct OverlayMsgInteractive
    {
        public bool state;
    }

    [StructLayout(LayoutKind.Explicit, Size=2060)]
    public struct OverlayMsg
    {
        [FieldOffset(0)]
        public Byte headerBuffer;
        [FieldOffset(0)]
        public OverlayMsgHeader overlayMsgHeader;
    }
}
