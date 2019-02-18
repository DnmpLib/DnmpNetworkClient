using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DNMPWindowsClient.PacketParser
{
    internal interface IPacket
    {
        byte[] Payload { get; }
        byte[] ToBytes();
        void ToStream(Stream streamTo);
    }
}
