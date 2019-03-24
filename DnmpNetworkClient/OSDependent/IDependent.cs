using DnmpNetworkClient.OSDependent.Parts.Gui;
using DnmpNetworkClient.OSDependent.Parts.Runtime;
using DnmpNetworkClient.OSDependent.Parts.Tap;

namespace DnmpNetworkClient.OSDependent
{
    internal interface IDependent
    {
        IGui GetGui();

        ITapInterface GetTapInerface();

        IRuntime GetRuntime();
    }
}
