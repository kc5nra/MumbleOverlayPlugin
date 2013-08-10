    using CLROBS;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MumbleOverlayPlugin
{
    class OverlayMsgBuffer
    {
        private Int32 headerLength;
        private Byte[] writeBuffer;
        private IntPtr writePtr;
        private Byte[] readBuffer;
        private IntPtr readPtr;

        public OverlayMsgBuffer()
        {
            headerLength = Marshal.SizeOf(typeof(OverlayMsgHeader));
            readBuffer = new byte[OverlayConstants.OVERLAY_STRUCT_LENGTH];
            writeBuffer = new byte[OverlayConstants.OVERLAY_STRUCT_LENGTH];
            readPtr = Marshal.AllocHGlobal((Int32)OverlayConstants.OVERLAY_STRUCT_LENGTH);
            writePtr = Marshal.AllocHGlobal((Int32)OverlayConstants.OVERLAY_STRUCT_LENGTH);
        }

        ~OverlayMsgBuffer() 
        {
            Marshal.FreeHGlobal(readPtr);
            Marshal.FreeHGlobal(writePtr);
        }

        Byte[] GetBytes<T>(OverlayMsg header, T message)
        {
            Marshal.StructureToPtr(header, readPtr, true);
            IntPtr adjustedPtr = new IntPtr(readPtr.ToInt64() + headerLength);
            Marshal.StructureToPtr(message, adjustedPtr, true);
            Marshal.Copy(readPtr, readBuffer, 0, (Int32)OverlayConstants.OVERLAY_STRUCT_LENGTH);
            
            return readBuffer;
        }

        public void writeTo<T>(NamedPipeClientStream pipe, OverlayMsgType type, T message)
        {
            OverlayMsg header = new OverlayMsg();
            Int32 typeLength = Marshal.SizeOf(typeof(T));
            header.overlayMsgHeader.magic = OverlayConstants.OVERLAY_MAGIC_NUMBER;
            header.overlayMsgHeader.length = typeLength;
            header.overlayMsgHeader.type = type;

            pipe.Write(GetBytes(header, message), 0, headerLength + typeLength);
        }

        public OverlayMsg readHeaderFrom(NamedPipeClientStream pipe)
        {
            int readPosition = 0;
            while (headerLength - readPosition > 0)
            {
                readPosition  += pipe.Read(readBuffer, readPosition, headerLength - readPosition);
            }

            Marshal.Copy(readBuffer, 0, readPtr, headerLength);
            return (OverlayMsg)Marshal.PtrToStructure(readPtr, typeof(OverlayMsg));
        }

        public T readFrom<T>(NamedPipeClientStream pipe, int typeLength)
        {
            int readPosition = 0;
            while (headerLength - readPosition > 0)
            {
                readPosition += pipe.Read(readBuffer, headerLength + readPosition, typeLength - readPosition);
            }
            IntPtr adjustedPtr = new IntPtr(readPtr.ToInt64() + headerLength);
            Marshal.Copy(readBuffer, headerLength, adjustedPtr, typeLength);
            return (T)Marshal.PtrToStructure(adjustedPtr, typeof(T));
        }
    }

    class SharedOverlayBitmap
    {
        private String shmemKey;
        private MemoryMappedViewStream mappedOverlayStream;
        private byte[] sharedBitmapData;

        private UInt32 width;
        private UInt32 height;

        private Object bitmapLock = new Object();

        public void UpdateSize(UInt32 width, UInt32 height)
        {
            this.width = width;
            this.height = height;
            
            lock (bitmapLock)
            {
                sharedBitmapData = new byte[width * height * 4];
            }
        }

        public void UpdateSharedMemoryKey(String sharedMemoryKey)
        {
            if (mappedOverlayStream != null)
            {
                mappedOverlayStream.Close();
                mappedOverlayStream = null;
            }

            shmemKey = sharedMemoryKey;
        }

        public bool Fetch()
        {
            if (!IsInitialized())
            {
                if (!InitializeSharedMemory())
                {
                    return false;
                }
            }

            lock (bitmapLock)
            {
                int readByteLength = 0;
                if ((readByteLength = mappedOverlayStream.Read(sharedBitmapData, 0, sharedBitmapData.Length)) != sharedBitmapData.Length)
                {
                    API.Instance.Log("Shared memory read had unexpected length; Expected {0} got {1}", sharedBitmapData.Length, readByteLength);
                    return false;
                }
            }
            mappedOverlayStream.Position = 0;

            return true;           
        }

        private Boolean IsInitialized()
        {
            return mappedOverlayStream != null && mappedOverlayStream.CanRead;
        }

        private bool InitializeSharedMemory() 
        {
            if (shmemKey == null)
            {
                API.Instance.Log("Unable to initialize shared memory; null shmem key");
                return false;
            }

            if (sharedBitmapData == null)
            {
                API.Instance.Log("Unable to initialize shared memory; width and height not set");
                return false;
            }

            if (mappedOverlayStream != null)
            {
                mappedOverlayStream.Close();
                mappedOverlayStream = null;
            }

            try
            {
                MemoryMappedFile mappedOverlayBitmap = MemoryMappedFile.CreateOrOpen(shmemKey, width * height * 4);
                mappedOverlayStream = mappedOverlayBitmap.CreateViewStream();
            }
            catch (FileNotFoundException e)
            {
                API.Instance.Log("Shared memory file with shmem key {0} not found; {1}", shmemKey, e.Message);
                return false;
            }
            catch (UnauthorizedAccessException e)
            {
                API.Instance.Log("Unable to access shmem key {0}; {1}", shmemKey, e.Message);
                return false;
            }

            return true;
        }

        public void Close()
        {
            if (mappedOverlayStream != null)
            {
                mappedOverlayStream.Close();
                mappedOverlayStream = null;
            }
        }

        public UInt32 Width 
        {
            get { return width; }
        }

        public UInt32 Height
        {
            get { return height; }
        }

        public byte[] BitmapData
        {
            get 
            {
                lock (bitmapLock)
                {
                    return sharedBitmapData;
                }
            }
        }
    }

    class OverlayHook
    {
        private NamedPipeClientStream overlayPipe;
        private OverlayMsgBuffer buffer;
        private SharedOverlayBitmap bitmap;
        private Thread overlayHookThread;

        // fps timing
        private Stopwatch stopwatch;
        private UInt32 frameCount;
        private UInt32 lastFpsUpdate;

        private bool isConnected;

        public OverlayHook()
        {
            buffer = new OverlayMsgBuffer();
            bitmap = new SharedOverlayBitmap();
            stopwatch = new Stopwatch();
            isConnected = false;
        }

        public delegate void StartDelegate();

        public void Start(UInt32 width, UInt32 height)
        {
            Thread startThread = new Thread(new ThreadStart(() =>
            {
                if (overlayPipe != null && overlayPipe.IsConnected)
                {
                    return;
                }

                overlayPipe = new NamedPipeClientStream(".", "MumbleOverlayPipe", PipeDirection.InOut, PipeOptions.Asynchronous);
                try
                {
                    overlayPipe.Connect(5000);
                }
                catch (TimeoutException e)
                {
                    API.Instance.Log("Timeout connecting to named pipe \\\\.\\MumbleOverlayPipe; {0}", e.Message);
                    return;
                }

                OverlayMsgPid pid = new OverlayMsgPid();
                pid.processId = (UInt32)Process.GetCurrentProcess().Id;

                buffer.writeTo(overlayPipe, OverlayMsgType.ProcessId, pid);

                stopwatch.Start();

                overlayHookThread = new Thread(new ThreadStart(WaitForRead));
                overlayHookThread.Name = "OverlayHookPipeListener";
                overlayHookThread.Start();

                isConnected = true;

                UpdateSize(width, height);

            }));

            startThread.Name = "OverlayHookStarter";
            startThread.Start();
        }

        public void Stop()
        {
            if (overlayPipe != null && isConnected)
            {
                overlayPipe.Close();
            }

            if (overlayHookThread != null)
            {
                overlayHookThread.Abort();

                Stopwatch terminateCountdown = new Stopwatch();
                terminateCountdown.Start();
                while (overlayHookThread.IsAlive)
                {
                    if (terminateCountdown.ElapsedMilliseconds > 2000)
                    {
                        API.Instance.Log("Overlay hook thread unable to be terminated, 2000ms timeout exceeded; Giving up");
                        break;
                    }
                }
            }

            if (bitmap != null)
            {
                bitmap.Close();
                bitmap = null;
            }
        }

        public void UpdateSize(UInt32 width, UInt32 height)
        {
            bitmap.UpdateSize(width, height);
                        
            OverlayMsgInit init = new OverlayMsgInit();
            init.width = width;
            init.height = height;

            if (isConnected)
            {
                buffer.writeTo(overlayPipe, OverlayMsgType.Init, init);
            }
        }

        public void Draw(Texture texture)
        {
            if (bitmap != null && bitmap.BitmapData != null)
            {
                texture.SetImage(bitmap.BitmapData, GSImageFormat.GS_IMAGEFORMAT_BGRA, bitmap.Width * 4);
            }
        }
        public void WaitForRead()
        {
            try
            {
                while (overlayPipe.CanRead && overlayPipe.IsConnected)
                {
                    OverlayMsg header = buffer.readHeaderFrom(overlayPipe);

                    Int32 typeLength = header.overlayMsgHeader.length;

                    switch (header.overlayMsgHeader.type)
                    {
                        case OverlayMsgType.Shmem:
                            {
                                var shmem = buffer.readFrom<OverlayMsgShmem>(overlayPipe, typeLength);

                                String shmemKey = null;
                                unsafe
                                {
                                    // length - 1 to remove trailing \0
                                    shmemKey = new String(shmem.name, 0, typeLength - 1);
                                }

                                if (shmemKey != null)
                                {
                                    bitmap.UpdateSharedMemoryKey(shmemKey);
                                }
                                else
                                {
                                    API.Instance.Log("Unable to decode shmem key");
                                    continue;
                                }

                                break;
                            }
                        case OverlayMsgType.Blit:
                            {
                                var blit = buffer.readFrom<OverlayMsgBlit>(overlayPipe, typeLength);
                                
                                if (blit.x + blit.width > bitmap.Width
                                    || blit.y + blit.height > bitmap.Height)
                                {
                                    API.Instance.Log("Invalid width and height of blit request; Expected w={0},h={1} got w={2},h={3}",
                                        bitmap.Width,
                                        bitmap.Height,
                                        blit.x + blit.width,
                                        blit.y + blit.height);

                                    break;
                                }

                                frameCount++;

                                if (!bitmap.Fetch())
                                {
                                    API.Instance.Log("Fetch of bitmap from shared memory failed");
                                }

                                float elapsed = (float)(stopwatch.Elapsed.TotalMilliseconds - lastFpsUpdate) / 1000;
                                if (elapsed > OverlayConstants.OVERLAY_FPS_INTERVAL)
                                {
                                    OverlayMsgFps fps = new OverlayMsgFps();
                                    // really? int?
                                    fps.fps = (float)frameCount / elapsed;
                                    buffer.writeTo(overlayPipe, OverlayMsgType.Fps, fps);

                                    frameCount = 0;
                                    lastFpsUpdate = 0;
                                    stopwatch.Restart();
                                }
                                break;
                            }
                        case OverlayMsgType.Active:
                            {
                                var active = buffer.readFrom<OverlayMsgActive>(overlayPipe, typeLength);
                                break;
                            }
                    }
                }
            }
            catch (ThreadAbortException e)
            {
                API.Instance.Log("OverlayHookThread aborted; {0}", e.Message);
            }
        }
    }
}
