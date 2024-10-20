using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Playnite.SDK.Models;

namespace SuccessStory
{
    public partial class SuccessStoryForceSteamAppId : UserControl
    {
        public Game Game { get; }
        public int? SteamAppId { get; private set; }

        public SuccessStoryForceSteamAppId(Game game)
        {
            InitializeComponent();
            Game = game;
            this.DataContext = this;
        }

        private void OnOK(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(txtSteamAppId.Text, out int appId))
            {
                SteamAppId = appId;
                Window.GetWindow(this).Close();
            }
            else
            {
                MessageBox.Show("Please enter a valid number for the Steam AppId.");
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            // Open the URL in the default browser
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this).Close();
        }
    }
}