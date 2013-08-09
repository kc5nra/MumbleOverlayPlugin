using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CLROBS;
namespace MumbleOverlayPlugin
{
    class MumbleOverlayPlugin : AbstractPlugin
    {
        public MumbleOverlayPlugin()
        {
            // Setup the default properties
            Name = "Mumble Overlay Plugin";
            Description = "Lets you add the mumble overlay as a source";
        }
        
        public override bool LoadPlugin()
        {
            API.Instance.AddImageSourceFactory(new MumbleOverlaySourceFactory());
            return true;
        }
    }
}
