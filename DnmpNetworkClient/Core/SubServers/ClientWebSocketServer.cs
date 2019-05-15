using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using DnmpLibrary.Client;
using DnmpLibrary.Core;
using DnmpLibrary.Interaction.Protocol.EndPointImpl;
using DnmpNetworkClient.Config;
using DnmpNetworkClient.Tap.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using StackExchange.NetGain;
using StackExchange.NetGain.WebSockets;

namespace DnmpNetworkClient.Core.SubServers
{
    internal class ClientWebSocketServer : IDisposable
    {
        private class ClientJsonData
        {
            [JsonProperty(PropertyName = "id")]
            public ushort Id;

            [JsonProperty(PropertyName = "parentId")]
            public ushort ParentId;

            [JsonProperty(PropertyName = "publicIpPort")]
            public string PublicIpPort;

            [JsonProperty(PropertyName = "internalIp")]
            public string InternalIp;

            [JsonProperty(PropertyName = "internalDomain")]
            public string InternalDomain;

            [JsonProperty(PropertyName = "flags")]
            public ClientFlags Flags;

            [JsonProperty(PropertyName = "bytesReceived")]
            public long BytesReceived;

            [JsonProperty(PropertyName = "bytesSent")]
            public long BytesSent;

            [JsonProperty(PropertyName = "dataBytesReceived")]
            public long DataBytesReceived;

            [JsonProperty(PropertyName = "dataBytesSent")]
            public long DataBytesSent;

            [JsonProperty(PropertyName = "ping")]
            public int Ping;

            public ClientJsonData(DnmpNode client, MainClient mainClient)
            {
                Id = client.Id;
                ParentId = client.ParentId;
                PublicIpPort = client.EndPoint.ToString();
                InternalIp = mainClient.TapMessageInterface.GetIpFromId(client.Id).ToString();
                InternalDomain = DomainNameUtil.GetDomain(client.GetDnmpNodeData().DomainName, mainClient.Config.TapConfig.DnsFormat);
                Flags = client.Flags;
                BytesReceived = client.BytesReceived;
                BytesSent = client.BytesSent;
                DataBytesReceived = client.DataBytesReceived;
                DataBytesSent = client.DataBytesSent;
                Ping = client.Flags.HasFlag(ClientFlags.SymmetricKeyExchangeDone)
                    ? client.DirectPing
                    : client.RedirectPing.Ping;
            }

        }

        private class NetworkJsonData
        {
            [JsonProperty(PropertyName = "id")]
            public Guid Id;

            [JsonProperty(PropertyName = "name")]
            public string Name;

            [JsonProperty(PropertyName = "savedClients")]
            public int SavedClients;

            public NetworkJsonData(NetworkManager.SavedNetwork network)
            {
                Id = network.Id;
                Name = network.Name;
                SavedClients = network.SavedIpEndPoints.Count;
            }

        }

        private bool running;

        private readonly MainClient mainClient;
        private readonly TcpServer webSocketServer = new TcpServer();

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private Timer clientUpdateTimer;

        public ClientWebSocketServer(MainClient mainClient)
        {
            this.mainClient = mainClient;
            Initialize();
        }

