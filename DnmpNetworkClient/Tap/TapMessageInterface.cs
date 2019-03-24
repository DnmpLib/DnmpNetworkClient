using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using DnmpLibrary.Interaction.MessageInterface;
using DnmpNetworkClient.Config;
using DnmpNetworkClient.Tap.PacketParser.Layer2;
using DnmpNetworkClient.Tap.PacketParser.Layer3;
using DnmpNetworkClient.Tap.PacketParser.Layer4;
using DnmpNetworkClient.Tap.PacketParser.Layer7;
using DnmpNetworkClient.Tap.Util;
using DnmpNetworkClient.Core;
using DnmpNetworkClient.OSDependent.Parts.Tap;
using NLog;

namespace DnmpNetworkClient.Tap
{
    internal class TapMessageInterface : MessageInterface
    {
        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        private ushort selfId;

        private readonly byte[] tapIpPrefix;
        private readonly byte[] tapMacPrefix;
        private readonly string dnsFormat;
        private readonly string selfName;

        private bool initialized;

        private CancellationTokenSource cancellationTokenSource;

        private const int ipMacPoolShift = 2;

        private Stream tapStream;

        private readonly ITapInterface tapInterface;

        public TapMessageInterface(TapConfig tapConfig, ITapInterface tapInterface)
        {
            this.tapInterface = tapInterface;
            tapIpPrefix = tapConfig.IpPrefix.Split('.').Select(byte.Parse).ToArray();
            tapMacPrefix = tapConfig.MacPrefix.Split(':').Select(x => Convert.ToByte(x, 16)).ToArray();
            dnsFormat = tapConfig.DnsFormat;
            selfName = tapConfig.SelfName;
            if (tapIpPrefix.Length < 2 || tapMacPrefix.Length < 4)
                throw new Exception("Wrong IP/MAC format for TAP");
        }

        public PhysicalAddress GetPhysicalAddressFromIp(IPAddress ip)
        {
            return ip.GetAddressBytes()[2] * 256 + ip.GetAddressBytes()[3] == selfId + ipMacPoolShift ?
                tapInterface.GetPhysicalAddress() : 
                new PhysicalAddress(new [] { tapMacPrefix[0], tapMacPrefix[1], tapMacPrefix[2], tapMacPrefix[3], ip.GetAddressBytes()[2], ip.GetAddressBytes()[3] });
        }

        public IPAddress GetIpFromPhysicalAddress(PhysicalAddress mac)
        {
            return Equals(mac, tapInterface.GetPhysicalAddress()) ? 
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
            return Equals(mac, tapInterface.GetPhysicalAddress())
                ? selfId
                : mac.GetAddressBytes()[4] * 256 + mac.GetAddressBytes()[5] - ipMacPoolShift;
        }

        public PhysicalAddress GetPhysicalAddressFromId(int id)
        {
            return id == selfId ?
                tapInterface.GetPhysicalAddress() :
                new PhysicalAddress(new[] { tapMacPrefix[0], tapMacPrefix[1], tapMacPrefix[2], tapMacPrefix[3], (byte)((id + ipMacPoolShift) / 256), (byte)((id + ipMacPoolShift) % 256) });
        }

        public override async void Initialize(ushort newSelfId)
        {
            if (initialized)
                return;

            selfId = newSelfId;
            tapStream = tapInterface.Open();
            cancellationTokenSource = new CancellationTokenSource();
            initialized = true;
            StartAsyncReadData(cancellationTokenSource.Token);
            await Task.Delay(0);
        }

        public void Stop()
        {
            if (!initialized)
                return;

            cancellationTokenSource.Cancel();
            tapInterface.Close();
            initialized = false;
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
                ipv4Packet.DestinationAddress = GetIpFromPhysicalAddress(tapInterface.GetPhysicalAddress());
                ethernetPacket.DestinationAddress = tapInterface.GetPhysicalAddress();
            }

            var packetData = ethernetPacket.ToBytes();

            await tapStream.WriteAsync(packetData, 0, packetData.Length);
            await Task.Delay(0);
        }

        public override ushort GetMaxClientCount()
        {
            return 0xFFFC;
        }

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
                                if (intId >= 0)
                                {
                                    if (HostExists((ushort) intId) || intId == selfId)
                                        await Send(p.Payload, (ushort) intId);
                                    continue;
                                }

                                var ipPacket = (IPv4Packet) p.PayloadPacket;

