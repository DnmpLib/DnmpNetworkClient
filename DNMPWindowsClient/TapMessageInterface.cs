using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System.Net.NetworkInformation;
using System.Security.Principal;
using System.Threading;
using DNMPLibrary.Interaction.MessageInterface;
using DNMPWindowsClient.PacketParser;
using NLog;

namespace DNMPWindowsClient
{
    public class TapMessageInterface : MessageInterface
    {
        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        private ushort selfId;

        private NetworkInterface tapNetworkInterface;

        private readonly byte[] tapIpPrefix;
        private readonly byte[] tapMacPrefix;

        private bool initialized;

        private CancellationTokenSource cancellationTokenSource;

        private const int ipMacPoolShift = 2;

        public TapMessageInterface(string tapIpPrefixString, string tapMacPrefixString)
        {
            tapIpPrefix = tapIpPrefixString.Split('.').Select(byte.Parse).ToArray();
            tapMacPrefix = tapMacPrefixString.Split(':').Select(x => Convert.ToByte(x, 16)).ToArray();
            if (tapIpPrefix.Length < 2 || tapMacPrefix.Length < 4)
                throw new Exception("Wrong IP/MAC format for TAP");
        }

        public PhysicalAddress GetPhysicalAddressFromIp(IPAddress ip)
        {
            return ip.GetAddressBytes()[2] * 256 + ip.GetAddressBytes()[3] == selfId + ipMacPoolShift ? 
                tapNetworkInterface.GetPhysicalAddress() : 
                new PhysicalAddress(new [] { tapMacPrefix[0], tapMacPrefix[1], tapMacPrefix[2], tapMacPrefix[3], ip.GetAddressBytes()[2], ip.GetAddressBytes()[3] });
        }

        public IPAddress GetIpFromPhysicalAddress(PhysicalAddress mac)
        {
            return Equals(mac, tapNetworkInterface.GetPhysicalAddress()) ? 
                new IPAddress(new [] { tapIpPrefix[0], tapIpPrefix[1], (byte)((selfId + ipMacPoolShift) / 256), (byte)((selfId + ipMacPoolShift) % 256) }) : 
                new IPAddress(new [] { tapIpPrefix[0], tapIpPrefix[1], mac.GetAddressBytes()[4], mac.GetAddressBytes()[5] });
        }

        public IPAddress GetIpFromId(int id)
        {
            return new IPAddress(new[] { tapIpPrefix[0], tapIpPrefix[1], (byte)((id + ipMacPoolShift) / 256), (byte)((id + ipMacPoolShift) % 256) });
        }

        public int GetIdFromPhysicalAddress(PhysicalAddress mac)
        {
            if (!mac.GetAddressBytes().Take(4).SequenceEqual(tapMacPrefix))
                return -ipMacPoolShift;
            return Equals(mac, tapNetworkInterface.GetPhysicalAddress())
                ? selfId
                : mac.GetAddressBytes()[4] * 256 + mac.GetAddressBytes()[5] - ipMacPoolShift;
        }

        public PhysicalAddress GetPhysicalAddressFromId(int id)
        {
            return id == selfId ?
                tapNetworkInterface.GetPhysicalAddress() :
                new PhysicalAddress(new[] { tapMacPrefix[0], tapMacPrefix[1], tapMacPrefix[2], tapMacPrefix[3], (byte)((id + ipMacPoolShift) / 256), (byte)((id + ipMacPoolShift) % 256) });
        }

        public override async void Initialize(ushort newSelfId)
        {
            if (initialized)
                return;
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
                {
                    logger.Error(
                        $"Current user ({identity.Name}) has no administrator privileges! TAP-Windows will not be initialized!");
                    return;
                }
            }


