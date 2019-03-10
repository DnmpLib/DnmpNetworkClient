using System.IO;
using System.Net.NetworkInformation;

namespace DNMPWindowsClient.PacketParser
{
    internal class EthernetPacket : IPacket
    {
        internal enum PacketType : ushort
        {
            IpV4 = 0x0008,
            Arp = 0x0608
        }

        internal PhysicalAddress DestinationAddress;
        internal PhysicalAddress SourceAddress;
        internal PacketType Type;
        internal IPacket PayloadPacket;

        internal EthernetPacket(Stream packetStream)
        {
            var reader = new BinaryReader(packetStream);
            DestinationAddress = new PhysicalAddress(reader.ReadBytes(6));
            SourceAddress = new PhysicalAddress(reader.ReadBytes(6));
            Type = (PacketType) reader.ReadUInt16();
            switch (Type)
            {
                case PacketType.Arp:
                    PayloadPacket = new ArpPacket(packetStream);
                    break;
                case PacketType.IpV4:
                    PayloadPacket = new IPv4Packet(packetStream);
                    break;
                default:
                    PayloadPacket = new DummyPacket(packetStream);
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

        internal static EthernetPacket Parse(byte[] bytes) => new EthernetPacket(new MemoryStream(bytes));


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
            writer.Write(DestinationAddress.GetAddressBytes());
            writer.Write(SourceAddress.GetAddressBytes());
            writer.Write((ushort) Type);
            PayloadPacket.ToStream(streamTo);
        }
    }
}
