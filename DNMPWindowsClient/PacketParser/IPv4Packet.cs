using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

namespace DNMPWindowsClient.PacketParser
{
    internal sealed class IpV4Packet : IPacket
    {
        internal IPAddress SourceAddress;
        internal IPAddress DestinationAddress;
        private readonly byte[] preAddress;
        private readonly byte[] postAddress;
        internal IPacket PayloadPacket;

        internal IpV4Packet(Stream stream)
        {
            var reader = new BinaryReader(stream);
            preAddress = reader.ReadBytes(12);
            SourceAddress = new IPAddress(reader.ReadBytes(4));
            DestinationAddress = new IPAddress(reader.ReadBytes(4));
            postAddress = reader.ReadBytes(Math.Max(((preAddress[0] & 0b00001111) - 5) * 4, 0));
            PayloadPacket = new DummyPacket(stream);
        }

        internal static IpV4Packet Parse(byte[] bytes)
        {
            var stream = new MemoryStream(bytes);
            return new IpV4Packet(stream);
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
            writer.Write(preAddress);
            writer.Write(SourceAddress.GetAddressBytes());
            writer.Write(DestinationAddress.GetAddressBytes());
            writer.Write(postAddress);
            PayloadPacket.ToStream(streamTo);
        }
    }
}
