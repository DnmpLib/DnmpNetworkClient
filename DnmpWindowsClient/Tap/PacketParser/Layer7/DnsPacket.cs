using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DnmpLibrary.Util.BigEndian;

namespace DnmpWindowsClient.Tap.PacketParser.Layer7
{
    internal class DnsPacket : IPacket
    {
        internal struct ResourceRecord
        {
            internal List<string> Labels;
            internal ushort Type;
            internal ushort Class;
            internal uint TTL;
            internal byte[] Data;

            internal ResourceRecord(Stream stream, bool isQuery)
            {
                var reader = new BigEndianBinaryReader(stream);
                Labels = new List<string>();
                var pointer = -1L;
                for (; ; )
                {
                    var fragmentLn = reader.ReadByte();
                    if (fragmentLn >> 6 == 0b11)
                    {
                        pointer = reader.BaseStream.Position;
                        reader.BaseStream.Seek(fragmentLn * 255 + reader.ReadByte(), SeekOrigin.Begin);
                    }

                    if (fragmentLn == 0)
                    {
                        if (pointer >= 0)
                            reader.BaseStream.Seek(pointer, SeekOrigin.Begin);
                        break;
                    }
                    Labels.Add(Encoding.ASCII.GetString(reader.ReadBytes(fragmentLn)));
                }

                Type = reader.ReadUInt16();
                Class = reader.ReadUInt16();

                if (isQuery)
                {
                    TTL = 0;
                    Data = new byte[0];
                    return;
                }

                TTL = reader.ReadUInt32();
                var dataLength = reader.ReadUInt16();
                Data = reader.ReadBytes(dataLength);
            }

            internal void ToStream(Stream streamTo, bool isQuery)
            {
                var writer = new BigEndianBinaryWriter(streamTo);
                Labels.ForEach(part =>
                {
                    writer.Write((byte) part.Length);
                    writer.Write(Encoding.ASCII.GetBytes(part));
                });
                writer.Write((byte) 0);
                writer.Write(Type);
                writer.Write(Class);
                if (isQuery) return;
                writer.Write(TTL);
                writer.Write((ushort) Data.Length);
                writer.Write(Data);
            }
        }

        [Flags]
        internal enum DnsFlags : ushort
        {
            IsResponse = 0b1000000000000000,
            IsAutorative = 0b0000010000000000,
            IsTruncated = 0b0000001000000000,
            IsRecursionDesired = 0b0000000100000000,
            IsRecursionAvailable = 0b0000000010000000,
            IsIncorrect = 0b0000000001000000,
            IsAuthenticated = 0b0000000000100000
        }

        internal ushort TransactionId;
        internal DnsFlags Flags;
        internal byte OpCode;
        internal byte ReplyCode;
        internal List<ResourceRecord> Queries = new List<ResourceRecord>();
        internal List<ResourceRecord> Answers = new List<ResourceRecord>();
        internal List<ResourceRecord> Authorities = new List<ResourceRecord>();
        internal List<ResourceRecord> AdditionalRecords = new List<ResourceRecord>();

        internal DnsPacket(Stream stream, int readAmount = int.MaxValue)
        {
            var data = new byte[readAmount];
            stream.Read(data, 0, readAmount);
            var reader = new BigEndianBinaryReader(new MemoryStream(data));
            if (readAmount < 12) throw new InvalidPacketException();
            TransactionId = reader.ReadUInt16();
            var flags = reader.ReadUInt16();
            OpCode = (byte)((flags >> 11) & 0b1111);
            ReplyCode = (byte)(flags & 0b1111);
            Flags = (DnsFlags)(flags & 0b1000011111110000);
            var questions = reader.ReadUInt16();
            var answerRRs = reader.ReadUInt16();
            var authorityRRs = reader.ReadUInt16();
            var additionalRRs = reader.ReadUInt16();
            for (var query = 0; query < questions; query++)
                Queries.Add(new ResourceRecord(reader.BaseStream, true));
            for (var answer = 0; answer < answerRRs; answer++)
                Answers.Add(new ResourceRecord(reader.BaseStream, true));
            for (var authority = 0; authority < authorityRRs; authority++)
                Authorities.Add(new ResourceRecord(reader.BaseStream, true));
            for (var additional = 0; additional < additionalRRs; additional++)
                Answers.Add(new ResourceRecord(reader.BaseStream, true));
        }

        public DnsPacket(ushort transactionId, byte opCode, byte replyCode, List<ResourceRecord> queries, List<ResourceRecord> answers,
            List<ResourceRecord> authorities, List<ResourceRecord> additionalRecords, DnsFlags flags)
        {
            TransactionId = transactionId;
            Flags = flags;
            OpCode = opCode;
            ReplyCode = replyCode;
            Queries = queries ?? throw new ArgumentNullException(nameof(queries));
            Answers = answers ?? throw new ArgumentNullException(nameof(answers));
            Authorities = authorities ?? throw new ArgumentNullException(nameof(authorities));
            AdditionalRecords = additionalRecords ?? throw new ArgumentNullException(nameof(additionalRecords));
        }

        internal static DnsPacket Parse(byte[] data) => new DnsPacket(new MemoryStream(data), data.Length);

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
            writer.Write(TransactionId);
            writer.Write((ushort)((ushort)Flags | ReplyCode | (OpCode << 11)));
            writer.Write((ushort)Queries.Count);
            writer.Write((ushort)Answers.Count);
            writer.Write((ushort)Authorities.Count);
            writer.Write((ushort)AdditionalRecords.Count);
            Queries.ForEach(query => query.ToStream(writer.BaseStream, true));
            Answers.ForEach(answer => answer.ToStream(writer.BaseStream, false));
            Authorities.ForEach(authority => authority.ToStream(writer.BaseStream, false));
            AdditionalRecords.ForEach(additional => additional.ToStream(writer.BaseStream, false));
        }
    }
}
