using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using LumiSoft.Net.STUN.Client;
using NLog;
using Open.Nat;

namespace DnmpWindowsClient
{
    internal static class PortMapperUtils
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private const int portAlreadyInUseErrorCode = 718;

        public static async Task TryMapPort(int port, int timeOut)
        {
            var natDiscoverer = new NatDiscoverer();
            var cancellationTokenSource = new CancellationTokenSource(timeOut);
            var devices = (await natDiscoverer.DiscoverDevicesAsync(PortMapper.Upnp, cancellationTokenSource))
                .Union(await natDiscoverer.DiscoverDevicesAsync(PortMapper.Pmp, cancellationTokenSource));
            foreach (var device in devices)
            {
                try
                {
                    await device.CreatePortMapAsync(new Mapping(Protocol.Udp, port, port, "Dnmp auto port map"));
                }
                catch (MappingException mappingException)
                {
                    if (mappingException.ErrorCode == portAlreadyInUseErrorCode)
                        continue;
                    logger.Warn(mappingException, "Exception in TryMapPort");
                }
            }
        }

        public static IPEndPoint GetStunnedIpEndPoint(int port, string stunIp, int stunPort)
        {
            var result = STUN_Client.Query(stunIp, stunPort, new IPEndPoint(IPAddress.Any, port));
            if (result.NetType == STUN_NetType.RestrictedCone || result.NetType == STUN_NetType.PortRestrictedCone || result.NetType == STUN_NetType.UdpBlocked)
                return null;
            return result.PublicEndPoint;
        }
    }
}
