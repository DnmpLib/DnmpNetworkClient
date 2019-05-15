using System;
using System.Security.Principal;
using Mono.Unix.Native;
using NLog;

namespace DnmpNetworkClient.OSDependent.Parts.Runtime.Impl
{
    internal class UnixRuntime : IRuntime
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public void PreInit(bool useGui)
        {
            var descriptor = Syscall.open("/dev/net/tun", OpenFlags.O_RDWR);
            
            if (descriptor < 0)
            {
                logger.Error("/dev/net/tun open error! Check availability of TAP/TUN!");
                Environment.Exit(0);
            }

            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                if (principal.IsInRole(WindowsBuiltInRole.Administrator))
                    return;
            }

            logger.Error("Need root access for TUN/TAP interface!");
            Environment.Exit(0);
        }

        public void PostInit(bool useGui) { }
    }
}