                                switch (intId)
                                {
                                    case -ipMacPoolShift:
                                        {
                                            if (ipPacket.PayloadPacket is UdpPacket udpPacket &&
                                                udpPacket.PayloadPacket is DhcpPacket dhcpPacket)
                                            {

                                                if (dhcpPacket.Op != 1)
                                                    continue;

                                                var dhcpMessageType =
                                                    dhcpPacket.Options.ContainsKey(53) &&
                                                    dhcpPacket.Options[53].Length > 0
                                                        ? dhcpPacket.Options[53][0]
                                                        : -1;
                                                DhcpPacket answerDhcpPacket;

                                                switch (dhcpMessageType)
                                                {
                                                    case 1: // DHCPDISCOVER
                                                        answerDhcpPacket = new DhcpPacket
                                                        {
                                                            Xid = dhcpPacket.Xid,
                                                            YourIpAddress = GetIpFromId(selfId),
                                                            ServerIpAddress = GetIpFromId(-1),
                                                            ClientHardwareAddress = dhcpPacket.ClientHardwareAddress,
                                                            Options =
                                                                new Dictionary<byte, byte[]>
                                                                {
                                                                    {53, new byte[] {2}},
                                                                    {1, new byte[] {255, 255, 0, 0}},
                                                                    {
                                                                        51,
                                                                        BitConverter.GetBytes(30 * 60).Reverse().ToArray()
                                                                    },
                                                                    {54, GetIpFromId(-1).GetAddressBytes()},
                                                                    {6, GetIpFromId(-1).GetAddressBytes()}
                                                                }
                                                        };
                                                        break;
                                                    case 3: // DHCPREQUEST
                                                        answerDhcpPacket = new DhcpPacket
                                                        {
                                                            Xid = dhcpPacket.Xid,
                                                            YourIpAddress = GetIpFromId(selfId),
                                                            ServerIpAddress = GetIpFromId(-1),
                                                            ClientHardwareAddress = dhcpPacket.ClientHardwareAddress,
                                                            Options =
                                                                new Dictionary<byte, byte[]>
                                                                {
                                                                    {53, new byte[] {5}},
                                                                    {1, new byte[] {255, 255, 0, 0}},
                                                                    {
                                                                        51,
                                                                        BitConverter.GetBytes(30 * 60).Reverse().ToArray()
                                                                    },
                                                                    {54, GetIpFromId(-1).GetAddressBytes()},
                                                                    {6, GetIpFromId(-1).GetAddressBytes()}
                                                                }
                                                        };
                                                        break;
                                                    default:
                                                        continue;
                                                }

                                                var answerIpV4Packet = new IPv4Packet(GetIpFromId(-1),
                                                    IPAddress.Broadcast);
                                                answerIpV4Packet.SetPayloadPacket(new UdpPacket(67, 68,
                                                    answerDhcpPacket, answerIpV4Packet));
                                                var answerEthernetPacket = new EthernetPacket(
                                                    GetPhysicalAddressFromId(-1),
                                                    p.SourceAddress, answerIpV4Packet, EthernetPacket.PacketType.IpV4);
                                                var answerData = answerEthernetPacket.ToBytes();
                                                await tapStream.WriteAsync(answerData, 0, answerData.Length,
                                                    cancellationToken);
                                                continue;
                                            }

                                            await Broadcast(p.Payload);
                                        }
                                        continue;
                                    case -1:
                                        {
                                            if (ipPacket.PayloadPacket is UdpPacket udpPacket &&
                                                udpPacket.PayloadPacket is DnsPacket dnsPacket)
                                            {
                                                if (dnsPacket.Queries.Count == 1 && dnsPacket.Queries[0].Type == 1 &&
                                                    dnsPacket.Queries[0].Class == 1 && DomainNameUtil.GetName(string.Join(".", dnsPacket.Queries[0].Labels), dnsFormat) != null)
                                                {
                                                    var name = DomainNameUtil.GetName(string.Join(".", dnsPacket.Queries[0].Labels), dnsFormat);
                                                    if (string.IsNullOrEmpty(name))
                                                        continue;
                                                    if (name == selfName)
                                                        await DnsReply(dnsPacket.TransactionId, dnsPacket.Queries[0].Labels, selfId, udpPacket.SourcePort);
                                                    else
                                                    {
                                                        var clientId = GetNodes().FirstOrDefault(x =>
                                                            x.GetDnmpNodeData().DomainName == name)?.Id;
                                                        if (clientId != null)
                                                            await DnsReply(dnsPacket.TransactionId, dnsPacket.Queries[0].Labels, clientId.Value, udpPacket.SourcePort);
                                                    }
                                                }
                                            }
                                        }
                                        continue;
                                }
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
                                if (!HostExists((ushort) targetId) && targetId != -1)
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

        private async Task DnsReply(ushort transactionId, List<string> labels, ushort id, ushort sourcePort)
        {

            var answerDnsPacket = new DnsPacket(transactionId, 0, 0, new List<DnsPacket.ResourceRecord>
                {
                    new DnsPacket.ResourceRecord
                    {
                        Class = 0x0001,
                        Type = 0x0001,
                        Labels = labels
                    }
                }, new List<DnsPacket.ResourceRecord>
                {
                    new DnsPacket.ResourceRecord
                    {
                        Class = 0x0001,
                        Type = 0x0001,
                        TTL = 30 * 60,
                        Labels = labels,
                        Data = GetIpFromId(id).GetAddressBytes()
                    }
                },
                new List<DnsPacket.ResourceRecord>(),
                new List<DnsPacket.ResourceRecord>(), DnsPacket.DnsFlags.IsAutorative | DnsPacket.DnsFlags.IsResponse);

            var answerIpV4Packet = new IPv4Packet(GetIpFromId(-1), GetIpFromId(selfId));
            answerIpV4Packet.SetPayloadPacket(new UdpPacket(53, sourcePort,
                answerDnsPacket, answerIpV4Packet));
            var answerEthernetPacket = new EthernetPacket(GetPhysicalAddressFromId(-1),
                GetPhysicalAddressFromId(selfId), answerIpV4Packet, EthernetPacket.PacketType.IpV4);
            var answerData = answerEthernetPacket.ToBytes();
            await tapStream.WriteAsync(answerData, 0, answerData.Length);
        }
    }
}
