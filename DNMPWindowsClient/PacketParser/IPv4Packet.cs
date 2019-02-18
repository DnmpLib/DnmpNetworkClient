using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

namespace DNMPWindowsClient.PacketParser
{
    internal sealed class IPv4Packet : IPacket
    {
        internal IPAddress SourceAddress;
        internal IPAddress DestinationAddress;
        private readonly byte[] preAddress;
        private readonly byte[] postAddress;
        internal IPacket PayloadPacket;

        internal IPv4Packet(Stream stream)
        {
            var reader = new BinaryReader(stream);
            preAddress = reader.ReadBytes(12);
            SourceAddress = new IPAddress(reader.ReadBytes(4));
            DestinationAddress = new IPAddress(reader.ReadBytes(4));
            postAddress = reader.ReadBytes(BitConverter.ToUInt16(preAddress, 6) & 0b0001111111111111);
            PayloadPacket = new DummyPacket(stream);
        }

        internal static IPv4Packet Parse(byte[] bytes)
        {
            var stream = new MemoryStream(bytes);
            return new IPv4Packet(stream);
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
