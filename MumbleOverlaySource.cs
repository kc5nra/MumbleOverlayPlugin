using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CLROBS;
using System.Threading;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace MumbleOverlayPlugin
{
    class MumbleOverlaySource : AbstractImageSource, IDisposable
    {
        private Object textureLock = new Object();
        private Texture texture = null;
        private XElement config;

        private OverlayHook overlayHook;

        public MumbleOverlaySource(XElement config)
        {
            this.config = config;

            UpdateSettings();
        }
        
        override public void UpdateSettings()
        {
            
            UInt32 width = (UInt32)config.GetInt("width", 640);
            UInt32 height = (UInt32)config.GetInt("height", 480);

            Size.X = width;
            Size.Y = height;

            config.Parent.SetInt("cx", (Int32)width);
            config.Parent.SetInt("cy", (Int32)height);

            lock (textureLock)
            {
                if (texture != null)
                {
                    texture.Dispose();
                    texture = null;
                }

                texture = GS.CreateTexture(width, height, GSColorFormat.GS_BGRA, null, false, false);
            }

            if (overlayHook == null)
            {
                overlayHook = new OverlayHook();
                overlayHook.Start(width, height);
            }
            else
            {
                overlayHook.UpdateSize(width, height);
            }
        }

        override public void Render(float x, float y, float width, float height)
        {
            lock (textureLock)
            {
                if (texture != null)
                {
                    overlayHook.Draw(texture);
                    GS.DrawSprite(texture, 0xFFFFFFFF, x, y, x + width, y + height);
                }
            }
        }

        public void Dispose()
        {
            overlayHook.Stop();

            lock (textureLock)
            {
                if (texture != null)
                {
                    texture.Dispose();
                    texture = null;
                }
            }
        }
    }
}
