using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DnmpNetworkClient.OSDependant.Parts.Gui;
using DnmpNetworkClient.OSDependant.Parts.Runtime;
using DnmpNetworkClient.OSDependant.Parts.Tap;

namespace DnmpNetworkClient.OSDependant
{
    internal interface IDependant
    {
        IGui GetGui();

        ITapInterface GetTapInerface();

        IRuntime GetRuntime();
    }
}