            selfId = newSelfId;
            var deviceGuid = GetDeviceGuid();
            if (deviceGuid == null)
            {
                logger.Error("Can't find TAP adapter on Windows");
                return;
            }
            tapNetworkInterface = NetworkInterface.GetAllNetworkInterfaces().First(x => x.Name == HumanName(deviceGuid));
            if (!ClearArpTable())
            {
                logger.Error("Error while clearing ARP table");
                return;
            }
            if (!SetDhcpMethod(HumanName(deviceGuid)))
                logger.Warn("netsh returned 1 while setting DHCP");
            logger.Info($"TAP interface started");
            var devicePointer = CreateFile(usermodeDeviceSpace + deviceGuid + ".tap", FileAccess.ReadWrite, FileShare.ReadWrite, 0, FileMode.Open, systemFileAttribute | noBufferingFileFlag | writeThroughFileFlag | overlappedFileFlag, IntPtr.Zero);
            var statusPointer = Marshal.AllocHGlobal(4);
            Marshal.WriteInt32(statusPointer, 1);
            DeviceIoControl(devicePointer, TapControlCode(6, bufferedMethod) /* TAP_IOCTL_SET_MEDIA_STATUS */, statusPointer, 4, statusPointer, 4, out var _, IntPtr.Zero);
            tapStream = new FileStream(new SafeFileHandle(devicePointer, true), FileAccess.ReadWrite, 1, true);
            cancellationTokenSource = new CancellationTokenSource();
            initialized = true;
            StartAsyncReadData(cancellationTokenSource.Token);
            await Task.Delay(0);
        }

        public void Stop()
        {
            if (!initialized)
                return;

            initialized = false;
            cancellationTokenSource.Cancel();
            tapStream.Close();
        }

        public override async void PacketReceived(object sender, DataMessageEventArgs eventArgs)
        {
            if (!initialized)
                return;

            var ipv4Packet = IPv4Packet.Parse(eventArgs.Data);
            ipv4Packet.SourceAddress = GetIpFromPhysicalAddress(GetPhysicalAddressFromId(eventArgs.SourceId));
            ipv4Packet.DestinationAddress = IPAddress.Broadcast;

            var ethernetPacket = new EthernetPacket(GetPhysicalAddressFromId(eventArgs.SourceId),
                new PhysicalAddress(new byte[] {0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF}), ipv4Packet, EthernetPacket.PacketType.IpV4);

            if (!eventArgs.IsBroadcast)
            {
                ipv4Packet.DestinationAddress = GetIpFromPhysicalAddress(tapNetworkInterface.GetPhysicalAddress());
                ethernetPacket.DestinationAddress = tapNetworkInterface.GetPhysicalAddress();
            }

            var packetData = ethernetPacket.ToBytes();

            await tapStream.WriteAsync(packetData, 0, packetData.Length);
            await Task.Delay(0);
        }

        public override ushort GetMaxClientCount()
        {
            return 0xFFFC;
        }

        private const string usermodeDeviceSpace = "\\\\.\\Global\\";
        private const string networkDevicesClass = "{4D36E972-E325-11CE-BFC1-08002BE10318}";

