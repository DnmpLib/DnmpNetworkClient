using System.IO;

namespace DnmpWindowsClient.PacketParser
{
    internal interface IPacket
    {
        byte[] Payload { get; }

        byte[] ToBytes();

        void ToStream(Stream streamTo);
    }
}
