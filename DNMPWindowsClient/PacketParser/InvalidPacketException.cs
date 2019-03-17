using System;

namespace DNMPWindowsClient.PacketParser
{
    internal class InvalidPacketException : Exception
    {
        internal InvalidPacketException() { }
        internal InvalidPacketException(string message) : base(message) { }
    }
}
