using System;
using System.IO;
using System.Net;

namespace DNMPWindowsClient.PacketParser
{
    internal sealed class IPv4Packet : IPacket
    {
        internal enum PacketType : byte
        {
            Udp = 17
        }

        internal byte Version;
        private readonly byte internetHeaderLength;
        internal byte TypeOfService;
        private readonly ushort totalLength;
        private readonly ushort identification;
        private readonly byte flags;
        private readonly ushort fragmentOffset;
        internal byte TimeToLive;
        internal PacketType Protocol;
        private readonly ushort headerChecksum;
        private readonly byte[] options;
        internal IPAddress SourceAddress;
        internal IPAddress DestinationAddress;
        internal IPacket PayloadPacket;

        internal IPv4Packet(Stream stream)
        {
            var reader = new BinaryReader(stream);
            var tmpbyte = reader.ReadByte();
            Version = (byte)(tmpbyte >> 4);
            internetHeaderLength = (byte)(tmpbyte & 0b00001111);
            TypeOfService = reader.ReadByte();
            totalLength = reader.ReadUInt16();
            identification = reader.ReadUInt16();
            fragmentOffset = reader.ReadUInt16();
            flags = (byte)(fragmentOffset >> 13);
            fragmentOffset = (byte) (fragmentOffset & 0b0001111111111111);
            TimeToLive = reader.ReadByte();
            Protocol = (PacketType)reader.ReadByte();
            headerChecksum = reader.ReadUInt16();
            SourceAddress = new IPAddress(reader.ReadBytes(4));
            DestinationAddress = new IPAddress(reader.ReadBytes(4));
            options = reader.ReadBytes(internetHeaderLength - 20);
            reader.ReadBytes((internetHeaderLength + 3) / 4 * 4 - internetHeaderLength);
            switch (Protocol)
            {
                case PacketType.Udp:
                    PayloadPacket = new UdpPacket(stream);
                    break;
                default:
                    PayloadPacket = new DummyPacket(stream);
                    break;
            }
        }
        
        internal IPv4Packet(IPAddress sourceAddress, IPAddress destinationAddress, IPacket payloadPacket, byte timeToLive = 64)
        {
            Version = 4;
            internetHeaderLength = 20;
            TypeOfService = 0;
            totalLength = (ushort)(internetHeaderLength + payloadPacket.Payload.Length);
            identification = (ushort)new Random().Next();
            flags = 0;
            fragmentOffset = 0;
            TimeToLive = timeToLive;
            switch (payloadPacket.GetType().Name)
            {
                case nameof(UdpPacket):
                    Protocol = PacketType.Udp;
                    break;
                default:
                    Protocol = (PacketType) 143;
                    break;
            }
            headerChecksum = ((Func<ushort>)(() =>
            {
                var sum = ((Version << 4 + internetHeaderLength) << 8 + TypeOfService) + totalLength + identification +
                          (flags << 13 | fragmentOffset) + (TimeToLive << 8 + (byte)Protocol) +
                          BitConverter.ToUInt16(sourceAddress.GetAddressBytes(), 0) +
                          BitConverter.ToUInt16(sourceAddress.GetAddressBytes(), 2) +
                          BitConverter.ToUInt16(destinationAddress.GetAddressBytes(), 0) +
                          BitConverter.ToUInt16(destinationAddress.GetAddressBytes(), 2);
                return (ushort)~(sum >> 16 + sum << 16 >> 16);
            }))();
            SourceAddress = sourceAddress;
            DestinationAddress = destinationAddress;
        }

        internal static IPv4Packet Parse(byte[] bytes) => new IPv4Packet(new MemoryStream(bytes));

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
            writer.Write((byte) (Version << 4 | internetHeaderLength));
            writer.Write(TypeOfService);
            writer.Write(totalLength);
            writer.Write(identification);
            writer.Write((ushort)(flags << 13 | fragmentOffset));
            writer.Write(TimeToLive);
            writer.Write((byte)Protocol);
            writer.Write(headerChecksum);
            writer.Write(SourceAddress.GetAddressBytes());
            writer.Write(DestinationAddress.GetAddressBytes());
            writer.Write(options);
            writer.Write(new byte[(internetHeaderLength + 3) / 4 * 4 - internetHeaderLength]);
            PayloadPacket.ToStream(streamTo);
        }
    }
}
