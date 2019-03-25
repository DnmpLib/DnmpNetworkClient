using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Mono.Unix.Native;
using Mono.Unix;
using NLog;

namespace DnmpNetworkClient.OSDependent.Parts.Tap.Impl
{
    internal class UnixTapInterface : ITapInterface
    {
        [StructLayout(LayoutKind.Sequential, Size = 40)]
        private struct IfReq
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
            public readonly string Name;

            public short Flags;
        }

        private const int tapIff = 0x0002;
        private const int noPiIff = 0x1000;

        [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
        private static extern int IoCtl(int descriptor, uint request, ref IfReq ifreq);

        private IfReq currentInterfaceInfo;
        private Stream currentStream;

        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        public PhysicalAddress GetPhysicalAddress()
        {
            return new PhysicalAddress(new byte[6]);
        }

        public Stream Open()
        {
            var descriptor = Syscall.open("/dev/net/tun", OpenFlags.O_RDWR);
            if (descriptor < 0)
            {
                logger.Error($"/dev/net/tun open error: ret: {descriptor}; LastError: {Marshal.GetLastWin32Error()}");
                return null;
            }
            currentInterfaceInfo = new IfReq
            {
                Flags = tapIff | noPiIff
            };
            var ioctlRetCode = IoCtl(descriptor, 0x400454CA, ref currentInterfaceInfo);
            if (ioctlRetCode < 0)
            {
                logger.Error($"ioctl error: ret: {ioctlRetCode}; LastError: {Marshal.GetLastWin32Error()}");
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
