using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace DNMPWindowsClient.PacketParser
{
    internal sealed class DummyPacket : IPacket
    {
        public byte[] Payload { get; private set; }
        internal DummyPacket(Stream data)
        {
            Payload = new byte[data.Length - data.Position];
            data.Read(Payload, 0, (int)(data.Length - data.Position));
        }
        internal DummyPacket(byte[] payload)
        {
            Payload = payload;
        }

        public byte[] ToBytes()
        {
            return Payload;
        }

        public void ToStream(Stream streamTo)
        {
            streamTo.Write(Payload, 0, Payload.Length);
        }
    }
}
