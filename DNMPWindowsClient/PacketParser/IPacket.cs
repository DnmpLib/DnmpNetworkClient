using System.IO;

namespace DNMPWindowsClient.PacketParser
{
    internal interface IPacket
    {
        byte[] Payload { get; }

        byte[] ToBytes();

        void ToStream(Stream streamTo);
    }
}
