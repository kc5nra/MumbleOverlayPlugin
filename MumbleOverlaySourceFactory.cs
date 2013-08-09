using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CLROBS;


namespace MumbleOverlayPlugin
{
    public class MumbleOverlaySourceFactory : AbstractImageSourceFactory
    {
        public MumbleOverlaySourceFactory()
        {
            ClassName = "MumbleOverlaySource";
            DisplayName = "Mumble Overlay";
        }

        public override ImageSource Create(XElement data)
        {
            return new MumbleOverlaySource(data);
        }

        public override bool ShowConfiguration(XElement data)
        {
            MumbleOverlayConfigurationDialog dialog = new MumbleOverlayConfigurationDialog(data);
            dialog.Show();
            return dialog.ShowDialog().GetValueOrDefault(false);
        }
    }
}
