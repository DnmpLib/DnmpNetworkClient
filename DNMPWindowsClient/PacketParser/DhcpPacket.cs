using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace DNMPWindowsClient.PacketParser
{
    internal class DhcpPacket : IPacket
    {
        internal byte Op;
        internal byte HardwareAddressType;
        internal byte HardwareAddressLength;
        internal byte Hops;
        internal uint Xid;
        internal ushort Secs;
        internal ushort Flags;
        internal IPAddress ClinetIpAddress;
        internal IPAddress YourIpAddress;
        internal IPAddress ServerIpAddress;
        internal IPAddress RelayIpAddress;
        internal PhysicalAddress ClientHardwareAddress;
        internal string ServerHostName;
        internal string BootFileName;
        internal byte[] Options;

        internal DhcpPacket(Stream stream, int readAmount = int.MaxValue)
        {
            if (readAmount < 236) throw new InvalidPacketException();
            var reader = new BinaryReader(stream);
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
            Options = reader.ReadBytes(readAmount - 236);
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
            var writer = new BinaryWriter(streamTo);
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
            Array.Resize(ref address, HardwareAddressLength);
            writer.Write(address);
            writer.Write(Encoding.ASCII.GetBytes(ServerHostName.PadRight(64, '\0')));
            writer.Write(Encoding.ASCII.GetBytes(BootFileName.PadRight(128, '\0')));
            writer.Write(Options);
        }
    }
}
