using System.IO;
using DnmpLibrary.Util.BigEndian;

namespace DnmpWindowsClient.Tap.PacketParser.Layer3
{
    internal class ArpPacket : IPacket
    {
        internal enum OperationType : ushort
        {
            Response = 0x0002
        }

        internal ushort HardwareType;
        internal ushort ProtocolType;
        internal byte HardwareLength;
        internal byte ProtocolLength;
        internal OperationType Operation;
        internal byte[] SenderHardwareAddress;
        internal byte[] SenderProtocolAddress;
        internal byte[] TargetHardwareAddress;
        internal byte[] TargetProtocolAddress;

        internal ArpPacket(Stream packetStream, int readAmount = int.MaxValue)
        {
            if (readAmount < 8) throw new InvalidPacketException();
            var reader = new BigEndianBinaryReader(packetStream);
            HardwareType = reader.ReadUInt16();
            ProtocolType = reader.ReadUInt16();
            HardwareLength = reader.ReadByte();
            ProtocolLength = reader.ReadByte();
            Operation = (OperationType) reader.ReadUInt16();
            if (readAmount < (HardwareLength + ProtocolLength) * 2 + 8) throw new InvalidPacketException();
            SenderHardwareAddress = reader.ReadBytes(HardwareLength);
            SenderProtocolAddress = reader.ReadBytes(ProtocolLength);
            TargetHardwareAddress = reader.ReadBytes(HardwareLength);
            TargetProtocolAddress = reader.ReadBytes(ProtocolLength);
            reader.ReadBytes(readAmount - (HardwareLength + ProtocolLength) * 2 + 8);
        }

        internal ArpPacket()
        {

        }

        public byte[] Payload => new byte[0];

        public byte[] ToBytes()
        {
            var stream = new MemoryStream();
            ToStream(stream);
            return stream.ToArray();
        }

        public void ToStream(Stream streamTo)
        {
            var writer = new BigEndianBinaryWriter(streamTo);
            writer.Write(HardwareType);
            writer.Write(ProtocolType);
            writer.Write((byte)SenderHardwareAddress.Length);
            writer.Write((byte)SenderProtocolAddress.Length);
            writer.Write((ushort) Operation);
            writer.Write(SenderHardwareAddress);
            writer.Write(SenderProtocolAddress);
            writer.Write(TargetHardwareAddress);
            writer.Write(TargetProtocolAddress);
        }
    }
}
