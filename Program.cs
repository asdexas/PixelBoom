using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static PixelBoom.NativeMethods;

namespace PixelBoom
{
    public class MainForm : Form
    {
        private const uint BoomColor = 0xEF67EE;
        private const int ColorRatio = 70;
        private const int InitialVariation = 10;
        private const int SleepAfterSend = 180;

        private const int Search1_X1 = 935, Search1_Y1 = 534, Search1_X2 = 958, Search1_Y2 = 548;
        private const int Search2_X1 = 962, Search2_Y1 = 534, Search2_X2 = 985, Search2_Y2 = 548;

        private const int HOTKEY_F1 = 1, HOTKEY_F2 = 2;

        private volatile bool _zoom = false;
        private CancellationTokenSource _cts;
        private Label _statusLabel;
        private Label _infoLabel;
        private NotifyIcon _trayIcon;

        public MainForm()
        {
            this.Text = "PixelBoom";
            this.Size = new System.Drawing.Size(400, 220);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);

            _statusLabel = new Label
            {
                Text = "DURUM: KAPALI",
                Font = new System.Drawing.Font("Consolas", 18, System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.Red,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 70
            };
            Controls.Add(_statusLabel);

            _infoLabel = new Label
            {
                Text = "F1 = Baslat  |  F2 = Durdur\n\n" +
                       "Renk: #EF67EE  |  Tolerans: 70\n" +
                       "Tetik: XButton1 (Mouse yan tus)\n" +
                       "Gonderilen tus: G harfi",
                Font = new System.Drawing.Font("Consolas", 10),
                ForeColor = System.Drawing.Color.LightGray,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill
            };
            Controls.Add(_infoLabel);

            _trayIcon = new NotifyIcon
            {
                Text = "PixelBoom",
                Icon = System.Drawing.SystemIcons.Application,
                Visible = true
            };

            try { SetPriorityClass(GetCurrentProcess(), HIGH_PRIORITY_CLASS); } catch { }
            RegisterHotKey(this.Handle, HOTKEY_F1, 0, (uint)VK_F1);
            RegisterHotKey(this.Handle, HOTKEY_F2, 0, (uint)VK_F2);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                if (m.WParam.ToInt32() == HOTKEY_F1) StartBoom();
                else if (m.WParam.ToInt32() == HOTKEY_F2) StopBoom();
            }
            base.WndProc(ref m);
        }

        private void StartBoom()
        {
            if (_zoom) return;

            using (var s = new PixelSearcher())
            {
                int fx, fy;
                int r = s.PixelSearch(Search1_X1, Search1_Y1, Search1_X2, Search1_Y2,
                    BoomColor, InitialVariation, out fx, out fy);
                if (r == 2) { MessageBox.Show("ERROR"); return; }
            }

            _zoom = true;
            MessageBeep(MB_ICONEXCLAMATION);

            if (InvokeRequired)
                BeginInvoke(new Action(() => { _statusLabel.Text = "DURUM: AKTIF"; _statusLabel.ForeColor = System.Drawing.Color.Lime; }));
            else { _statusLabel.Text = "DURUM: AKTIF"; _statusLabel.ForeColor = System.Drawing.Color.Lime; }

            _cts = new CancellationTokenSource();
            Task.Run(() => BoomLoop(_cts.Token));
        }

        private void StopBoom()
        {
            if (!_zoom) return;
            _zoom = false;
            _cts?.Cancel();
            MessageBeep(MB_ICONHAND);

            if (InvokeRequired)
                BeginInvoke(new Action(() => { _statusLabel.Text = "DURUM: KAPALI"; _statusLabel.ForeColor = System.Drawing.Color.Red; }));
            else { _statusLabel.Text = "DURUM: KAPALI"; _statusLabel.ForeColor = System.Drawing.Color.Red; }
        }

        private void BoomLoop(CancellationToken token)
        {
            using (var searcher = new PixelSearcher())
            {
                while (_zoom && !token.IsCancellationRequested)
                {
                    if ((GetAsyncKeyState(VK_XBUTTON1) & 0x8000) != 0)
                    {
                        int fx, fy;
                        if (searcher.PixelSearch(Search1_X1, Search1_Y1, Search1_X2, Search1_Y2,
                            BoomColor, ColorRatio, out fx, out fy) == 0)
                        {
                            if (searcher.PixelSearch(Search2_X1, Search2_Y1, Search2_X2, Search2_Y2,
                                BoomColor, ColorRatio, out fx, out fy) == 0)
                            {
                                KeyboardSimulator.SendGKey();
                                Thread.Sleep(SleepAfterSend);
                            }
                        }
                    }
                    Thread.Sleep(0);
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _zoom = false;
            _cts?.Cancel();
            UnregisterHotKey(this.Handle, HOTKEY_F1);
            UnregisterHotKey(this.Handle, HOTKEY_F2);
            _trayIcon?.Dispose();
            base.OnFormClosing(e);
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            using (var mutex = new Mutex(true, "PixelBoom_Single", out bool created))
            {
                if (!created) { MessageBox.Show("Zaten calisiyor!"); return; }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
        }
    }
}
