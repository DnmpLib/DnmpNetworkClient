using System;

namespace DnmpNetworkClient.Tap.PacketParser
{
    internal class InvalidPacketException : Exception
    {
        internal InvalidPacketException() { }
        internal InvalidPacketException(string message) : base(message) { }
    }
}
