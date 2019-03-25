using DnmpNetworkClient.OSDependent.Parts.Gui;
using DnmpNetworkClient.OSDependent.Parts.Runtime;
using DnmpNetworkClient.OSDependent.Parts.Runtime.Impl;
using DnmpNetworkClient.OSDependent.Parts.Tap;
using DnmpNetworkClient.OSDependent.Parts.Tap.Impl;

namespace DnmpNetworkClient.OSDependent.Impl
{
    internal class UnixDependent : IDependent
    {
        private readonly UnixTapInterface tapInterface = new UnixTapInterface();
        private readonly UnixRuntime runtime = new UnixRuntime();

        public IGui GetGui() => new DummyGui();

        public ITapInterface GetTapInerface() => tapInterface;

        public IRuntime GetRuntime() => runtime;
    }
}
