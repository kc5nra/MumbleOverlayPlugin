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

            overlayHook = new OverlayHook();
            overlayHook.Start(1024, 1024);

            UpdateSettings();
        }
        
        override public void UpdateSettings()
        {
            if (texture != null)
            {
                texture.Dispose();
                texture = null;
            }

            texture = GS.CreateTexture(1024, 1024, GSColorFormat.GS_BGRA, null, false, false);

            overlayHook.UpdateSize(1024, 1024);

            Size.X = 1024;
            Size.Y = 1024;
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
