using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Shaiya_Duff_Updater
{
    public partial class iButton2 : UserControl
    {
        public static readonly DependencyProperty XamlText = DependencyProperty.Register("Text", typeof(string), typeof(iButton2), new PropertyMetadata(TextChanged));
        public string Text { get { return (string)GetValue(XamlText); } set { SetValue(XamlText, value); } }
        public iButton2() { InitializeComponent(); }
        private static void TextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) { var b = (iButton2)sender; if (b.TextBlockButtonText != null) b.TextBlockButtonText.Text = e.NewValue == null ? "" : e.NewValue.ToString(); }
        private static Brush B(string c) { return (Brush)new BrushConverter().ConvertFrom(c); }
        private static SolidColorBrush Cb(byte a, byte r, byte g, byte b) { return new SolidColorBrush(Color.FromArgb(a, r, g, b)); }
        private static DropShadowEffect G(string c) { return new DropShadowEffect { Color = (Color)ColorConverter.ConvertFromString(c), Direction = 320.0, ShadowDepth = 0.0, Opacity = 1.0, BlurRadius = 8.0 }; }

        private bool _actionEnabled = true;
        // When false the button is greyed out and ignores hover/click (e.g. login server offline).
        public bool ActionEnabled
        {
            get { return _actionEnabled; }
            set { _actionEnabled = value; ApplyEnabledVisual(); }
        }

        private void ApplyEnabledVisual()
        {
            if (TextBlockButtonText == null || ButtonBorder == null) return;
            if (_actionEnabled)
            {
                ButtonBorder.Background = Cb(0x1A, 0xE8, 0xC8, 0x81);
                ButtonBorder.BorderBrush = B("#4DE8C881");
                TextBlockButtonText.Foreground = B("#E8C881");
                TextBlockButtonText.Effect = null;
                Cursor = Cursors.Hand;
            }
            else
            {
                ButtonBorder.Background = Cb(0x14, 0x12, 0x12, 0x16);
                ButtonBorder.BorderBrush = B("#1FE8C881");
                TextBlockButtonText.Foreground = B("#6E6A5E");
                TextBlockButtonText.Effect = null;
                Cursor = Cursors.Arrow;
            }
        }

        private void UserControl_MouseEnter(object sender, MouseEventArgs e) { if (!_actionEnabled) return; ButtonBorder.Background = Cb(0x33, 0xE8, 0xC8, 0x81); TextBlockButtonText.Foreground = B("#FFF3C9"); TextBlockButtonText.Effect = G("#E8C881"); }
        private void UserControl_MouseLeave(object sender, MouseEventArgs e) { if (!_actionEnabled) { ApplyEnabledVisual(); return; } ButtonBorder.Background = Cb(0x1A, 0xE8, 0xC8, 0x81); TextBlockButtonText.Foreground = B("#E8C881"); TextBlockButtonText.Effect = null; }
        private void UserControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (!_actionEnabled) return; ButtonBorder.Background = Cb(0x0D, 0xE8, 0xC8, 0x81); TextBlockButtonText.Foreground = B("#B8A065"); TextBlockButtonText.Effect = G("#E8C881"); }
        private void UserControl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { if (!_actionEnabled) return; ButtonBorder.Background = Cb(0x1A, 0xE8, 0xC8, 0x81); TextBlockButtonText.Foreground = B("#E8C881"); TextBlockButtonText.Effect = null; }
    }
}
