using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using DnmpLibrary.Client;
using DnmpLibrary.Interaction.Protocol.EndPointImpl;
using DnmpLibrary.Security.Cryptography.Symmetric.Impl;
using DnmpNetworkClient.Config;
using DnmpNetworkClient.Core.SubServers;
using DnmpNetworkClient.OSDependent;
using DnmpNetworkClient.Tap;
using DnmpNetworkClient.Util;
using Newtonsoft.Json;

namespace DnmpNetworkClient.Core
{
    internal class MainClient : IDisposable
    {
        public readonly string ConfigFile;

        public MainConfig Config { get; }

        public NetworkManager NetworkManager { get; }

        public DnmpClient DnmpClient { get; }

        public TapMessageInterface TapMessageInterface { get; }

        public ClientWebSocketServer WebSocketServer { get; }

        public ClientHttpServer HttpServer { get; }

        public Guid CurrentNetworkId { get; private set; }

        private static readonly DnmpNodeData selfNodeData = new DnmpNodeData();

        public volatile bool Running;

        public MainClient(string configFile, IDependent dependent)
        {
            ConfigFile = configFile;

            Config = new MainConfig();
            if (File.Exists(ConfigFile))
                Config = JsonConvert.DeserializeObject<MainConfig>(File.ReadAllText(ConfigFile));
            File.WriteAllText(ConfigFile, JsonConvert.SerializeObject(Config));
            selfNodeData.DomainName = Config.TapConfig.SelfName;

            NetworkManager = new NetworkManager(Config.NetworksSaveConfig);
            TapMessageInterface = new TapMessageInterface(Config.TapConfig, dependent.GetTapInerface());
            DnmpClient = new DnmpClient(TapMessageInterface, new OsUdpProtocol(Config.GeneralConfig.ReceiveBufferSize, Config.GeneralConfig.SendBufferSize),
                Config.ClientConfig);
            DnmpClient.OnDisconnected += () => CurrentNetworkId = Guid.Empty;
            DnmpClient.OnConnectionTimeout += () => CurrentNetworkId = Guid.Empty;
            WebSocketServer = new ClientWebSocketServer(this);
            HttpServer = new ClientHttpServer(this);
        }

        private static readonly Random random = new Random();

        private static IEnumerable<IPAddress> GetTraceRoute(IPAddress ipAddress, int ttl)
        {
            var pinger = new Ping();
            const int timeout = 600;
            var buffer = new byte[32];
            var result = new HashSet<IPAddress>();
            for (var i = 1; i < ttl; i++)
            {
                random.NextBytes(buffer);
                var reply = pinger.Send(ipAddress, timeout, buffer, new PingOptions(i, true));
                if (reply == null)
                    break;
                switch (reply.Status)
                {
                    case IPStatus.Success:
                        result.Add(reply.Address);
                        break;
                    case IPStatus.TtlExpired:
                        result.Add(reply.Address);
                        break;
                    case IPStatus.TimedOut:
                        break;
                    case IPStatus.DestinationNetworkUnreachable:
                    case IPStatus.DestinationHostUnreachable:
                    case IPStatus.DestinationProtocolUnreachable:
                    case IPStatus.DestinationPortUnreachable:
                    case IPStatus.NoResources:
                    case IPStatus.BadOption:
                    case IPStatus.HardwareError:
                    case IPStatus.PacketTooBig:
                    case IPStatus.BadRoute:
                    case IPStatus.TtlReassemblyTimeExceeded:
                    case IPStatus.ParameterProblem:
                    case IPStatus.SourceQuench:
                    case IPStatus.BadDestination:
                    case IPStatus.DestinationUnreachable:
                    case IPStatus.TimeExceeded:
                    case IPStatus.BadHeader:
                    case IPStatus.UnrecognizedNextHeader:
                    case IPStatus.IcmpError:
                    case IPStatus.DestinationScopeMismatch:
                    case IPStatus.Unknown:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                if (reply.Status == IPStatus.TimedOut || reply.Status == IPStatus.Success)
                    break;
            }

            return result;
        }

        public void Connect(Guid networkId, int sourcePort, bool startAsFirst, IPAddress publicIp, bool useUpnp = true, bool useStun = true)
        {
            var networkConnectData = NetworkManager.SavedNetworks[networkId].GetConnectionData();

            if (useUpnp)
                PortMapperUtils.TryMapPort(sourcePort, Config.StunConfig.PortMappingTimeout).Wait();

            var endPoints = new HashSet<IPEndPoint>();

            if (startAsFirst)
            {
                IPEndPoint stunnedEndPoint;

                if (useStun)
                {
                    try
                    {
                        stunnedEndPoint = PortMapperUtils.GetStunnedIpEndPoint(sourcePort,
                            Config.StunConfig.Host,
                            Config.StunConfig.Port);
                    }
                    catch (Exception)
                    {
                        throw new ClientException("stun-error");
                    }

                    if (stunnedEndPoint == null)
                    {
                        throw new ClientException("stun-error");
                    }
                }
                else
                {
                    stunnedEndPoint = new IPEndPoint(publicIp, sourcePort);
                }

                foreach (var ip in GetTraceRoute(stunnedEndPoint.Address, 32))
                {
                    endPoints.Add(new IPEndPoint(ip, stunnedEndPoint.Port));
                    endPoints.Add(new IPEndPoint(ip, sourcePort));
                }

                endPoints.Add(stunnedEndPoint);
                endPoints.Add(new IPEndPoint(IPAddress.Loopback, sourcePort));

                foreach (var endPoint in endPoints)
                    NetworkManager.AddEndPoint(networkId, new RealIPEndPoint(endPoint));
                NetworkManager.SaveNetworks();
                WebSocketServer.BroadcastNetworkList();
                Task.Run(() => DnmpClient.StartAsFirstNodeAsync(new RealIPEndPoint(new IPEndPoint(IPAddress.Any, sourcePort)), new RealIPEndPoint((IPEndPoint)stunnedEndPoint), networkConnectData.Item2, new AesSymmetricKey(), selfNodeData.GetBytes()));
            }
            else
                Task.Run(() => DnmpClient.ConnectManyAsync(networkConnectData.Item1.ToArray(), new RealIPEndPoint(new IPEndPoint(IPAddress.Any, sourcePort)), true, networkConnectData.Item2, new AesSymmetricKey(), selfNodeData.GetBytes()));
            CurrentNetworkId = networkId;
        }

        public void Disconnect()
        {
            DnmpClient.Stop();
            TapMessageInterface.Stop();
            CurrentNetworkId = Guid.Empty;
        }

        public void StopServers()
        {
            Running = false;
            WebSocketServer.Stop();
            HttpServer.Stop();
        }

        public void StartServers()
        {
            WebSocketServer.Start();
            HttpServer.Start();
            Running = true;
        }

        public void Dispose()
        {
            DnmpClient?.Dispose();
            WebSocketServer?.Dispose();
            TapMessageInterface?.Dispose();
        }
    }

    internal class ClientException : Exception
    {
        public ClientException(string message) : base(message) { }
    }
}
