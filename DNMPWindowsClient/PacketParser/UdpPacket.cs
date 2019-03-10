using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DNMPWindowsClient.PacketParser
{
    internal class UdpPacket : IPacket
    {
        internal ushort SourcePort;
        internal ushort DestinationPort;
        internal ushort Length;
        private readonly short checksum;
        internal IPacket PayloadPacket;

        internal UdpPacket(Stream stream)
        {
            var reader = new BinaryReader(stream);
            SourcePort = reader.ReadUInt16();
            DestinationPort = reader.ReadUInt16();
            Length = reader.ReadUInt16();
            checksum = reader.ReadInt16();
            PayloadPacket = new DummyPacket(stream);
        }

        internal UdpPacket(ushort sourcePort, ushort destinationPort, IPacket payloadPacket, IpV4Packet parent)
        {
            SourcePort = sourcePort;
            DestinationPort = destinationPort;
            Length = (ushort)(payloadPacket.Payload.Length + 8);
            PayloadPacket = payloadPacket;
            var payload = ToBytes();
            var sum = 0;
            for (var i = 0; i < payload.Length / 2; i++)
            {
                sum += BitConverter.ToUInt16(payload, i * 2);
            }
            sum += BitConverter.ToInt32(parent.SourceAddress.GetAddressBytes(), 0);
            sum += BitConverter.ToInt32(parent.DestinationAddress.GetAddressBytes(), 0);
            sum += Length;
            checksum = (short)(sum >> 16 + sum << 16 >> 16);
        }

        internal static UdpPacket Parse(byte[] bytes)
        {
            var stream = new MemoryStream(bytes);
            return new UdpPacket(stream);
        }

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
