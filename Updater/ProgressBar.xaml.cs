using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Shaiya_Duff_Updater
{
    public partial class ProgressBar : UserControl
    {
        private readonly LinearGradientBrush _fillBrush;
        private readonly DispatcherTimer _shimmerTimer;
        private double _shimmerPhase;
        private double _currentPct;

        private const double BarOuterWidth = 896.0;

        public ProgressBar()
        {
            InitializeComponent();

            _fillBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint   = new Point(1, 0)
            };
            _fillBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0xFF, 0x8A, 0x6A, 0x1A), 0.00));
            _fillBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0xFF, 0xD4, 0xA8, 0x30), 0.35));
            _fillBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0xFF, 0xFF, 0xF0, 0x80), 0.50));
            _fillBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0xFF, 0xD4, 0xA8, 0x30), 0.65));
            _fillBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0xFF, 0x8A, 0x6A, 0x1A), 1.00));
            ProgressFill.Fill = _fillBrush;

            _shimmerTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _shimmerTimer.Tick += OnShimmerTick;
            _shimmerTimer.Start();

            TxtStatus.Text  = string.Empty;
            TxtSpeed.Text   = string.Empty;
            TxtBytes.Text   = string.Empty;
            TxtPercent.Text = string.Empty;
        }

        private void OnShimmerTick(object sender, EventArgs e)
        {
            _shimmerPhase += 0.018;
            if (_shimmerPhase > 1.0) _shimmerPhase -= 1.0;

            double pos = _shimmerPhase;
            double half = 0.18;

            _fillBrush.GradientStops[0].Offset = Math.Max(0.0, pos - half - 0.10);
            _fillBrush.GradientStops[1].Offset = Math.Max(0.0, pos - half * 0.5);
            _fillBrush.GradientStops[2].Offset = pos;
            _fillBrush.GradientStops[3].Offset = Math.Min(1.0, pos + half * 0.5);
            _fillBrush.GradientStops[4].Offset = Math.Min(1.0, pos + half + 0.10);
        }

        private void UpdateBar(double pct)
        {
            _currentPct = Math.Max(0, Math.Min(100, pct));
            double w = BarOuterWidth * _currentPct / 100.0;
            ProgressFill.Width = w;

            double canvasH = BarCanvas.ActualHeight > 0 ? BarCanvas.ActualHeight : 10;
            ProgressFill.Height = canvasH;
        }

        public void SetProgress(byte percent)
        {
            UpdateBar(percent);
            TxtPercent.Text = percent + "%";
        }

        public void SetDownloadProgress(int pct, string status, string received, string total, string speed)
        {
            UpdateBar(pct);
            TxtStatus.Text  = status ?? string.Empty;
            TxtSpeed.Text   = speed  ?? string.Empty;
            TxtBytes.Text   = (received != null && total != null) ? received + " / " + total : string.Empty;
            TxtPercent.Text = pct + "%";
        }

        public void SetInstallProgress(int pct, string status)
        {
            UpdateBar(pct);
            TxtStatus.Text  = status ?? string.Empty;
            TxtSpeed.Text   = string.Empty;
            TxtBytes.Text   = string.Empty;
            TxtPercent.Text = pct + "%";
        }

        public void SetStatus(string text)
        {
            TxtStatus.Text = text ?? string.Empty;
        }

        public void Reset()
        {
            UpdateBar(0);
            TxtStatus.Text  = string.Empty;
            TxtSpeed.Text   = string.Empty;
            TxtBytes.Text   = string.Empty;
            TxtPercent.Text = string.Empty;
        }
    }
}
