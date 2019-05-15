using System;
using System.Threading;
using System.Threading.Tasks;
using DnmpNetworkClient.OSDependent;
using DnmpNetworkClient.OSDependent.Impl;
using NLog;

namespace DnmpNetworkClient.Core
{
    internal class Program
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private static IDependent dependent;

        private static MainClient client;

        private static void Main(string[] args)
        {
            var useGui = args.Length == 0;

            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                case PlatformID.Win32S:
                case PlatformID.Win32Windows:
                case PlatformID.WinCE:
                    logger.Info("Found Windows");
                    dependent = new WindowsDependent();
                    break;
                case PlatformID.Unix:
                    useGui = false;
                    logger.Info("Found Unix");
                    dependent = new UnixDependent();
                    break;
                case PlatformID.MacOSX:
                    logger.Fatal("Mac OS X is not supported yet");
                    return;
                case PlatformID.Xbox:
                    logger.Fatal("Xbox is not supported yet");
                    return;
                default:
                    logger.Fatal($"Platform [{Environment.OSVersion.Platform}] is not supported yet");
                    return;
            }

            dependent.GetRuntime().PreInit(useGui);
            logger.Info("Starting...");

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                logger.Fatal((Exception)e.ExceptionObject, $"UnhandledException from {e}");
            };
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                logger.Fatal(e.Exception, $"UnobservedTaskException from {e}");
            };

            client = new MainClient("config.json", dependent);

            dependent.GetGui().Start(client.Config);
            dependent.GetRuntime().PostInit(useGui);
            client.StartServers();
            while (client.Running)
                Thread.Sleep(1);
            client.StopServers();
        }
    }
}
