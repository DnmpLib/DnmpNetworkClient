using System.IO;

namespace DNMPWindowsClient.PacketParser
{
    internal sealed class DummyPacket : IPacket
    {
        public byte[] Payload { get; private set; }
        internal DummyPacket(Stream data, int readAmount = int.MaxValue)
        {
            Payload = new byte[data.Length - data.Position];
            data.Read(Payload, 0, readAmount);
        }
        internal DummyPacket(byte[] payload)
        {
            Payload = payload;
        }

        public byte[] ToBytes()
        {
            return Payload;
        }

        public void ToStream(Stream streamTo)
        {
            streamTo.Write(Payload, 0, Payload.Length);
        }
    }
}
