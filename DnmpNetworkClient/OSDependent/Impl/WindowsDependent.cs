using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DnmpNetworkClient.OSDependant.Parts.Gui;
using DnmpNetworkClient.OSDependant.Parts.Gui.Impl;
using DnmpNetworkClient.OSDependant.Parts.Runtime;
using DnmpNetworkClient.OSDependant.Parts.Runtime.Impl;
using DnmpNetworkClient.OSDependant.Parts.Tap;
using DnmpNetworkClient.OSDependant.Parts.Tap.Impl;

namespace DnmpNetworkClient.OSDependant.Impl
{
    internal class WindowsDependant : IDependant
    {
        private readonly WindowsGui gui = new WindowsGui();
        private readonly WindowsTapInterface tapInterface = new WindowsTapInterface();
        private readonly WindowsRuntime runtime = new WindowsRuntime();

        public IGui GetGui() => gui;

        public ITapInterface GetTapInerface() => tapInterface;

        public IRuntime GetRuntime() => runtime;
    }
}
