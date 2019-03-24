using DnmpNetworkClient.OSDependent.Parts.Gui;
using DnmpNetworkClient.OSDependent.Parts.Runtime;
using DnmpNetworkClient.OSDependent.Parts.Tap;
using DnmpNetworkClient.OSDependent.Parts.Tap.Impl;

namespace DnmpNetworkClient.OSDependent.Impl
{
    internal class UnixDependent : IDependent
    {
        private readonly UnixTapInterface tapInterface = new UnixTapInterface();

        public IGui GetGui() => new DummyGui();

        public ITapInterface GetTapInerface() => tapInterface;

        public IRuntime GetRuntime() => new DummyRuntime();
    }
}
