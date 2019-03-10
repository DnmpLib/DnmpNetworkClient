using System;
using System.IO;

namespace DNMPWindowsClient.PacketParser
{
    internal class UdpPacket : IPacket
    {
        internal ushort SourcePort;
        internal ushort DestinationPort;
        internal ushort Length;
        private readonly short checksum;
        internal IPacket PayloadPacket;

        internal UdpPacket(Stream stream, int readAmount = int.MaxValue)
        {
            if (readAmount < 8) throw new InvalidPacketException();
            var reader = new BinaryReader(stream);
            SourcePort = reader.ReadUInt16();
            DestinationPort = reader.ReadUInt16();
            Length = reader.ReadUInt16();
            checksum = reader.ReadInt16();
            if (readAmount < Length) throw new InvalidPacketException();
            if (SourcePort == 67 || SourcePort == 68 || DestinationPort == 67 || DestinationPort == 68)
                PayloadPacket = new DhcpPacket(stream, Length - 8);
            else PayloadPacket = new DummyPacket(stream, Length - 8);
            reader.ReadBytes(readAmount - Length);
        }

        internal UdpPacket(ushort sourcePort, ushort destinationPort, IPacket payloadPacket, IPv4Packet parent)
        {
            SourcePort = sourcePort;
            DestinationPort = destinationPort;
            Length = (ushort)(payloadPacket.Payload.Length + 8);
            PayloadPacket = payloadPacket;
            var payload = ToBytes();
            var sum = 0;
            for (var i = 0; i < payload.Length / 2; i++)
                sum += BitConverter.ToUInt16(payload, i * 2);
            sum += BitConverter.ToInt32(parent.SourceAddress.GetAddressBytes(), 0);
            sum += BitConverter.ToInt32(parent.DestinationAddress.GetAddressBytes(), 0);
            sum += Length;
            checksum = (short)(sum >> 16 + sum << 16 >> 16);
        }

        internal static UdpPacket Parse(byte[] bytes) => new UdpPacket(new MemoryStream(bytes));

        public byte[] Payload => PayloadPacket.ToBytes();

        public byte[] ToBytes()
        {
            var stream = new MemoryStream();
            ToStream(stream);
            return stream.ToArray();
        }

        public void ToStream(Stream streamTo)
        {
            var writer = new BinaryWriter(streamTo);
            writer.Write(SourcePort);
            writer.Write(DestinationPort);
            writer.Write(Length);
            writer.Write(checksum);
            PayloadPacket.ToStream(streamTo);
        }
    }
}
