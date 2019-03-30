using System;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace DnmpNetworkClient.OSDependent.Parts.Runtime.Impl
{
    internal class WindowsRuntime : IRuntime
    {
        private static readonly Mutex singleInstanceMutex = new Mutex(true, "{f26f7326-ff1e-4138-ba52-f633fcdef7ab}");

        public void PreInit()
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
                if (principal.IsInRole(WindowsBuiltInRole.Administrator))
                    return;
                MessageBox.Show(@"Для работы TAP-интерфейса требуются права администратора!", @"Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void PostInit() { }
    }
}
