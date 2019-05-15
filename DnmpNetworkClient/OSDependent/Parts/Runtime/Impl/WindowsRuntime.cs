using NLog;
using System;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace DnmpNetworkClient.OSDependent.Parts.Runtime.Impl
{
    internal class WindowsRuntime : IRuntime
    {
        private static readonly Mutex singleInstanceMutex = new Mutex(true, "{f26f7326-ff1e-4138-ba52-f633fcdef7ab}");

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public void PreInit(bool useGui)
        {
            if (!singleInstanceMutex.WaitOne(TimeSpan.Zero, true))
            {
                if (useGui)
                    MessageBox.Show(@"Только один экземпляр приложения может быть запущен!", @"Ошибка",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                else
                    logger.Error("Only one instance of app can be opened");
                Environment.Exit(1);
            }

            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                if (principal.IsInRole(WindowsBuiltInRole.Administrator))
                    return;
                if (useGui)
                    MessageBox.Show(@"Для работы TAP-интерфейса требуются права администратора!", @"Ошибка",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                else
                    logger.Error("Need Administrator priveleges for TUN/TAP interface!");
                Environment.Exit(1);
            }
        }

        public void PostInit(bool useGui) { }
    }
}
