using System;
using System.IO;
using System.Net.NetworkInformation;

namespace DnmpNetworkClient.OSDependent.Parts.Tap.Impl
{
    internal class UnixTapInterface : ITapInterface
    {
        public PhysicalAddress GetPhysicalAddress()
        {
            throw new NotImplementedException();
        }

        public Stream Open()
        {
            throw new NotImplementedException();
        }

        public void Close()
        {
            throw new NotImplementedException();
        }
    }
}
