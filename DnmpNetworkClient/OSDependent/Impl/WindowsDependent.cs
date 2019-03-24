using DnmpNetworkClient.OSDependent.Parts.Gui;
using DnmpNetworkClient.OSDependent.Parts.Gui.Impl;
using DnmpNetworkClient.OSDependent.Parts.Runtime;
using DnmpNetworkClient.OSDependent.Parts.Runtime.Impl;
using DnmpNetworkClient.OSDependent.Parts.Tap;
using DnmpNetworkClient.OSDependent.Parts.Tap.Impl;

namespace DnmpNetworkClient.OSDependent.Impl
{
    internal class WindowsDependent : IDependent
    {
        private readonly WindowsGui gui = new WindowsGui();
        private readonly WindowsTapInterface tapInterface = new WindowsTapInterface();
        private readonly WindowsRuntime runtime = new WindowsRuntime();

        public IGui GetGui() => gui;

        public ITapInterface GetTapInerface() => tapInterface;

        public IRuntime GetRuntime() => runtime;
    }
}
