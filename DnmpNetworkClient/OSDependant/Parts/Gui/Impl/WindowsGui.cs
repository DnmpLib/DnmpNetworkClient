using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using DnmpNetworkClient.Config;
using DnmpNetworkClient.Properties;

namespace DnmpNetworkClient.OSDependant.Parts.Gui.Impl
{
    internal class WindowsGui : IGui
    {
        private static void WindowsGuiThreadVoid(object configObject)
        {
            var config = (MainConfig) configObject;

            var mainNotifyIcon = new NotifyIcon();
            var iconContextMenu = new ContextMenu();

            var exitMenuItem = new MenuItem
            {
                Text = @"Выход",
            };

            var openGuiMenuItem = new MenuItem
            {
                Text = @"Открыть интерфейс",
            };

            openGuiMenuItem.Click += (o, e) => { Process.Start(new ProcessStartInfo("cmd", $"/c start http://127.0.0.1:{config.WebServerConfig.HttpServerPort}") { CreateNoWindow = true, UseShellExecute = false }); };

            mainNotifyIcon.DoubleClick += (o, e) => { Process.Start(new ProcessStartInfo("cmd", $"/c start http://127.0.0.1:{config.WebServerConfig.HttpServerPort}") { CreateNoWindow = true, UseShellExecute = false }); };

            exitMenuItem.Click += (o, e) =>
            {
                mainNotifyIcon.Visible = false;
                Application.Exit();
                Environment.Exit(0);
            };

            iconContextMenu.MenuItems.Add(openGuiMenuItem);
            iconContextMenu.MenuItems.Add(new MenuItem { Text = @"-" });
            iconContextMenu.MenuItems.Add(exitMenuItem);

            mainNotifyIcon.Text = @"DynNet client";
            mainNotifyIcon.ContextMenu = iconContextMenu;
            mainNotifyIcon.Icon = Resources.NotifyIcon;

            mainNotifyIcon.Visible = true;

            Application.Run();
        }

        public void Start(MainConfig config)
        {
            new Thread(WindowsGuiThreadVoid).Start(config);
        }
    }
}
