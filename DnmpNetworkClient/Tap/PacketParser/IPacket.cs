using System.IO;

namespace DnmpNetworkClient.Tap.PacketParser
{
    internal interface IPacket
    {
        byte[] Payload { get; }

        byte[] ToBytes();

        void ToStream(Stream streamTo);
    }
}
