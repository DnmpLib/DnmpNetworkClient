using System.IO;
using System.Net.NetworkInformation;

namespace DnmpNetworkClient.OSDependent.Parts.Tap
{
    internal interface ITapInterface
    {
        PhysicalAddress GetPhysicalAddress();
        Stream Open();
        void Close();
    }
}
