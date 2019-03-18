using System.IO;
using System.Net.NetworkInformation;
using DnmpLibrary.Util.BigEndian;

namespace DnmpWindowsClient.PacketParser
{
    internal class EthernetPacket : IPacket
    {
        internal enum PacketType : ushort
        {
            IpV4 = 0x0800,
            Arp = 0x0806
        }

        internal PhysicalAddress DestinationAddress;
        internal PhysicalAddress SourceAddress;
        internal PacketType Type;
        internal IPacket PayloadPacket;

        internal EthernetPacket(Stream packetStream, int readAmount = int.MaxValue)
        {
            if (readAmount < 14) throw new InvalidPacketException();
            var reader = new BigEndianBinaryReader(packetStream);
            DestinationAddress = new PhysicalAddress(reader.ReadBytes(6));
            SourceAddress = new PhysicalAddress(reader.ReadBytes(6));
            Type = (PacketType) reader.ReadUInt16();
            switch (Type)
            {
                case PacketType.Arp:
                    PayloadPacket = new ArpPacket(packetStream, readAmount - 14);
                    break;
                case PacketType.IpV4:
                    PayloadPacket = new IPv4Packet(packetStream, readAmount - 14);
                    break;
                default:
                    PayloadPacket = new DummyPacket(packetStream, readAmount - 14);
                    break;
            }
        }

        internal EthernetPacket(PhysicalAddress sourceAddress, PhysicalAddress destinationAddress, IPacket payloadPacket, PacketType packetType)
        {
            SourceAddress = sourceAddress;
            DestinationAddress = destinationAddress;
            PayloadPacket = payloadPacket;
            Type = packetType;
        }

        internal static EthernetPacket Parse(byte[] bytes) => new EthernetPacket(new MemoryStream(bytes), bytes.Length);


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
            writer.Write(DestinationAddress.GetAddressBytes());
            writer.Write(SourceAddress.GetAddressBytes());
            writer.Write((ushort) Type);
            PayloadPacket.ToStream(streamTo);
        }
    }
}
