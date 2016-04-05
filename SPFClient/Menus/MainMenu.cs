using System;
using System.Windows.Forms;
using NativeUI;
using SPFClient.Network;
using SPFClient.UI;
using GTA;
using System.Net;

namespace SPFClient.Menus
{
    public sealed class GameMenu : Script
    {
        public static UIMenu MainMenu { get { return mainMenu; } }

        private readonly MenuPool menuPool;
        private static UIMenu mainMenu, serverListMenu;

        public GameMenu()
        {
            WCFNetworkService.Init();
            serverListMenu = new UIMenu("Server Browser", "Active Sessions");
            serverListMenu.CounterEnabled = false;
            mainMenu = new UIMenu("SPF Menu", "BETA");
            var menuItem = new UIMenuItem("Server Browser");
            mainMenu.AddItem(menuItem);
            menuItem = new UIMenuItem("Direct Connect");
            menuItem.Activated += (s, e) => GetIPAddressInput();
            mainMenu.AddItem(menuItem);
            mainMenu.BindMenuToItem(serverListMenu, mainMenu.MenuItems[0]);         
            mainMenu.OnItemSelect += MainMenu_OnItemSelect;
            menuPool = new MenuPool();
            menuPool.ToList().AddRange(new UIMenu[] { mainMenu, serverListMenu });
            KeyDown += KeyPressed;
            Tick += OnTick;
        }

        private void ShowOnlineServerBrowser()
        {
            if (!WCFNetworkService.Initialized)
            {
                GTA.UI.Notify(string.Format("~r~Error- ~w~{0}", "Server unavailable."));
                return;
            }

            serverListMenu.MenuItems.Clear();

            var data = WCFNetworkService.GetSessionList();

            for (int i = 0; i < data.Length; i++)
            {
                var menuItem = new ServerMenuItem(data[i]);

                menuItem.Enabled = false;

                menuItem.Activated += (s, e) =>
                {
                    if (NetworkSession.Initialized)
                        NetworkSession.Close();

                   // Scripts.FadeInScreen(500, 1000);

                    Wait(500);

                    NetworkSession.JoinActiveSession(menuItem.Session);

                    //Scripts.FadeInScreen(500, 1000);

                    s.Visible = false;
                };

                serverListMenu.AddItem(menuItem);
            }

            serverListMenu.RefreshIndex();
        }

        private void MainMenu_OnItemSelect(UIMenu sender, UIMenuItem selectedItem, int index)
        {
            if (sender == mainMenu)
                switch (index)
                {
                    case 0:
                        ShowOnlineServerBrowser(); return;
                }
        }

        private void GetIPAddressInput()
        {
            menuPool.CloseAllMenus();
           NetworkSession.JoinSessionDirect(IPAddress.Parse("192.168.1.147"));
      //     UIManager.UIInput.ReturnedResult += UIInput_ReturnedResult;
       //     UIManager.UIInput.Display();                
        }
        private void UIInput_ReturnedResult(UIInput sender, string result)
        {
            IPAddress addr;

            if (IPAddress.TryParse(result, out addr))
            {
                NetworkSession.JoinSessionDirect(addr);
            }

            else GTA.UI.Notify("Invalid IP address specified. \"" + result + "\"");
        }

        private void OnTick(object sender, EventArgs e)
        {    
            menuPool.ProcessMenus();
        }

        private void KeyPressed(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Y)
            {
                mainMenu.Visible = !mainMenu.Visible;
            }

            if (NetworkSession.Initialized && !UIChat.Active && e.KeyCode == Keys.T)
            {
                Wait(100);
                UIChat.SetVisibleState(UIChat.VisibleState.Typing, UIChat.TypeMode.All);
            }
        }
    }
}
