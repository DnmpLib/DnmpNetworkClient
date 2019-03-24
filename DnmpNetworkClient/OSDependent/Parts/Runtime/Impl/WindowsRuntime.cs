using System;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace DnmpNetworkClient.OSDependant.Parts.Runtime.Impl
{
    internal class WindowsRuntime : IRuntime
    {
        private static readonly Mutex singleInstanceMutex = new Mutex(true, "{f26f7326-ff1e-4138-ba52-f633fcdef7ab}");

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        public void PreInit()
        {
            ShowWindow(GetConsoleWindow(), 0);

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
        }

        public void Init() { }

        public void PostInit() { }
    }
}