        private void Initialize()
        {
            webSocketServer.ImmediateReconnectListeners = false;
            webSocketServer.ProtocolFactory = WebSocketsSelectorProcessor.Default;
            webSocketServer.ConnectionTimeoutSeconds = mainClient.Config.WebServerConfig.WebSocketTimeout / 1000;
            webSocketServer.Received += message =>
            {
                try
                {
                    var messageText = (string)message.Value;
                    if (messageText == null)
                        return;
                    var requestObject = JObject.Parse(messageText);
                    var requestType = (WebGuiRequestType)requestObject["requestType"].Value<int>();
                    switch (requestType)
                    {
                        case WebGuiRequestType.Disconnect:
                            {
                                mainClient.Disconnect();
                            }
                            break;
                        case WebGuiRequestType.Initialize:
                            {
                                message.Connection.Send(message.Context, JsonConvert.SerializeObject(new
                                {
                                    eventType = WebGuiEventType.Initialization,
                                    eventData = new
                                    {
                                        networkInfo = new
                                        {
                                            state = mainClient.DnmpClient.CurrentStatus,
                                            selfClientId = mainClient.DnmpClient.SelfClient?.Id ?? 0xFFFF,
                                            clients = mainClient.DnmpClient.CurrentStatus == DnmpClient.ClientStatus.Connected &&
                                                      mainClient.DnmpClient.SelfClient != null
                                                ? mainClient.DnmpClient.ClientsById.Concat(new[]
                                                    {
                                                        new KeyValuePair<ushort, DnmpNode>(
                                                            mainClient.DnmpClient.SelfClient.Id, mainClient.DnmpClient.SelfClient)
                                                    })
                                                    .Select(x => new ClientJsonData(x.Value, mainClient))
                                                : null,
                                            config = mainClient.Config
                                        }
                                    }
                                }, new ConfigJsonConverter()));
                                BroadcastNetworkList();
                            }
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                catch (Exception e)
                {
                    logger.Debug(e, "Exception on receiving message from WebSocket");
                }
            };

            mainClient.DnmpClient.OnConnected += () => BroadcastStatusChange(DnmpClient.ClientStatus.Connected);

            mainClient.DnmpClient.OnDisconnected += () => BroadcastStatusChange(DnmpClient.ClientStatus.NotConnected);

            mainClient.DnmpClient.OnConnectionTimeout += () => BroadcastStatusChange(DnmpClient.ClientStatus.NotConnected);

            mainClient.DnmpClient.OnClientConnected += clientId =>
            {
                if (clientId == mainClient.DnmpClient.SelfClient.Id)
                    return;
                mainClient.NetworkManager.AddEndPoint(mainClient.CurrentNetworkId, (RealIPEndPoint)mainClient.DnmpClient.ClientsById[clientId].EndPoint);
                mainClient.NetworkManager.SaveNetworks();
                BroadcastClientConnect(mainClient.DnmpClient.ClientsById[clientId]);
                BroadcastNetworkList();
            };

            mainClient.DnmpClient.OnClientDisconnected += BroadcastClientDisconnect;

            mainClient.DnmpClient.OnClientParentChange += (clientId, newParentId, oldParentId) =>
                BroadcastClientUpdate(clientId == mainClient.DnmpClient.SelfClient.Id ? mainClient.DnmpClient.SelfClient : mainClient.DnmpClient.ClientsById[clientId]);
        }

        public void BroadcastNotification(string notificationName)
        {
            if (!running)
                return;
            webSocketServer.Broadcast(JsonConvert.SerializeObject(new
            {
                eventType = WebGuiEventType.Notification,
                eventData = new
                {
                    name = notificationName
                }
            }));
        }

        public void BroadcastNetworkList()
        {
            if (!running)
                return;
            webSocketServer.Broadcast(JsonConvert.SerializeObject(new
            {
                eventType = WebGuiEventType.NetworkListUpdate,
                eventData = new
                {
                    networks = mainClient.NetworkManager.SavedNetworks.Values.Select(x => new NetworkJsonData(x))
                }
            }));
        }

        public void BroadcastClientUpdate(DnmpNode client)
        {
            if (!running)
                return;
            webSocketServer.Broadcast(JsonConvert.SerializeObject(new
            {
                eventType = WebGuiEventType.ClientUpdate,
                eventData = new
                {
                    client = new ClientJsonData(client, mainClient)
                }
            }));
        }

        public void BroadcastStatusChange(DnmpClient.ClientStatus status)
        {
            if (!running)
                return;
            webSocketServer.Broadcast(JsonConvert.SerializeObject(new
            {
                eventType = WebGuiEventType.SelfStatusChange,
                eventData = new
                {
                    status,
                    selfClient = mainClient.DnmpClient.SelfClient == null ? null : new ClientJsonData(mainClient.DnmpClient.SelfClient, mainClient)
                }
            }));
        }

        public void BroadcastClientConnect(DnmpNode client)
        {
            if (!running)
                return;
            webSocketServer.Broadcast(JsonConvert.SerializeObject(new
            {
                eventType = WebGuiEventType.ClientConnect,
                eventData = new
                {
                    newClient = new ClientJsonData(client, mainClient)
                }
            }));
        }

        public void BroadcastClientDisconnect(ushort clientId)
        {
            if (!running)
                return;
            webSocketServer.Broadcast(JsonConvert.SerializeObject(new
            {
                eventType = WebGuiEventType.ClientDisconnect,
                eventData = new
                {
                    clientId
                }
            }));
        }

        public void Start()
        {
            logger.Info("WebSocket server started");
            webSocketServer.Start(null, new IPEndPoint(IPAddress.Parse(mainClient.Config.WebServerConfig.HttpServerIp), mainClient.Config.WebServerConfig.WebSocketServerPort));
            clientUpdateTimer?.Dispose();
            clientUpdateTimer = new Timer(_ =>
            {
                foreach (var dynNetClient in mainClient.DnmpClient.ClientsById.Values)
                    BroadcastClientUpdate(dynNetClient);
            }, null, 0, 1000);
            running = true;
        }

        public void Stop()
        {
            clientUpdateTimer.Change(Timeout.Infinite, Timeout.Infinite);
            running = false;
            webSocketServer.Stop();
        }

        public void Dispose()
        {
            mainClient?.Dispose();
            ((IDisposable) webSocketServer)?.Dispose();
            clientUpdateTimer?.Dispose();
        }
    }
}