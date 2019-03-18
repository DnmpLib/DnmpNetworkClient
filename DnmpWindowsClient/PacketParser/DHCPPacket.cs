using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using DnmpLibrary.Util.BigEndian;

namespace DnmpWindowsClient.PacketParser
{
    internal class DhcpPacket : IPacket
    {
        internal byte Op = 2;
        internal byte HardwareAddressType = 1;
        internal byte HardwareAddressLength = 6;
        internal byte Hops;
        internal uint Xid;
        internal ushort Secs;
        internal ushort Flags;
        internal IPAddress ClinetIpAddress = IPAddress.Any;
        internal IPAddress YourIpAddress = IPAddress.Any;
        internal IPAddress ServerIpAddress = IPAddress.Any;
        internal IPAddress RelayIpAddress = IPAddress.Any;
        internal PhysicalAddress ClientHardwareAddress = PhysicalAddress.None;
        internal string ServerHostName = "";
        internal string BootFileName = "";
        internal Dictionary<byte, byte[]> Options = new Dictionary<byte, byte[]>();

        internal DhcpPacket(Stream stream, int readAmount = int.MaxValue)
        {
            if (readAmount < 236) throw new InvalidPacketException();
            var reader = new BigEndianBinaryReader(stream);
            Op = reader.ReadByte();
            HardwareAddressType = reader.ReadByte();
            HardwareAddressLength = reader.ReadByte();
            Hops = reader.ReadByte();
            Xid = reader.ReadUInt32();
            Secs = reader.ReadUInt16();
            Flags = reader.ReadUInt16();
            ClinetIpAddress = new IPAddress(reader.ReadBytes(4));
            YourIpAddress = new IPAddress(reader.ReadBytes(4));
            ServerIpAddress = new IPAddress(reader.ReadBytes(4));
            RelayIpAddress = new IPAddress(reader.ReadBytes(4));
            ClientHardwareAddress = new PhysicalAddress(reader.ReadBytes(16).Take(HardwareAddressLength).ToArray());
            ServerHostName = Encoding.ASCII.GetString(reader.ReadBytes(64).TakeWhile(x => x != 0).ToArray());
            BootFileName = Encoding.ASCII.GetString(reader.ReadBytes(128).TakeWhile(x => x != 0).ToArray());
            readAmount -= 236;
            if (readAmount < 5) throw new InvalidPacketException();
            if (reader.ReadByte() != 99 || reader.ReadByte() != 130 || reader.ReadByte() != 83 ||
                reader.ReadByte() != 99) throw new InvalidPacketException("DHCP Magic cookie invalid");
            readAmount -= 4;
            Options = new Dictionary<byte, byte[]>();
            do
            {
                var opType = reader.ReadByte();
                readAmount--;
                if (opType == 0xFF)
                {
                    reader.ReadBytes(readAmount);
                    return;
                }
                if (readAmount < 1) throw new InvalidPacketException();
                var length = reader.ReadByte();
                readAmount--;
                if (readAmount < length) throw new InvalidPacketException();
                readAmount -= length;
                Options.Add(opType, reader.ReadBytes(length));
            } while (readAmount > 0);
            throw new InvalidPacketException();
        }

        internal DhcpPacket() { }

        internal static DhcpPacket Parse(byte[] bytes) => new DhcpPacket(new MemoryStream(bytes));

        public byte[] Payload => throw new InvalidOperationException();

        public byte[] ToBytes()
        {
            var stream = new MemoryStream();
            ToStream(stream);
            return stream.ToArray();
        }

        public void ToStream(Stream streamTo)
        {
            var writer = new BigEndianBinaryWriter(streamTo);
            writer.Write(Op);
            writer.Write(HardwareAddressType);
            writer.Write(HardwareAddressLength);
            writer.Write(Hops);
            writer.Write(Xid);
            writer.Write(Secs);
            writer.Write(Flags);
            writer.Write(ClinetIpAddress.GetAddressBytes());
            writer.Write(YourIpAddress.GetAddressBytes());
            writer.Write(ServerIpAddress.GetAddressBytes());
            writer.Write(RelayIpAddress.GetAddressBytes());
            var address = ClientHardwareAddress.GetAddressBytes();
            Array.Resize(ref address, 16);
            writer.Write(address);
            writer.Write(Encoding.ASCII.GetBytes(ServerHostName.PadRight(64, '\0')));
            writer.Write(Encoding.ASCII.GetBytes(BootFileName.PadRight(128, '\0')));
            writer.Write(new byte[] { 99, 130, 83, 99 });
            foreach (var option in Options)
            {
                writer.Write(option.Key);
                writer.Write((byte) option.Value.Length);
                writer.Write(option.Value);
            }
            writer.Write((byte) 255);
        }
    }
}
