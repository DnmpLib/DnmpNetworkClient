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

        private static void RunDefault()
        {
            dependent.GetGui().Start(client.Config);
            dependent.GetRuntime().PostInit();
            client.StartServers();
            while (client.Running)
                Thread.Sleep(1);
        }

        private static void RunConsole()
        {

        }

        private static void Main(string[] args)
        {
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

            dependent.GetRuntime().PreInit();
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
            
            if (args.Length == 0)
                RunDefault();
            //else
            //{
            //    switch (args[0])
            //    {
            //        case "networks":
            //            break;
            //        case "connect":
            //            if (args.Length < 2)
            //            {
            //                logger.Error(@"Not enough args");
            //                return;
            //            }

            //            var networkIdString = args[1];
            //            if (Guid.TryParse(networkIdString, out var networkId))
            //            {
            //                if (client.NetworkManager.SavedNetworks.ContainsKey(networkId))
            //                {

            //                }
            //            }
            //            else
            //            {
            //                logger.Error(@"Wrong gui format");
            //            }
            //            break;
            //        case "run":
            //            RunConsole();
            //            break;
            //        default:
            //            logger.Error(@$"Unknown command '{args[0]}'");
            //            break;
            //    }
            //}
        }
    }
}
