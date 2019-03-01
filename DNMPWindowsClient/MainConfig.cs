using DNMPLibrary.Client;

namespace DNMPWindowsClient
{
    [ValidableConfig]
    internal class GeneralConfig
    {
        [ValidableField("(\\d{1,})")]
        public int DefaultRsaKeySize = 2048;
    }

    [ValidableConfig]
    internal class NetworksSaveConfig
    {
        [ValidableField("(?!^(PRN|AUX|CLOCK\\$|NUL|CON|COM\\d|LPT\\d|\\..*)(\\..+)?$)[^\\x00-\\x1f\\\\?*:\\\";|/]+")]
        public string SaveFile = "networks.json";

        [ValidableField("(\\d{1,})")]
        public long SavedEndPointTtl = 2 * 86400 * 1000;

        [ValidableField("(\\d{1,})")]
        public long SavedEndPointsCleanUpInterval = 60 * 60 * 1000;
    }

    [ValidableConfig]
    internal class TapConfig
    {
        [ValidableField("(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.)(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)")]
        public string IpPrefix = "10.228";

        [ValidableField("([0-9a-fA-F]{2}):([0-9a-fA-F]{2}):([0-9a-fA-F]{2}):([0-9a-fA-F]{2})")]
        public string MacPrefix = "CA:FE:BA:BE";
    }

    [ValidableConfig]
    internal class StunConfig
    {
        [ValidableField("[a-z0-9]+([\\-\\.]{1}[a-z0-9]+)*\\.[a-z]{2,5}(:[0-9]{1,5})?(\\/.*)?|^(([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\\.){3}([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])")]
        public string Host = "stun.l.google.com";

        [ValidableField("([1-9][0-9]{0,3}|[1-5][0-9]{4}|6[0-4][0-9]{3}|65[0-4][0-9]{2}|655[0-2][0-9]|6553[0-5])")]
        public int Port = 19302;

        [ValidableField("(\\d{1,})")]
        public int PortMappingTimeout = 1000;

        [ValidableField("([1-9][0-9]{0,3}|[1-5][0-9]{4}|6[0-4][0-9]{3}|65[0-4][0-9]{2}|655[0-2][0-9]|6553[0-5])")]
        public int PunchPort = 51966;
    }

    [ValidableConfig]
    internal class VisualizationConfig
    {
        [ValidableField("(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)")]
        public string ServerIp = "127.0.0.1";

        [ValidableField("([1-9][0-9]{0,3}|[1-5][0-9]{4}|6[0-4][0-9]{3}|65[0-4][0-9]{2}|655[0-2][0-9]|6553[0-5])")]
        public int ServerPort = 12345;
    }

    [ValidableConfig]
    internal class WebServerConfig
    {
        [ValidableField("(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)")]
        public string HttpServerIp = "127.0.0.1";

        [ValidableField("([1-9][0-9]{0,3}|[1-5][0-9]{4}|6[0-4][0-9]{3}|65[0-4][0-9]{2}|655[0-2][0-9]|6553[0-5])")]
        public int HttpServerPort = 15380;

        [ValidableField("([1-9][0-9]{0,3}|[1-5][0-9]{4}|6[0-4][0-9]{3}|65[0-4][0-9]{2}|655[0-2][0-9]|6553[0-5])")]
        public int WebSocketServerPort = 15381;

        [ValidableField("(\\d{1,})")]
        public int WebSocketTimeout = 3000;

    }
    
    internal class MainConfig
    {
        public GeneralConfig GeneralConfig = new GeneralConfig();

        public WebServerConfig WebServerConfig = new WebServerConfig();

        public NetworksSaveConfig NetworksSaveConfig = new NetworksSaveConfig();

        public TapConfig TapConfig = new TapConfig();

        public StunConfig StunConfig = new StunConfig();

        public ClientConfig ClientConfig = new ClientConfig();

        public VisualizationConfig VisualizationConfig = new VisualizationConfig();
    }
}
