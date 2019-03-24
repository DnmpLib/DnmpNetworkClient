using System;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Mono.Unix;
using NLog;

namespace DnmpNetworkClient.OSDependent.Parts.Tap.Impl
{
    internal class UnixTapInterface : ITapInterface
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct IfReq
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
            public readonly string Name;

            private readonly SockAddr Addr;
            private readonly SockAddr DstAddr;
            private readonly SockAddr BroadAddr;
            private readonly SockAddr NetMask;
            public readonly SockAddr HwAddr;

            private readonly short Flags;
            private readonly int IValue;
            private readonly int Mtu;

            private readonly IfMap Map;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
            private readonly string Slave;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
            private readonly string NewName;
            private readonly string Data;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IfMap
        {
            private readonly ulong MemStart;
            private readonly ulong MemEnd;
            private readonly ushort BaseAddr;
            private readonly byte Irq;
            private readonly byte Dma;
            private readonly byte Port;
        };

        [StructLayout(LayoutKind.Sequential)]
        private struct SockAddr
        {
            private readonly ushort Family;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 14)]
            public readonly byte[] Data;
        }

        [DllImport("libtunbridge.so", EntryPoint = "tun_alloc")]
        private static extern int TunAlloc([Out] IfReq interfaceInfo, int flags);

        private const int tapIff = 0x0002;
        private const int noPiIff = 0x1000;

        private IfReq currentInterfaceInfo = new IfReq();
        private Stream currentStream;

        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        public PhysicalAddress GetPhysicalAddress()
        {
            return new PhysicalAddress(currentInterfaceInfo.HwAddr.Data);
        }

        public Stream Open()
        {
            var descriptor = TunAlloc(currentInterfaceInfo, tapIff | noPiIff);
            if (descriptor < 0)
            {
                switch (descriptor)
                {
                    case -1:
                        logger.Error("/dev/net/tun open error");
                        break;
                    case -2:
                        logger.Error("IOCTL error");
                        break;
                    default:
                        logger.Error("Generic error in initializing TAP");
                        break;
                }
                return null;
            }

            logger.Info($"Opened TAP device with FD #{descriptor} and name '{currentInterfaceInfo.Name}'");
            return currentStream = new UnixStream(descriptor);
        }

        public void Close()
        {
            currentStream.Close();
            currentStream = null;
        }
    }
}
