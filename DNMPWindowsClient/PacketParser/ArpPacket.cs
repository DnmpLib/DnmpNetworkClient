using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DNMPWindowsClient.PacketParser
{
    internal class ArpPacket : IPacket
    {
        internal enum OperationType : ushort
        {
            Request = 0x0001,
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

        internal ArpPacket(Stream packetStream)
        {
            var reader = new BinaryReader(packetStream);
            HardwareType = reader.ReadUInt16();
            ProtocolType = reader.ReadUInt16();
            HardwareLength = reader.ReadByte();
            ProtocolLength = reader.ReadByte();
            Operation = (OperationType) reader.ReadUInt16();
            SenderHardwareAddress = reader.ReadBytes(HardwareLength);
            SenderProtocolAddress = reader.ReadBytes(ProtocolLength);
            TargetHardwareAddress = reader.ReadBytes(HardwareLength);
            TargetProtocolAddress = reader.ReadBytes(ProtocolLength);
        }

        internal ArpPacket()
        {

        }

        public byte[] ToBytes()
        {
            var stream = new MemoryStream();
            ToStream(stream);
            return stream.ToArray();
        }

        public void ToStream(Stream streamTo)
        {
            var writer = new BinaryWriter(streamTo);
            writer.Write(HardwareType);
            writer.Write(ProtocolType);
            writer.Write(HardwareLength);
            writer.Write(ProtocolLength);
            writer.Write((ushort) Operation);
            writer.Write(SenderHardwareAddress);
            writer.Write(SenderProtocolAddress);
            writer.Write(TargetHardwareAddress);
            writer.Write(TargetProtocolAddress);
        }
    }
}
