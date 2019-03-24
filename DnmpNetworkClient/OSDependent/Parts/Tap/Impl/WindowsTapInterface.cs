using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using NLog;

namespace DnmpNetworkClient.OSDependant.Parts.Tap.Impl
{
    internal class WindowsTapInterface : ITapInterface
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private PhysicalAddress currentPhysicalAddress;
        private Stream currentStream;

        public PhysicalAddress GetPhysicalAddress()
        {
            return currentPhysicalAddress;
        }

        public Stream Open()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
                {
                    logger.Error(
                        $"Current user ({identity.Name}) has no administrator privileges! TAP-Windows will not be initialized!");
                    return null;
                }
            }

            var deviceGuid = GetDeviceGuid();
            if (deviceGuid == null)
            {
                logger.Error("Can't find TAP adapter on Windows");
                return null;
            }
            currentPhysicalAddress = NetworkInterface.GetAllNetworkInterfaces().First(x => x.Name == HumanName(deviceGuid)).GetPhysicalAddress();
            if (!ClearArpTable())
            {
                logger.Error("Error while clearing ARP table");
                return null;
            }
            if (!ClearDnsCache())
            {
                logger.Error("Error while clearing DNS cache");
                return null;
            }
            if (!SetDhcpMethod(HumanName(deviceGuid)))
                logger.Warn("netsh returned 1 while setting DHCP");
            logger.Info("TAP interface started");
            var devicePointer = CreateFile(usermodeDeviceSpace + deviceGuid + ".tap", FileAccess.ReadWrite, FileShare.ReadWrite, 0, FileMode.Open, systemFileAttribute | noBufferingFileFlag | writeThroughFileFlag | overlappedFileFlag, IntPtr.Zero);
            var statusPointer = Marshal.AllocHGlobal(4);
            Marshal.WriteInt32(statusPointer, 1);
            DeviceIoControl(devicePointer, TapControlCode(6, bufferedMethod) /* TAP_IOCTL_SET_MEDIA_STATUS */, statusPointer, 4, statusPointer, 4, out var _, IntPtr.Zero);
            return currentStream = new FileStream(new SafeFileHandle(devicePointer, true), FileAccess.ReadWrite, 1, true);
        }

        public void Close()
        {
            currentStream.Close();
            currentPhysicalAddress = null;
        }

        private const string usermodeDeviceSpace = "\\\\.\\Global\\";
        private const string networkDevicesClass = "{4D36E972-E325-11CE-BFC1-08002BE10318}";

        private static string GetDeviceGuid()
        {
            var registryKeyAdapters = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Control\\Class\\" + networkDevicesClass, true);
            if (registryKeyAdapters == null)
                throw new Exception("Can't open class key");
            var keyNames = registryKeyAdapters.GetSubKeyNames();
            var devGuid = default(string);
            foreach (var x in keyNames)
            {
                if (x == "Properties")
                    continue;
                var registryKeyAdapter = registryKeyAdapters.OpenSubKey(x);
                if (registryKeyAdapter == null)
                    throw new Exception("Can't open adapter key");
                var id = registryKeyAdapter.GetValue("ComponentId");
                if (id != null && id.ToString() == "tap0901")
                    devGuid = registryKeyAdapter.GetValue("NetCfgInstanceId").ToString();
            }
            return devGuid;
        }

        private static string HumanName(string guid)
        {
            if (guid == default(string))
                throw new Exception("Device not found");
            var registryKeyConnection = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Control\\Network\\" + networkDevicesClass + "\\" + guid + "\\Connection", true);
            if (registryKeyConnection == null)
                throw new Exception("Can't open connection key");
            var id = registryKeyConnection.GetValue("Name");
            return id?.ToString() ?? "Device not found";
        }

        private static uint ControlCode(uint deviceType, uint function, uint method, uint access)
        {
            return (deviceType << 16) | (access << 14) | (function << 2) | method;
        }

        private static uint TapControlCode(uint request, uint method)
        {
            return ControlCode(unknownFileDevice, request, method, anyAccessFile);
        }

        private const uint bufferedMethod = 0;
        private const uint anyAccessFile = 0;
        private const uint unknownFileDevice = 0x00000022;

        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr CreateFile(
            string filename,
            [MarshalAs(UnmanagedType.U4)]FileAccess fileaccess,
            [MarshalAs(UnmanagedType.U4)]FileShare fileshare,
            int securityattributes,
            [MarshalAs(UnmanagedType.U4)]FileMode creationdisposition,
            uint flags,
            IntPtr template);

        private const uint systemFileAttribute = 0x4;
        private const uint overlappedFileFlag = 0x40000000;
        private const uint noBufferingFileFlag = 0x20000000;
        private const uint writeThroughFileFlag = 0x80000000;

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool DeviceIoControl(IntPtr hDevice, uint dwIoControlCode,
            IntPtr lpInBuffer, uint nInBufferSize,
            IntPtr lpOutBuffer, uint nOutBufferSize,
            out int lpBytesReturned, IntPtr lpOverlapped);

        private static bool SetDhcpMethod(string adapter)
        {
            var netshProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    Arguments = $"int ip set address \"{adapter}\" dhcp",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            netshProcess.Start();
            netshProcess.WaitForExit();
            return netshProcess.ExitCode == 0;
        }

        private static bool ClearDnsCache()
        {
            var arpProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ipconfig",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    Arguments = "/flushdns",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            arpProcess.Start();
            arpProcess.WaitForExit();
            return arpProcess.ExitCode == 0;
        }

        private static bool ClearArpTable()
        {
            var arpProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "arp",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    Arguments = "-d *",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            arpProcess.Start();
            arpProcess.WaitForExit();
            return arpProcess.ExitCode == 0;
        }
    }
}
