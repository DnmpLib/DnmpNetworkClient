using System.IO;
using System.Net.NetworkInformation;

namespace DnmpWindowsClient.OSDependant.Parts.Tap
{
    internal interface ITapInterface
    {
        PhysicalAddress GetPhysicalAddress();
        Stream Open();
        void Close();
    }
}
