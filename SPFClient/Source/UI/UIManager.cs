﻿#define debug
using System;
using System.Drawing;
using GTA;
using Font = GTA.Font;
using System.Reflection;
using System.Media;

namespace SPFClient.UI
{
    public class UIManager : GTA.Script
    {
        /// <summary>
        /// Enable the UIText script.
        /// </summary>
        public static bool Enabled { get; set; } = false;

        public static UIRankBar RankBar { get { return rankBar; } }

        public static UIInput UIInput {  get { return uiInput; } }

        public static UIBigMessage BigMessage { get { return bigMessage; } }

        public static UIKillCam Killcam { get { return killcam; } }

        private UIText centerText;
        private static string _uiText1, _uiText2, _uiText3, _uiText4;

        private static int notificationTimer;
        private static int notificationTimeout = 5000;

        private static UIRankBar rankBar = new UIRankBar();

        private static UIInput uiInput = new UIInput();

        private static UIBigMessage bigMessage = new UIBigMessage();

        private static UIKillCam killcam = new UIKillCam();

        public UIManager()
        {
            var bounds = Game.ScreenResolution;
            centerText = new UIText(null, new Point(GTA.UI.WIDTH / 2, GTA.UI.HEIGHT - 38), 0.70f, Color.White, Font.ChaletComprimeCologne, true);
            rankBar.RankedUp += RankBar_RankedUp;
            Tick += OnTick;
        }

        private void RankBar_RankedUp(UIRankBar sender, UIRankBarEventArgs e)
        {
            Game.PlaySound("MEDAL_UP", "HUD_MINI_GAME_SOUNDSET");
            Game.PlaySound("RANK_UP", "HUD_AWARDS");
           // var extSound = new SoundPlayer(Properties.Resources.rank_achieved);
         //   extSound.Play();
        }

        /// <summary>
        /// Update UI related items
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnTick(object sender, EventArgs e)
        {
            if (drawNotify)
            {
                GTA.UI.Notify(bufferText);
                bufferText = null;
                drawNotify = false;
            }

            if (drawSubtitle)
            {
                GTA.UI.ShowSubtitle(bufferText);
                bufferText = null;
                drawSubtitle = false;
            }

#if debug
        UIText debugText = new UIText(string.Format("SPFClient | Beta Build # {0}\nConnected At: {1} Port: {2}\nSent (b): {3} RTT: {4}",
            Assembly.GetExecutingAssembly().GetName().Version.ToString(),
            Network.ClientSession.Current == null ? "N/A" :
            Network.ClientSession.Current.Connection.ServerConnection?.RemoteEndPoint.Address.ToString(),
            Network.ClientSession.Current?.Connection.ServerConnection?.RemoteEndPoint.Port,
            Network.ClientSession.PacketsSent,
            Network.ClientSession.Current?.Connection.ServerConnection?.AverageRoundtripTime,
            Network.ClientSession.Current?.Connection.Statistics.SentBytes,
            Network.ClientSession.Current?.Connection.ServerConnection?.AverageRoundtripTime),
            new Point((GTA.UI.WIDTH / Game.ScreenResolution.Width) * 1080, 20),
            0.6f,
            Color.White,
            GTA.Font.ChaletComprimeCologne,
            true);

            debugText.Draw();
#endif
            uiInput.Update();
            rankBar.Update();
            bigMessage.Update();
        }

        private static UIRectangle rect;
        private static UIText entityText;

        private static void DrawSquare(Point location, Color color, string subText)
        {
            rect = new UIRectangle(new Point(location.X - 25, location.Y), new Size(4, 52)); //left side
            rect.Color = color;
            rect.Draw();
            rect = new UIRectangle(new Point(location.X + 25, location.Y), new Size(4, 52)); //right side
            rect.Color = color;
            rect.Draw();
            rect = new UIRectangle(new Point(location.X - 25, location.Y + 50), new Size(54, 4)); //bottom
            rect.Color = color;
            rect.Draw();
            rect = new UIRectangle(new Point(location.X - 25, location.Y), new Size(52, 4)); //top
            rect.Color = color;
            rect.Draw();
            entityText = new UIText(subText, new Point(location.X - 5, location.Y + 55), 0.3f, Color.LimeGreen, Font.ChaletComprimeCologne, false);
            entityText.Draw();
            //   entityText = new UIText(subText1, new Point(location.X - 30, location.Y + 70), 0.22f);
            //    entityText.Draw();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="timeout"></param>
        public static void NotifyWithWait(string message, int timeout)
        {
            GTA.UI.Notify(message);
            notificationTimeout = timeout;
            notificationTimer = Game.GameTime + notificationTimeout;
        }

        static bool drawNotify, drawSubtitle;
        static string bufferText;

        public static void ShowNotification(string text)
        {
            bufferText = text;
            drawNotify = true;
        }

        public static void ShowSubtitle(string text)
        {
            bufferText = text;
            drawSubtitle = true;
        }

        /// <summary>
        /// Append subtitle UI text.
        /// </summary>
        /// <param name="text"></param>
        public static void Append(string text)
        {
            if (_uiText1 == null)
            {
                _uiText1 = text;
            }

            else if (_uiText2 == null)
            {
                _uiText2 = text;
            }

            else if (_uiText3 == null)
            {
                _uiText3 = text;
            }

            else if (_uiText4 == null)
            {
                _uiText4 = text;
            }

            else return;
        }

        /// <summary>
        /// Clear all subtitle UI text.
        /// </summary>
        public static void Clear()
        {
            _uiText1 = _uiText2 = _uiText3 = _uiText4 = null;
        }
    }
}