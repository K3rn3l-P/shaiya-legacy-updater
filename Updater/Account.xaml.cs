using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Shaiya_Duff_Updater
{
    public partial class Account : UserControl
    {
        public string UserID;

        public Account(string userId)
        {
            InitializeComponent();
            UserID = userId;
            TextBlockUserID.Text = userId;
            GridAccountRemove.ToolTip = "Remove " + userId + " from the list of saved accounts.";
        }

        private void UserControl_MouseEnter(object sender, MouseEventArgs e) { GridAccountRemove.Visibility = Visibility.Visible; }
        private void UserControl_MouseLeave(object sender, MouseEventArgs e) { GridAccountRemove.Visibility = Visibility.Hidden; }

        private static Brush ColorBrush(string color) { return (Brush)new BrushConverter().ConvertFrom(color); }
        private static DropShadowEffect Glow(string color) { return new DropShadowEffect { Color = (Color)ColorConverter.ConvertFromString(color), Direction = 320.0, ShadowDepth = 0.0, Opacity = 1.0, BlurRadius = 8.0 }; }

        private void GridAccountRemove_MouseEnter(object sender, MouseEventArgs e) { TextBlockX.Foreground = ColorBrush("#FFF3C9"); TextBlockX.Effect = Glow("#E8C881"); }
        private void GridAccountRemove_MouseLeave(object sender, MouseEventArgs e) { TextBlockX.Foreground = ColorBrush("#E8C881"); TextBlockX.Effect = null; }
        private void GridAccountRemove_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { TextBlockX.Foreground = ColorBrush("#B8A065"); TextBlockX.Effect = Glow("#E8C881"); }

        private void GridAccountRemove_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            TextBlockX.Foreground = ColorBrush("#E8C881");
            TextBlockX.Effect = null;
            if (MessageDialog.Display("Confirmation", "Are you sure you want to remove " + UserID + " from your saved accounts ?", "YesNo") != "Yes") return;
            UpdaterManager.RemoveSavedAccount(UserID);
            MainWindow.Accounts.Remove(UserID);
            MainWindow.MW.AccountRectangles.Children.Remove(this);
            MainWindow.MW.RefreshLogInView();
        }

        private void GridAccountName_MouseEnter(object sender, MouseEventArgs e) { TextBlockUserID.Foreground = ColorBrush("#FFF3C9"); TextBlockUserID.Effect = Glow("#E8C881"); }
        private void GridAccountName_MouseLeave(object sender, MouseEventArgs e) { TextBlockUserID.Foreground = ColorBrush("#E8C881"); TextBlockUserID.Effect = null; }
        private void GridAccountName_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { TextBlockUserID.Foreground = ColorBrush("#B8A065"); TextBlockUserID.Effect = Glow("#E8C881"); }
        private void GridAccountName_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { TextBlockUserID.Foreground = ColorBrush("#E8C881"); TextBlockUserID.Effect = null; MainWindow.MW.LogInId(UserID); }
    }
}
