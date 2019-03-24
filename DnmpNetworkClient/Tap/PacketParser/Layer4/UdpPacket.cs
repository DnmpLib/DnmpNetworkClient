using System;
using System.IO;
using System.Linq;
using DnmpLibrary.Util.BigEndian;
using DnmpNetworkClient.Tap.PacketParser.Layer3;
using DnmpNetworkClient.Tap.PacketParser.Layer7;

namespace DnmpNetworkClient.Tap.PacketParser.Layer4
{
    internal class UdpPacket : IPacket
    {
        internal ushort SourcePort;
        internal ushort DestinationPort;
        internal ushort Length;
        private readonly ushort checksum;
        internal IPacket PayloadPacket;

        internal UdpPacket(Stream stream, int readAmount = int.MaxValue)
        {
            if (readAmount < 8) throw new InvalidPacketException();
            var reader = new BigEndianBinaryReader(stream);
            SourcePort = reader.ReadUInt16();
            DestinationPort = reader.ReadUInt16();
            Length = reader.ReadUInt16();
            checksum = reader.ReadUInt16();
            if (readAmount < Length) throw new InvalidPacketException();
            try
            {
                if (SourcePort == 67 || SourcePort == 68 || DestinationPort == 67 || DestinationPort == 68)
                    PayloadPacket = new DhcpPacket(stream, Length - 8);
                else if (SourcePort == 53 || DestinationPort == 53)
                    PayloadPacket = new DnsPacket(stream, Length - 8);
                else
                    PayloadPacket = new DummyPacket(stream, Length - 8);
            }
            catch (Exception)
            {
                PayloadPacket = null;
            }

            reader.ReadBytes(readAmount - Length);
        }

        internal UdpPacket(ushort sourcePort, ushort destinationPort, IPacket payloadPacket, IPv4Packet parent)
        {
            SourcePort = sourcePort;
            DestinationPort = destinationPort;
            Length = (ushort)(payloadPacket.ToBytes().Length + 8);
            PayloadPacket = payloadPacket;
            checksum = 0;
            var preHeader = parent.SourceAddress.GetAddressBytes().Concat(parent.DestinationAddress.GetAddressBytes())
                .Concat(new[] { (byte) 0, (byte) IPv4Packet.PacketType.Udp })
                .Concat(new[] { (byte)(Length / 256), (byte)(Length % 256) });
            var payload = preHeader.Concat(ToBytes()).ToArray();
            var sum = 0;
            for (var i = 0; i < payload.Length / 2; i++)
                sum += payload[i * 2] << 8 | payload[i * 2 + 1];
            if (payload.Length % 2 == 1)
                sum += payload.Last() << 8;
            checksum = (ushort)~((sum >> 16) + (sum & 0xFFFF));
        }

        internal static UdpPacket Parse(byte[] bytes) => new UdpPacket(new MemoryStream(bytes), bytes.Length);

        public byte[] Payload => PayloadPacket.ToBytes();

        public byte[] ToBytes()
        {
            var stream = new MemoryStream();
            ToStream(stream);
            return stream.ToArray();
        }

        public void ToStream(Stream streamTo)
        {
            var writer = new BigEndianBinaryWriter(streamTo);
            writer.Write(SourcePort);
            writer.Write(DestinationPort);
            writer.Write(Length);
            writer.Write(checksum);
            PayloadPacket.ToStream(streamTo);
        }
    }
}
