using System;
using System.IO;
using System.Linq;
using System.Net;
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

        public void Connect(Guid networkId, int sourcePort, bool startAsFirst, IPAddress publicIp, bool useUpnp = true, bool useStun = true)
        {
            var networkConnectData = NetworkManager.SavedNetworks[networkId].GetConnectionData();

            if (useUpnp)
                PortMapperUtils.TryMapPort(sourcePort, Config.StunConfig.PortMappingTimeout).Wait();

            if (startAsFirst)
            {
                EndPoint stunnedEndPoint;

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

                NetworkManager.AddEndPoint(networkId, new RealIPEndPoint((IPEndPoint)stunnedEndPoint));
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
