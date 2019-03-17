using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DNMPWindowsClient.PacketParser
{
    internal class InvalidPacketException : Exception
    {
        internal InvalidPacketException() { }
        internal InvalidPacketException(string message) : base(message) { }
    }
}
