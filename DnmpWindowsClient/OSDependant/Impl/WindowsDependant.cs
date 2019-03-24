using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DnmpWindowsClient.OSDependant.Parts.Gui;
using DnmpWindowsClient.OSDependant.Parts.Gui.Impl;
using DnmpWindowsClient.OSDependant.Parts.Runtime;
using DnmpWindowsClient.OSDependant.Parts.Runtime.Impl;
using DnmpWindowsClient.OSDependant.Parts.Tap;
using DnmpWindowsClient.OSDependant.Parts.Tap.Impl;

namespace DnmpWindowsClient.OSDependant.Impl
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
