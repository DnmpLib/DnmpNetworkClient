using System.IO;

namespace DnmpWindowsClient.Tap.PacketParser
{
    internal interface IPacket
    {
        byte[] Payload { get; }

        byte[] ToBytes();

        void ToStream(Stream streamTo);
    }
}
