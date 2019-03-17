using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using DNMPLibrary.Client;
using DNMPLibrary.Core;
using DNMPLibrary.Interaction.Protocol.EndPointImpl;
using DNMPLibrary.Interaction.Protocol.ProtocolImpl;
using DNMPLibrary.Security.Cryptography.Asymmetric.Impl;
using DNMPLibrary.Security.Cryptography.Symmetric.Impl;
using DNMPLibrary.Util;
using Newtonsoft.Json.Linq;
using NLog;
using DNMPWindowsClient.Properties;
using Newtonsoft.Json;
using uhttpsharp;
using uhttpsharp.RequestProviders;
using uhttpsharp.Listeners;
using StackExchange.NetGain;
using StackExchange.NetGain.WebSockets;
using Timer = System.Threading.Timer;

namespace DNMPWindowsClient
{
    internal class DNMPNodeData
    {
        public string DomainName = "";

        public byte[] GetBytes()
        {
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(this));
        }
    }

    internal static class DNMPNodeExtension
    {
        public static DNMPNodeData GetDNMPNodeData(this DNMPNode node)
        {
            return JsonConvert.DeserializeObject<DNMPNodeData>(Encoding.UTF8.GetString(node.CustomData));
        }
    }

    internal class Program
    {

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private static readonly HttpServer httpServer = new HttpServer(new HttpRequestProvider());

        private static MainConfig config = new MainConfig();

        private static NetworkManager networkManager;

        private static readonly TcpServer webSocketServer = new TcpServer();

        private static TapMessageInterface tapMessageInterface;

        private static DNMPClient dnmpClient;

        private static readonly Random random = new Random();

        private static Guid currentNetworkId;

        private enum WebGuiEventType
        {
            Initialization,
            SelfStatusChange,
            ClientConnect,
            ClientDisconnect,
            ClientUpdate,
            NetworkListUpdate,
        }

        private enum WebGuiRequestType
        {
            Initialize,
            Disconnect
        }

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

            public ClientJsonData(DNMPNode client)
            {
                Id = client.Id;
                ParentId = client.ParentId;
                PublicIpPort = client.EndPoint.ToString();
                InternalIp = tapMessageInterface.GetIpFromId(client.Id).ToString();
                InternalDomain = DomainNameUtil.GetDomain(client.GetDNMPNodeData().DomainName, config.TapConfig.DnsFormat);
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

        private static void CleanUpVoid(object _)
        {
            networkManager.CleanUpOldEndPoints(TimeSpan.FromMilliseconds(config.NetworksSaveConfig.SavedEndPointTtl));
            EventQueue.AddEvent(CleanUpVoid, null,
                DateTime.Now + TimeSpan.FromMilliseconds(config.NetworksSaveConfig.SavedEndPointsCleanUpInterval));
        }

        private static volatile bool running;

        private static void WinFormsThread(object _)
        {
            var mainNotifyIcon = new NotifyIcon();
            var iconContextMenu = new ContextMenu();

            var exitMenuItem = new MenuItem
            {
                Text = @"Выход",
            };

            var openGuiMenuItem = new MenuItem
            {
                Text = @"Открыть интерфейс",
            };

            openGuiMenuItem.Click += (o, e) => { Process.Start(new ProcessStartInfo("cmd", $"/c start http://127.0.0.1:{config.WebServerConfig.HttpServerPort}") { CreateNoWindow = true, UseShellExecute = false }); };
            
            mainNotifyIcon.DoubleClick += (o, e) => { Process.Start(new ProcessStartInfo("cmd", $"/c start http://127.0.0.1:{config.WebServerConfig.HttpServerPort}") { CreateNoWindow = true, UseShellExecute = false }); };

            exitMenuItem.Click += (o, e) =>
            {
                mainNotifyIcon.Visible = false;
                Application.Exit();
                Environment.Exit(0);
                running = false;
            };

            iconContextMenu.MenuItems.Add(openGuiMenuItem);
            iconContextMenu.MenuItems.Add(new MenuItem { Text = @"-" });
            iconContextMenu.MenuItems.Add(exitMenuItem);

            mainNotifyIcon.Text = @"DynNet client";
            mainNotifyIcon.ContextMenu = iconContextMenu;
            mainNotifyIcon.Icon = Resources.NotifyIcon;

            mainNotifyIcon.Visible = true;

            Application.Run();
        }
        
        private static readonly Mutex singleInstanceMutex = new Mutex(true, "{f26f7326-ff1e-4138-ba52-f633fcdef7ab}");
        
        private const string configFile = "config.json";

        private static readonly DNMPNodeData selfNodeData = new DNMPNodeData();

        private static void Main()
        {
            if (!singleInstanceMutex.WaitOne(TimeSpan.Zero, true))
            {
                MessageBox.Show(@"Только один экземпляр приложения может быть запущен!", @"Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
                {
                    MessageBox.Show(@"Для работы TAP-интерфейса требуются права администратора!", @"Ошибка",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            running = true;

            var winFormsThread = new Thread(WinFormsThread);
            winFormsThread.Start();

            logger.Info("Starting...");

            if (File.Exists(configFile))
                config = JsonConvert.DeserializeObject<MainConfig>(File.ReadAllText(configFile));
            File.WriteAllText(configFile, JsonConvert.SerializeObject(config));

            selfNodeData.DomainName = config.TapConfig.SelfName;

            networkManager = new NetworkManager(config.NetworksSaveConfig.SaveFile);
            CleanUpVoid(null);

            tapMessageInterface = new TapMessageInterface(config.TapConfig);

            dnmpClient = new DNMPClient(tapMessageInterface, new UdpProtocol(), config.ClientConfig);

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                logger.Fatal((Exception)e.ExceptionObject, $"UnhandledException from {e}");
            };
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                logger.Fatal(e.Exception, $"UnobservedTaskException from {e}");
            };

            webSocketServer.ImmediateReconnectListeners = false;
            webSocketServer.ProtocolFactory = WebSocketsSelectorProcessor.Default;
            webSocketServer.ConnectionTimeoutSeconds = config.WebServerConfig.WebSocketTimeout / 1000;
            webSocketServer.Received += message =>
            {
                try
                {
                    var messageText = (string) message.Value;
                    if (messageText == null)
                        return;
                    var requestObject = JObject.Parse(messageText);
                    var requestType = (WebGuiRequestType)requestObject["requestType"].Value<int>();
                    switch (requestType)
                    {
                        case WebGuiRequestType.Disconnect:
                            {
                                dnmpClient.Stop();
                                tapMessageInterface.Stop();
                                currentNetworkId = Guid.Empty;
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
                                            state = dnmpClient.CurrentStatus,
                                            selfClientId = dnmpClient.SelfClient?.Id ?? 0xFFFF,
                                            clients = dnmpClient.CurrentStatus == DNMPClient.ClientStatus.Connected &&
                                                      dnmpClient.SelfClient != null
                                                ? dnmpClient.ClientsById.Concat(new[]
                                                    {
                                                        new KeyValuePair<ushort, DNMPNode>(
                                                            dnmpClient.SelfClient.Id, dnmpClient.SelfClient)
                                                    })
                                                    .Select(x => new ClientJsonData(x.Value))
                                                : null,
                                            config
                                        }
                                    }
                                }, new ConfigJsonConverter()));

                                webSocketServer.Broadcast(JsonConvert.SerializeObject(new
                                {
                                    eventType = WebGuiEventType.NetworkListUpdate,
                                    eventData = new
                                    {
                                        networks = networkManager.SavedNetworks.Values.Select(x => new NetworkJsonData(x))
                                    }
                                }));
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
            webSocketServer.Start(null,
                new IPEndPoint(IPAddress.Parse(config.WebServerConfig.HttpServerIp), config.WebServerConfig.WebSocketServerPort));

            dnmpClient.OnConnected += () =>
            {
                webSocketServer.Broadcast(JsonConvert.SerializeObject(new
                {
                    eventType = WebGuiEventType.SelfStatusChange,
                    eventData = new
                    {
                        status = DNMPClient.ClientStatus.Connected,
                        selfClient = new ClientJsonData(dnmpClient.SelfClient)
                    }
                }));
            };

            dnmpClient.OnDisconnected += () =>
            {
                currentNetworkId = Guid.Empty;
                webSocketServer.Broadcast(JsonConvert.SerializeObject(new
                {
                    eventType = WebGuiEventType.SelfStatusChange,
                    eventData = new
                    {
                        status = DNMPClient.ClientStatus.NotConnected
                    }
                }));
            };

            dnmpClient.OnConnectionTimeout += () =>
            {
                currentNetworkId = Guid.Empty;
                webSocketServer.Broadcast(JsonConvert.SerializeObject(new
                {
                    eventType = WebGuiEventType.SelfStatusChange,
                    eventData = new
                    {
                        status = DNMPClient.ClientStatus.NotConnected
                    }
                }));
            };

            dnmpClient.OnClientConnected += clientId =>
            {
                if (clientId == dnmpClient.SelfClient.Id)
                    return;
                networkManager.AddEndPoint(currentNetworkId, (RealIPEndPoint)dnmpClient.ClientsById[clientId].EndPoint);
                networkManager.SaveNetworks(config.NetworksSaveConfig.SaveFile);
                webSocketServer.Broadcast(JsonConvert.SerializeObject(new
                {
                    eventType = WebGuiEventType.ClientConnect,
                    eventData = new
                    {
                        newClient = new ClientJsonData(dnmpClient.ClientsById[clientId])
                    }
                }));
            };

            dnmpClient.OnClientDisconnected += clientId =>
            {
                webSocketServer.Broadcast(JsonConvert.SerializeObject(new
                {
                    eventType = WebGuiEventType.ClientDisconnect,
                    eventData = new
                    {
                        clientId
                    }
                }));
            };

            dnmpClient.OnClientParentChange += (clientId, newParentId, oldParentId) =>
            {
                webSocketServer.Broadcast(JsonConvert.SerializeObject(new
                {
                    eventType = WebGuiEventType.ClientUpdate,
                    eventData = new
                    {
                        client = clientId == dnmpClient.SelfClient.Id ? new ClientJsonData(dnmpClient.SelfClient) : new ClientJsonData(dnmpClient.ClientsById[clientId])
                    }
                }));
            };

            var clientUpdateTimer = new Timer(_ =>
            {
                foreach (var dynNetClient in dnmpClient.ClientsById.Values)
                    webSocketServer.Broadcast(JsonConvert.SerializeObject(new
                    {
                        eventType = WebGuiEventType.ClientUpdate,
                        eventData = new
                        {
                            client = new ClientJsonData(dynNetClient)
                        }
                    }));
            }, null, 0, 1000);

            logger.Info("WebSocket server started");

            httpServer.Use(new TcpListenerAdapter(new TcpListener(IPAddress.Parse(config.WebServerConfig.HttpServerIp), config.WebServerConfig.HttpServerPort)));
            httpServer.Use((context, next) =>
            {
                var url = context.Request.Uri.OriginalString;
                if (Regex.IsMatch(url, "^/api/([^/]*)$"))
                {
                    object response = null;
                    try
                    {
                        var apiMatch = Regex.Match(url, "^/api/([^/]*)$");
                        var endPoint = apiMatch.Groups[1].Value;
                        var requestObject = JObject.Parse(Encoding.UTF8.GetString(context.Request.Post.Raw));
                        switch (endPoint)
                        {
                            case "getwebsocket":
                                {
                                    response = new
                                    {
                                        address = $"ws://127.0.0.1:{config.WebServerConfig.WebSocketServerPort}"
                                    };
                                }
                                break;
                            case "createnetwork":
                                {
                                    var keySize = requestObject["requestData"]["keySize"].Value<int?>() ??
                                                  config.GeneralConfig.DefaultRsaKeySize;
                                    var networkName = requestObject["requestData"]["name"].Value<string>() ??
                                                      "My best network - " + random.Next();
                                    networkManager.AddNetwork(networkName,
                                        RSAKeyUtils.PrivateKeyToPKCS8(new RSAAsymmetricKey(keySize).KeyParameters));
                                    networkManager.SaveNetworks(config.NetworksSaveConfig.SaveFile);
                                    webSocketServer.Broadcast(JsonConvert.SerializeObject(new
                                    {
                                        eventType = WebGuiEventType.NetworkListUpdate,
                                        eventData = new
                                        {
                                            networks = networkManager.SavedNetworks.Values.Select(x =>
                                                new NetworkJsonData(x))
                                        }
                                    }));
                                    response = new
                                    {
                                        error = default(string)
                                    };
                                }
                                break;
                            case "addnetwork":
                                {
                                    try
                                    {
                                        var key = Base32.Decode(requestObject["requestData"]["key"].Value<string>());
                                        var networkName = requestObject["requestData"]["name"].Value<string>() ??
                                                          "My best network - " + random.Next();
                                        var newNetworkId = networkManager.AddNetwork(networkName, key);
                                        response = new
                                        {
                                            error = newNetworkId == Guid.Empty ? default(string) : "network-already-exists",
                                            networkId = networkManager.AddNetwork(networkName, key)
                                        };
                                        networkManager.SaveNetworks(config.NetworksSaveConfig.SaveFile);
                                        webSocketServer.Broadcast(JsonConvert.SerializeObject(new
                                        {
                                            eventType = WebGuiEventType.NetworkListUpdate,
                                            eventData = new
                                            {
                                                networks = networkManager.SavedNetworks.Values.Select(x =>
                                                    new NetworkJsonData(x))
                                            }
                                        }));
                                    }
                                    catch (Exception)
                                    {
                                        response = new
                                        {
                                            error = "incorrect-key-format"
                                        };
                                    }
                                }
                                break;
                            case "removenetwork":
                                networkManager.RemoveNetwork(Guid.Parse(requestObject["requestData"]["networkId"].Value<string>()));
                                networkManager.SaveNetworks(config.NetworksSaveConfig.SaveFile);
                                webSocketServer.Broadcast(JsonConvert.SerializeObject(new
                                {
                                    eventType = WebGuiEventType.NetworkListUpdate,
                                    eventData = new
                                    {
                                        networks = networkManager.SavedNetworks.Values.Select(x => new NetworkJsonData(x))
                                    }
                                }));
                                response = new
                                {
                                    error = default(string)
                                };
                                break;
                            case "connecttonetwork":
                                {
                                    var networkId = Guid.Parse(requestObject["requestData"]["networkId"].Value<string>());
                                    var sourcePort = requestObject["requestData"]["sourcePort"].Value<int?>() ?? config.StunConfig.PunchPort;
                                    var startAsFirst = requestObject["requestData"]["startAsFirst"].Value<bool>();
                                    var useUpnp = requestObject["requestData"]["useUpnp"].Value<bool>();

                                    var networkConnectData = networkManager.SavedNetworks[networkId].GetConnectionData();

                                    if (useUpnp)
                                        PortMapperUtils.TryMapPort(sourcePort, config.StunConfig.PortMappingTimeout).Wait();

                                    if (startAsFirst)
                                    {
                                        EndPoint stunnedEndPoint;

                                        if (requestObject["requestData"]["useStun"].Value<bool>())
                                        {
                                            try
                                            {
                                                stunnedEndPoint = PortMapperUtils.GetStunnedIpEndPoint(sourcePort,
                                                    config.StunConfig.Host,
                                                    config.StunConfig.Port);
                                            }
                                            catch (Exception)
                                            {
                                                response = new
                                                {
                                                    error = "stun-error"
                                                };
                                                break;
                                            }

                                            if (stunnedEndPoint == null)
                                            {
                                                response = new
                                                {
                                                    error = "stun-error"
                                                };
                                                break;
                                            }
                                        }
                                        else
                                        {
                                            stunnedEndPoint = new IPEndPoint(IPAddress.Parse(requestObject["requestData"]["publicIp"].Value<string>()),
                                                sourcePort);
                                        }

                                        networkManager.AddEndPoint(networkId, new RealIPEndPoint((IPEndPoint)stunnedEndPoint));
                                        networkManager.SaveNetworks(config.NetworksSaveConfig.SaveFile);
                                        webSocketServer.Broadcast(JsonConvert.SerializeObject(new
                                        {
                                            eventType = WebGuiEventType.NetworkListUpdate,
                                            eventData = new
                                            {
                                                networks = networkManager.SavedNetworks.Values.Select(x => new NetworkJsonData(x))
                                            }
                                        }));
                                        Task.Run(() => dnmpClient.StartAsFirstNodeAsync(new RealIPEndPoint(new IPEndPoint(IPAddress.Any, sourcePort)), new RealIPEndPoint((IPEndPoint)stunnedEndPoint), networkConnectData.Item2, new AESSymmetricKey(), selfNodeData.GetBytes()));
                                    }
                                    else
                                        Task.Run(() => dnmpClient.ConnectManyAsync(networkConnectData.Item1.ToArray(), new RealIPEndPoint(new IPEndPoint(IPAddress.Any, sourcePort)), true, networkConnectData.Item2, new AESSymmetricKey(), selfNodeData.GetBytes()));
                                    currentNetworkId = networkId;
                                    webSocketServer.Broadcast(JsonConvert.SerializeObject(new
                                    {
                                        eventType = WebGuiEventType.SelfStatusChange,
                                        eventData = new
                                        {
                                            status = DNMPClient.ClientStatus.Connecting
                                        }
                                    }));
                                    response = new
                                    {
                                        error = default(string)
                                    };
                                }
                                break;
                            case "processinvite":
                                {
                                    try
                                    {
                                        var inviteCode = Base32.Decode(requestObject["requestData"]["inviteCode"].Value<string>());
                                        var inviteInfo = networkManager.AcceptInviteCode(inviteCode);
                                        networkManager.SaveNetworks(config.NetworksSaveConfig.SaveFile);
                                        if (networkManager.SavedNetworks.ContainsKey(inviteInfo.Item1))
                                        {
                                            response = new
                                            {
                                                error = default(string),
                                                count = inviteInfo.Item2,
                                                networkId = inviteInfo.Item1
                                            };

                                            webSocketServer.Broadcast(JsonConvert.SerializeObject(new
                                            {
                                                eventType = WebGuiEventType.NetworkListUpdate,
                                                eventData = new
                                                {
                                                    networks = networkManager.SavedNetworks.Values.Select(x => new NetworkJsonData(x))
                                                }
                                            }));
                                        }
                                        else
                                            response = new
                                            {
                                                error = "invite-code-network-not-found",
                                                count = inviteInfo.Item2,
                                                networkId = inviteInfo.Item1
                                            };
                                    }
                                    catch (Exception)
                                    {
                                        response = new
                                        {
                                            error = "incorrect-invite-format",
                                            count = 0,
                                            networkId = Guid.Empty
                                        };
                                    }
                                }
                                break;
                            case "generatekey":
                                {
                                    var networkId = Guid.Parse(requestObject["requestData"]["networkId"].Value<string>());
                                    response = new
                                    {
                                        error = default(string),
                                        text = Base32.Encode(networkManager.SavedNetworks[networkId].GenerateKeyData())
                                    };
                                }
                                break;
                            case "generateinvite":
                                {
                                    var networkId = Guid.Parse(requestObject["requestData"]["networkId"].Value<string>());
                                    var maxLength = requestObject["requestData"]["maxLength"].Value<int>();
                                    try
                                    {
                                        response = new
                                        {
                                            error = default(string),
                                            text = Base32.Encode(networkManager.SavedNetworks[networkId].GenerateInviteData(maxLength))
                                        };
                                    }
                                    catch (Exception)
                                    {
                                        response = new
                                        {
                                            error = "unable-to-generate-invite",
                                            text = default(string)
                                        };
                                    }
                                }
                                break;
                            case "updateconfig":
                                {
                                    var newConfig = JsonConvert.DeserializeObject<MainConfig>(requestObject["requestData"]["newConfigJson"].Value<string>());
                                    File.WriteAllText(configFile, JsonConvert.SerializeObject(newConfig));
                                    var info = new ProcessStartInfo
                                    {
                                        Arguments = "/C ping 127.0.0.1 -n 2 && \"" + Application.ExecutablePath + "\"",
                                        WindowStyle = ProcessWindowStyle.Hidden,
                                        CreateNoWindow = true,
                                        FileName = "cmd.exe"
                                    };
                                    Process.Start(info);
                                    Application.Exit();
                                    Environment.Exit(0);
                                }
                                break;
                            default:
                                response = new
                                {
                                    error = "api-method-not-dount"
                                };
                                break;
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Error(e, $"Error while handling request to {context.Request.Uri}");
                    }
                    context.Response = new HttpResponse(HttpResponseCode.Ok, "application/json",
                        new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response))), false);
                    return Task.Factory.GetCompleted();
                }
                if (url.EndsWith("/"))
                    url += "index.html";
                var filePath = $"www-data{url.Replace("..", "")}";
                context.Response = File.Exists(filePath) ?
                    new HttpResponse(HttpResponseCode.Ok, MimeTypeHelper.GetMimeType(Path.GetExtension(url)), new MemoryStream(File.ReadAllBytes(filePath)), false) :
                    new HttpResponse(HttpResponseCode.NotFound, Encoding.UTF8.GetBytes($"<pre>File `{filePath}` not found</pre>"), false);
                return Task.Factory.GetCompleted();
            });
            httpServer.Start();

            logger.Info($"HTTP server started on http://{config.WebServerConfig.HttpServerIp}:{config.WebServerConfig.HttpServerPort}/");

            while (running)
            {
                Thread.Sleep(1);
            }

            webSocketServer.Stop();
            clientUpdateTimer.Change(Timeout.Infinite, Timeout.Infinite);
            clientUpdateTimer.Dispose();

            logger.Info("WebSocket server stopped");
        }
    }
}