        public async void StartAsyncReadData(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var buffer = new byte[4096];
                    var readBytes = await tapStream.ReadAsync(buffer, 0, 4096, cancellationToken);
                    if (readBytes <= 0)
                        continue;
                    var p = EthernetPacket.Parse(buffer.Take(readBytes).ToArray());
                    if (p.DestinationAddress.GetAddressBytes().Take(3).SequenceEqual(new byte[] {0x01, 0x00, 0x5E}))
                        continue;
                    // ReSharper disable once SwitchStatementMissingSomeCases
                    switch (p.Type)
                    {
                        case EthernetPacket.PacketType.IpV4:
                            {
                                var intId = GetIdFromPhysicalAddress(p.DestinationAddress);
                                if (intId < 0)
                                {
                                    switch (intId)
                                    {
                                        case -ipMacPoolShift:
                                            continue;
                                        case -1:
                                            //TODO DNS
                                            break;
                                    }
                                }

                                var id = (ushort) intId;
                                if (id == 0xFFFE)
                                {
                                    if (p.PayloadPacket is UdpPacket udpPacket &&
                                        udpPacket.PayloadPacket is DHCPPacket dhcpPacket)
                                    {
                                        logger.Debug("Got DHCP packet");

                                        if (dhcpPacket.Op != 1)
                                            continue;
                                        logger.Debug($"OP: {dhcpPacket.Op}");

                                        var dhcpMessageType =
                                            dhcpPacket.Options.ContainsKey(53) && dhcpPacket.Options[53].Length > 0
                                                ? dhcpPacket.Options[53][0]
                                                : -1;
                                        logger.Debug($"DHCPMessageType: {dhcpMessageType}");
                                        DHCPPacket answerDhcpPacket;

                                        switch (dhcpMessageType)
                                        {
                                            case 1: // DHCPDISCOVER
                                                answerDhcpPacket = new DHCPPacket
                                                {
                                                    Xid = dhcpPacket.Xid,
                                                    YourIpAddress = GetIpFromId(selfId),
                                                    ServerIpAddress = GetIpFromId(-1),
                                                    ClientHardwareAddress = dhcpPacket.ClientHardwareAddress,
                                                    Options = new System.Collections.Generic.Dictionary<byte, byte[]>
                                                    {
                                                        { 53, new byte[] { 2 } },
                                                        { 1, new byte[] { 255, 255, 0, 0 } },
                                                        { 51, BitConverter.GetBytes(30 * 60).Reverse().ToArray() },
                                                        { 54, GetIpFromId(-1).GetAddressBytes() },
                                                        { 6, GetIpFromId(-1).GetAddressBytes() }
                                                    }
                                                };
                                                break;
                                            case 3: // DHCPREQUEST
                                                answerDhcpPacket = new DHCPPacket
                                                {
                                                    Xid = dhcpPacket.Xid,
                                                    YourIpAddress = GetIpFromId(selfId),
                                                    ServerIpAddress = GetIpFromId(-1),
                                                    ClientHardwareAddress = dhcpPacket.ClientHardwareAddress,
                                                    Options = new System.Collections.Generic.Dictionary<byte, byte[]>
                                                    {
                                                        { 53, new byte[] { 5 } },
                                                        { 1, new byte[] { 255, 255, 0, 0 } },
                                                        { 51, BitConverter.GetBytes(30 * 60).Reverse().ToArray() },
                                                        { 54, GetIpFromId(-1).GetAddressBytes() },
                                                        { 6, GetIpFromId(-1).GetAddressBytes() }
                                                    }
                                                };
                                                break;
                                            default:
                                                continue;
                                        }

                                        var answerIpV4Packet = new IPv4Packet(GetIpFromId(-1), IPAddress.Broadcast);
                                        answerIpV4Packet.SetPayloadPacket(new UdpPacket(67, 68, answerDhcpPacket, answerIpV4Packet));
                                        var answerEthernetPacket = new EthernetPacket(GetPhysicalAddressFromId(-1),
                                            p.SourceAddress, answerIpV4Packet, EthernetPacket.PacketType.IpV4);
                                        var answerData = answerEthernetPacket.ToBytes();
                                        await tapStream.WriteAsync(answerData, 0, answerData.Length, cancellationToken);
                                        continue;
                                    }

                                    await Broadcast(p.Payload);
                                }

                                if (HostExists(id) || id == selfId)
                                    await Send(p.Payload, id);
                            }
                            break;
                        case EthernetPacket.PacketType.Arp:
                            {
                                var arpPacket = (ArpPacket) p.PayloadPacket;
                                var targetIp = new IPAddress(arpPacket.TargetProtocolAddress);
                                if (!targetIp.GetAddressBytes().Take(2).SequenceEqual(tapIpPrefix))
                                    continue;
                                var targetId = GetIdFromPhysicalAddress(GetPhysicalAddressFromIp(targetIp));
                                if (targetId == -ipMacPoolShift)
                                    continue;
                                if (!HostExists((ushort) targetId))
                                    break;
                                var answerArpPacket = new ArpPacket
                                {
                                    TargetHardwareAddress = arpPacket.SenderHardwareAddress,
                                    TargetProtocolAddress = arpPacket.SenderProtocolAddress,
                                    SenderHardwareAddress = GetPhysicalAddressFromIp(targetIp).GetAddressBytes(),
                                    SenderProtocolAddress = arpPacket.TargetProtocolAddress,
                                    Operation = ArpPacket.OperationType.Response,
                                    HardwareType = 0x0001,
                                    ProtocolType = 0x0800
                                };
                                var answerEthernetPacket = new EthernetPacket(GetPhysicalAddressFromIp(targetIp),
                                    new PhysicalAddress(arpPacket.SenderHardwareAddress), answerArpPacket,
                                    EthernetPacket.PacketType.Arp);
                                var answerData = answerEthernetPacket.ToBytes();
                                await tapStream.WriteAsync(answerData, 0, answerData.Length, cancellationToken);
                            }
                            break;
                        default:
                            continue;
                    }
                }
                catch (TaskCanceledException)
                {
                    return;
                }
                catch (Exception e)
                {
                    logger.Error(e, "Exception in processing packet from TAP-Windows");
                }
            }
        }

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

        private static FileStream tapStream;

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
