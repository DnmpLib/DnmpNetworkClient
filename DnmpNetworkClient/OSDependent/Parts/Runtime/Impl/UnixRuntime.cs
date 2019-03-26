using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using Mono.Unix.Native;
using NLog;

namespace DnmpNetworkClient.OSDependent.Parts.Runtime.Impl
{
    internal class UnixRuntime : IRuntime
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public void PreInit()
        {
            var descriptor = Syscall.open("/dev/net/tun", OpenFlags.O_RDWR);
            
            if (descriptor >= 0)
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

            logger.Error("Need sudo for TUN/TAP interface!");
            Environment.Exit(0);
        }

        public void PostInit() { }
    }
}
