using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DnmpLibrary.Client;
using DnmpLibrary.Security.Cryptography.Asymmetric.Impl;
using DnmpNetworkClient.Config;
using DnmpNetworkClient.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using uhttpsharp;
using uhttpsharp.Listeners;
using uhttpsharp.RequestProviders;

namespace DnmpNetworkClient.Core.SubServers
{
    internal class ClientHttpServer
    {
        private HttpServer httpServer;

        private readonly MainClient mainClient;

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private static readonly Random random = new Random();

        public ClientHttpServer(MainClient mainClient)
        {
            this.mainClient = mainClient;
            Initialize();
        }

        private void Initialize()
        {
            httpServer = new HttpServer(new HttpRequestProvider());

            httpServer.Use(new TcpListenerAdapter(new TcpListener(IPAddress.Parse(mainClient.Config.WebServerConfig.HttpServerIp), mainClient.Config.WebServerConfig.HttpServerPort)));
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
                            case "getwebsocketport":
                                {
                                    response = new
                                    {
                                        port = mainClient.Config.WebServerConfig.WebSocketServerPort
                                    };
                                }
                                break;
                            case "createnetwork":
                                {
                                    var keySize = requestObject["requestData"]["keySize"].Value<int?>() ??
                                                  mainClient.Config.GeneralConfig.DefaultRsaKeySize;
                                    var networkName = requestObject["requestData"]["name"].Value<string>() ??
                                                      "My best network - " + random.Next();
                                    mainClient.NetworkManager.AddNetwork(networkName,
                                        RsaKeyUtil.PrivateKeyToPKCS8(new RsaAsymmetricKey(keySize).KeyParameters));
                                    mainClient.NetworkManager.SaveNetworks();
                                    mainClient.WebSocketServer.BroadcastNetworkList();
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
                                        var key = Base32Util.Decode(requestObject["requestData"]["key"].Value<string>());
                                        var networkName = requestObject["requestData"]["name"].Value<string>() ??
                                                          "My best network - " + random.Next();
                                        var newNetworkId = mainClient.NetworkManager.AddNetwork(networkName, key);
                                        response = new
                                        {
                                            error = newNetworkId == Guid.Empty ? default(string) : "network-already-exists",
                                            networkId = mainClient.NetworkManager.AddNetwork(networkName, key)
                                        };
                                        mainClient.NetworkManager.SaveNetworks();
                                        mainClient.WebSocketServer.BroadcastNetworkList();
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
                                mainClient.NetworkManager.RemoveNetwork(Guid.Parse(requestObject["requestData"]["networkId"].Value<string>()));
                                mainClient.NetworkManager.SaveNetworks();
                                mainClient.WebSocketServer.BroadcastNetworkList();
                                response = new
                                {
                                    error = default(string)
                                };
                                break;
                            case "connecttonetwork":
                                {
                                    var networkId = Guid.Parse(requestObject["requestData"]["networkId"].Value<string>());
                                    var sourcePort = requestObject["requestData"]["sourcePort"].Value<int?>() ?? mainClient.Config.StunConfig.PunchPort;
                                    var startAsFirst = requestObject["requestData"]["startAsFirst"].Value<bool>();
                                    var useUpnp = requestObject["requestData"]["useUpnp"].Value<bool>();
                                    var useStun = requestObject["requestData"]["useStun"].Value<bool>();
                                    IPAddress.TryParse(requestObject["requestData"]["publicIp"].Value<string>(), out var publicIp);


                                    mainClient.WebSocketServer.BroadcastStatusChange(DnmpClient.ClientStatus
                                        .Connecting);
                                    try
                                    {
                                        mainClient.Connect(networkId, sourcePort, startAsFirst, publicIp, useUpnp,
                                            useStun);
                                        response = new
                                        {
                                            error = default(string)
                                        };
                                    }
                                    catch (ClientException e)
                                    {
                                        response = new
                                        {
                                            error = e.Message
                                        };
                                    }

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
                                        var inviteCode = Base32Util.Decode(requestObject["requestData"]["inviteCode"].Value<string>());
                                        var inviteInfo = mainClient.NetworkManager.AcceptInviteCode(inviteCode);
                                        mainClient.NetworkManager.SaveNetworks();
                                        if (mainClient.NetworkManager.SavedNetworks.ContainsKey(inviteInfo.Item1))
                                        {
                                            response = new
                                            {
                                                error = default(string),
                                                count = inviteInfo.Item2,
                                                networkId = inviteInfo.Item1
                                            };

                                            mainClient.WebSocketServer.BroadcastNetworkList();
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
                                        text = Base32Util.Encode(mainClient.NetworkManager.SavedNetworks[networkId].GenerateKeyData())
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
                                            text = Base32Util.Encode(mainClient.NetworkManager.SavedNetworks[networkId].GenerateInviteData(maxLength))
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
                                    File.WriteAllText(mainClient.ConfigFile, JsonConvert.SerializeObject(newConfig));
                                    Environment.Exit(0);
                                }
                                break;
                            default:
                                response = new
                                {
                                    error = "api-method-not-found"
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
        }

        public void Start()
        {
            httpServer.Start();
            logger.Info($"HTTP server started on http://{mainClient.Config.WebServerConfig.HttpServerIp}:{mainClient.Config.WebServerConfig.HttpServerPort}/");
        }

        public void Stop()
        {
            httpServer.Dispose();
            Initialize();
        }
    }
}
