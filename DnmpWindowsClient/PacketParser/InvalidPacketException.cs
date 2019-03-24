using System;

namespace DnmpWindowsClient.PacketParser
{
    internal class InvalidPacketException : Exception
    {
        internal InvalidPacketException() { }
        internal InvalidPacketException(string message) : base(message) { }
    }
}
