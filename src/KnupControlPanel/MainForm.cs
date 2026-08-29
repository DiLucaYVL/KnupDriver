using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using Microsoft.Win32;

namespace EmuladorKnup360
{
    public class MainForm : Form
    {
        private EmulatorService emulator;
        private ButtonMapping config;
        private string? currentlyMapping = null;
        private Label statusLabel;
        private Label headerStatus;
        private TrackBar vibTrackBar;
        private Panel leftStickPanel;
        private Panel rightStickPanel;
        private Label lblDUp, lblDDown, lblDLeft, lblDRight;
        private System.Windows.Forms.Timer uiTimer;
        private Dictionary<string, Button> mapButtons = new();
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private CheckBox chkAutoStart;
        private CheckBox chkHidHide;

        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "KnupXbox360Driver";

        public MainForm(bool startMinimized = false)
        {
            this.Text = "Knup 360 Driver & Painel de Controle";
            this.Size = new Size(520, 880);
            this.MinimumSize = new Size(500, 800);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.AutoScroll = true;
            this.Icon = SystemIcons.Application;

            config = ConfigManager.Load();

            headerStatus = new Label
            {
                Location = new Point(10, 10),
                Width = 480,
                Height = 35,
                BackColor = Color.FromArgb(230, 126, 34),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "⚠ Aguardando conexão do controle na USB...",
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            this.Controls.Add(headerStatus);

            statusLabel = new Label
            {
                Location = new Point(10, 790),
                Width = 480,
                ForeColor = Color.DarkSlateGray,
                Height = 35,
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            leftStickPanel = new Panel();
            rightStickPanel = new Panel();
            vibTrackBar = new TrackBar();
            uiTimer = new System.Windows.Forms.Timer { Interval = 30 };

            lblDUp = new Label();
            lblDDown = new Label();
            lblDLeft = new Label();
            lblDRight = new Label();
            chkAutoStart = new CheckBox();
            chkHidHide = new CheckBox();

            // Configuração do Tray Icon (Bandeja)
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Abrir Painel", null, (s, e) => ShowFromTray());
            trayMenu.Items.Add("Ocultar Painel", null, (s, e) => HideToTray());
            trayMenu.Items.Add("-");
            trayMenu.Items.Add("Sair do Driver", null, (s, e) => ExitApplication());

            trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Shield,
                ContextMenuStrip = trayMenu,
                Text = "Knup 360 Driver",
                Visible = true
            };
            trayIcon.DoubleClick += (s, e) => ShowFromTray();

            emulator = new EmulatorService(config, enableVirtualXbox: false);
            emulator.OnLog += msg =>
            {
                if (this.IsHandleCreated)
                    this.BeginInvoke((Action)(() => statusLabel.Text = msg));
            };
            emulator.OnConnectionChanged += connected =>
            {
                if (this.IsHandleCreated)
                {
                    this.BeginInvoke((Action)(() =>
                    {
                        if (connected)
                        {
                            headerStatus.BackColor = Color.FromArgb(39, 174, 96);
                            headerStatus.Text = "✔ Controle Conectado → Monitorando Entradas & Configuração";
                            if (trayIcon != null) trayIcon.Text = "Knup 360 Painel: Conectado";
                        }
                        else
                        {
                            headerStatus.BackColor = Color.FromArgb(230, 126, 34);
                            headerStatus.Text = "⚠ Aguardando conexão do controle na USB...";
                            if (trayIcon != null) trayIcon.Text = "Knup 360 Painel: Desconectado";
                        }
                    }));
                }
            };

            emulator.OnJoystickButtonReady += OnJoystickButtonPressed;

            InitializeUI();

            this.Load += (s, e) =>
            {
                emulator.Start();
                uiTimer.Start();

                if (startMinimized)
                {
                    this.WindowState = FormWindowState.Minimized;
                    this.ShowInTaskbar = false;
                    this.Hide();
                }
            };
        }

        private void InitializeUI()
        {
            int y = 55;
            var anchorLR = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;

            // ── Seção: Botões Principais ─────────────────────────────────
            var grpMain = new GroupBox { Text = "Botões Principais (Xbox 360)", Location = new Point(10, y), Width = 480, Height = 240, Anchor = anchorLR };
            this.Controls.Add(grpMain);

            string[] col1Names = { "A", "B", "X", "Y" };
            string[] col2Names = { "LB", "RB", "LT", "RT" };
            string[] col3Names = { "Back", "Start", "L3", "R3" };

            int bRow = 20;
            for (int i = 0; i < 4; i++)
            {
                AddMapRow(grpMain, col1Names[i], 10,  bRow);
                AddMapRow(grpMain, col2Names[i], 165, bRow);
                AddMapRow(grpMain, col3Names[i], 320, bRow);
                bRow += 50;
            }

            y += 250;

            // ── Seção: D-Pad / Setas ─────────────────────────────────────
            var grpDpad = new GroupBox { Text = "D-Pad / Setas Direcionais (Automático)", Location = new Point(10, y), Width = 480, Height = 95, Anchor = anchorLR };
            this.Controls.Add(grpDpad);

            lblDUp    = CreateDpadIndicator("↑ Cima", 15, 25);
            lblDDown  = CreateDpadIndicator("↓ Baixo", 130, 25);
            lblDLeft  = CreateDpadIndicator("← Esq", 245, 25);
            lblDRight = CreateDpadIndicator("→ Dir", 360, 25);

            grpDpad.Controls.AddRange(new Control[] { lblDUp, lblDDown, lblDLeft, lblDRight });

            var lblDpadHint = new Label
            {
                Text = "D-Pad lido automaticamente. Certifique-se de que o LED 'ANALOG' do controle está ACESO.",
                Location = new Point(15, 65),
                Width = 450,
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 8, FontStyle.Italic)
            };
            grpDpad.Controls.Add(lblDpadHint);

            y += 105;

            // ── Seção: Analógicos ─────────────────────────────────────────
            var grpAnalog = new GroupBox { Text = "Analógicos (Visualização em Tempo Real)", Location = new Point(10, y), Width = 480, Height = 165, Anchor = anchorLR };
            this.Controls.Add(grpAnalog);

            var lblL = new Label { Text = "Analógico Esquerdo", Location = new Point(40, 18), Width = 140, TextAlign = ContentAlignment.MiddleCenter };
            leftStickPanel = new Panel { Location = new Point(40, 38), Width = 140, Height = 115, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White };
            leftStickPanel.Paint += (s, e) => DrawStick(e.Graphics, leftStickPanel, emulator.LeftX, emulator.LeftY);

            var lblR = new Label { Text = "Analógico Direito", Location = new Point(295, 18), Width = 140, TextAlign = ContentAlignment.MiddleCenter };
            rightStickPanel = new Panel { Location = new Point(295, 38), Width = 140, Height = 115, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White };
            rightStickPanel.Paint += (s, e) => DrawStick(e.Graphics, rightStickPanel, emulator.RightX, emulator.RightY);

            grpAnalog.Controls.AddRange(new Control[] { lblL, leftStickPanel, lblR, rightStickPanel });

            y += 175;

            // ── Seção: Vibração ───────────────────────────────────────────
            var grpVib = new GroupBox { Text = "Motor de Vibração (Deslize para testar ou clique em Testar)", Location = new Point(10, y), Width = 480, Height = 70, Anchor = anchorLR };
            this.Controls.Add(grpVib);

            vibTrackBar = new TrackBar { Location = new Point(10, 22), Width = 310, Minimum = 0, Maximum = 255, TickFrequency = 32, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            vibTrackBar.Scroll += (s, e) => emulator.SendVibration((byte)vibTrackBar.Value);
            vibTrackBar.MouseUp += (s, e) => { vibTrackBar.Value = 0; emulator.SendVibration(0); };
            grpVib.Controls.Add(vibTrackBar);

            var btnTestVib = new Button
            {
                Text = "⚡ Testar (1s)",
                Location = new Point(330, 20),
                Width = 135,
                Height = 35,
                BackColor = Color.FromArgb(142, 68, 173),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnTestVib.Click += async (s, e) =>
            {
                btnTestVib.Enabled = false;
                emulator.SendVibration(255);
                await System.Threading.Tasks.Task.Delay(1000);
                emulator.SendVibration(0);
                btnTestVib.Enabled = true;
            };
            grpVib.Controls.Add(btnTestVib);

            y += 80;


            // ── Configurações de Sistema & Status do Serviço ───────────────
            var grpDriver = new GroupBox { Text = "Serviço do Driver & Configurações", Location = new Point(10, y), Width = 480, Height = 175, Anchor = anchorLR };
            this.Controls.Add(grpDriver);

            var lblServiceStatus = new Label
            {
                Location = new Point(15, 22),
                Width = 450,
                Height = 22,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Text = "Verificando serviço do driver..."
            };
            grpDriver.Controls.Add(lblServiceStatus);

            void UpdateServiceStatusText()
            {
                try
                {
                    using var sc = new System.ServiceProcess.ServiceController("KnupDriverService");
                    var st = sc.Status;
                    if (st == System.ServiceProcess.ServiceControllerStatus.Running)
                    {
                        lblServiceStatus.ForeColor = Color.FromArgb(39, 174, 96);
                        lblServiceStatus.Text = "✔ Serviço de Driver em Segundo Plano: ATIVO (Executando)";
                    }
                    else
                    {
                        lblServiceStatus.ForeColor = Color.FromArgb(230, 126, 34);
                        lblServiceStatus.Text = $"⚠ Serviço de Driver: {st}";
                    }
                }
                catch
                {
                    lblServiceStatus.ForeColor = Color.Gray;
                    lblServiceStatus.Text = "ℹ Modo Standalone (Serviço não instalado ou painel autônomo)";
                }
            }
            UpdateServiceStatusText();

            var btnRestartService = new Button
            {
                Text = "🔄 Reiniciar Driver",
                Location = new Point(15, 48),
                Width = 140,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8, FontStyle.Bold)
            };
            btnRestartService.Click += (s, e) =>
            {
                try
                {
                    using var sc = new System.ServiceProcess.ServiceController("KnupDriverService");
                    if (sc.Status == System.ServiceProcess.ServiceControllerStatus.Running)
                    {
                        sc.Stop();
                        sc.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(5));
                    }
                    sc.Start();
                    sc.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Running, TimeSpan.FromSeconds(5));
                    UpdateServiceStatusText();
                    MessageBox.Show("Serviço do driver reiniciado com sucesso!", "Driver", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Não foi possível reiniciar o serviço: " + ex.Message + "\n(Dica: Execute o painel como Administrador se necessário)", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            grpDriver.Controls.Add(btnRestartService);

            chkHidHide = new CheckBox
            {
                Text = emulator.IsHidHideAvailable
                    ? "🙈 Ocultar controle físico dos jogos (HidHide)"
                    : "🙈 HidHide não instalado",
                Location = new Point(165, 50),
                Width = 300,
                Height = 24,
                Enabled = emulator.IsHidHideAvailable
            };
            chkHidHide.CheckedChanged += (s, e) =>
            {
                if (chkHidHide.Checked) emulator.EnableHidHide();
                else emulator.DisableHidHide();
            };
            grpDriver.Controls.Add(chkHidHide);

            var saveBtn = new Button
            {
                Text = "💾 Salvar Configurações",
                Location = new Point(15, 85),
                Width = 210,
                Height = 35,
                BackColor = Color.FromArgb(41, 128, 185),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            saveBtn.Click += (s, e) =>
            {
                ConfigManager.Save(config);
                MessageBox.Show("Configurações salvas!\nO serviço em segundo plano atualiza o mapeamento automaticamente.", "Salvo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            grpDriver.Controls.Add(saveBtn);

            var minimizeBtn = new Button
            {
                Text = "🔽 Fechar / Segundo Plano",
                Location = new Point(235, 85),
                Width = 230,
                Height = 35,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Regular)
            };
            minimizeBtn.Click += (s, e) => this.Close();
            grpDriver.Controls.Add(minimizeBtn);

            var lblInfo = new Label
            {
                Text = "💡 Dica: O driver roda como serviço do Windows. Você não precisa deixar este painel aberto para jogar!",
                Location = new Point(15, 128),
                Width = 450,
                Height = 40,
                ForeColor = Color.DimGray,
                Font = new Font("Segoe UI", 8, FontStyle.Italic)
            };
            grpDriver.Controls.Add(lblInfo);

            y += 185;

            this.Controls.Add(statusLabel);


            // Timer para atualizar sticks, D-Pad e botões em tempo real
            uiTimer.Tick += (s, e) =>
            {
                if (!this.Visible) return;

                leftStickPanel.Invalidate();
                rightStickPanel.Invalidate();

                UpdateDpadIndicator(lblDUp, emulator.DPadUp);
                UpdateDpadIndicator(lblDDown, emulator.DPadDown);
                UpdateDpadIndicator(lblDLeft, emulator.DPadLeft);
                UpdateDpadIndicator(lblDRight, emulator.DPadRight);

                var btns = emulator.ButtonStates;
                foreach (var kvp in mapButtons)
                {
                    string key = kvp.Key;
                    Button btn = kvp.Value;

                    if (currentlyMapping == key) continue;

                    if (config.Buttons.TryGetValue(key, out int btnId) && btnId < btns.Length && btns[btnId])
                    {
                        btn.BackColor = Color.FromArgb(46, 204, 113);
                        btn.ForeColor = Color.White;
                    }
                    else
                    {
                        btn.BackColor = Color.FromArgb(245, 245, 245);
                        btn.ForeColor = Color.Black;
                    }
                }
            };
        }

        private bool _allowVisible = true;

        protected override void SetVisibleCore(bool value)
        {
            if (!_allowVisible)
            {
                value = false;
                if (!this.IsHandleCreated) CreateHandle();
            }
            base.SetVisibleCore(value);
        }

        private void ShowFromTray()
        {
            _allowVisible = true;
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
            this.BringToFront();
            this.Activate();
        }

        private void HideToTray()
        {
            _allowVisible = false;
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.Hide();
            trayIcon.ShowBalloonTip(1500, "Knup 360 Driver", "O driver está rodando em segundo plano. Clique aqui para abrir.", ToolTipIcon.Info);
        }

        private void ExitApplication()
        {
            _allowVisible = true;
            trayIcon.Visible = false;
            try { uiTimer?.Stop(); } catch { }
            try { emulator?.Dispose(); } catch { }
            try { trayIcon?.Dispose(); } catch { }
            Application.Exit();
        }

        private bool IsAutoStartEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
                if (key?.GetValue(AppName) != null) return true;
            }
            catch { }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = "/Query /TN \"Knup360Driver\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(1000);
                return proc?.ExitCode == 0;
            }
            catch { return false; }
        }

        private void SetAutoStart(bool enable)
        {
            string exe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (string.IsNullOrEmpty(exe)) return;

            if (enable)
            {
                // 1. Task Scheduler com privilégios de Administrador (RL HIGHEST) - Inicialização 100% silenciosa sem prompt UAC
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = $"/Create /TN \"Knup360Driver\" /TR \"\\\"{exe}\\\" --minimized\" /SC ONLOGON /RL HIGHEST /F",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(psi);
                    proc?.WaitForExit(3000);
                }
                catch { }

                // 2. Registro HKCU Run como fallback
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                    key?.SetValue(AppName, $"\"{exe}\" --minimized");
                }
                catch { }
            }
            else
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = "/Delete /TN \"Knup360Driver\" /F",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(psi);
                    proc?.WaitForExit(3000);
                }
                catch { }

                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                    key?.DeleteValue(AppName, false);
                }
                catch { }
            }
        }

        private Label CreateDpadIndicator(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                Width = 100,
                Height = 32,
                TextAlign = ContentAlignment.MiddleCenter,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(240, 240, 240),
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
        }

        private void UpdateDpadIndicator(Label lbl, bool active)
        {
            if (active)
            {
                lbl.BackColor = Color.FromArgb(46, 204, 113);
                lbl.ForeColor = Color.White;
            }
            else
            {
                lbl.BackColor = Color.FromArgb(240, 240, 240);
                lbl.ForeColor = Color.Black;
            }
        }

        private void AddMapRow(GroupBox parent, string key, int x, int y)
        {
            var lbl = new Label { Text = key + ":", Location = new Point(x, y + 3), Width = 45, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            var btn = new Button
            {
                Location = new Point(x + 45, y),
                Width = 95,
                Height = 28,
                Tag = key,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(245, 245, 245),
                Font = new Font("Segoe UI", 9, FontStyle.Regular)
            };
            btn.FlatAppearance.BorderColor = Color.LightGray;
            btn.Text = config.Buttons.ContainsKey(key) ? "Btn " + config.Buttons[key] : "Mapear";
            btn.Click += OnMapButtonClick;
            mapButtons[key] = btn;
            parent.Controls.Add(lbl);
            parent.Controls.Add(btn);
        }

        private void DrawStick(Graphics g, Panel panel, int x, int y)
        {
            g.Clear(Color.White);
            int cx = panel.Width / 2;
            int cy = panel.Height / 2;
            int radius = (panel.Height / 2) - 8;

            g.DrawEllipse(Pens.Gray, cx - radius, cy - radius, radius * 2, radius * 2);
            g.DrawLine(Pens.LightGray, cx - radius, cy, cx + radius, cy);
            g.DrawLine(Pens.LightGray, cx, cy - radius, cx, cy + radius);

            float nx = Math.Clamp((x - 32767f) / 32767f, -1f, 1f);
            float ny = Math.Clamp((y - 32767f) / 32767f, -1f, 1f);
            int dotX = cx + (int)(nx * radius);
            int dotY = cy + (int)(ny * radius);

            g.FillEllipse(Brushes.DodgerBlue, dotX - 7, dotY - 7, 14, 14);
            g.DrawEllipse(Pens.DarkBlue, dotX - 7, dotY - 7, 14, 14);
        }

        private void OnMapButtonClick(object? sender, EventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                currentlyMapping = tag;
                btn.Text = "⏳ Aperte...";
                emulator.IsWaitingForMap = true;
            }
        }

        private void OnJoystickButtonPressed(int buttonId)
        {
            if (currentlyMapping == null) return;
            string mapping = currentlyMapping;
            config.Buttons[mapping] = buttonId;
            emulator.Config = config;
            currentlyMapping = null;

            this.BeginInvoke((Action)(() =>
            {
                if (mapButtons.TryGetValue(mapping, out var btn))
                    btn.Text = "Btn " + buttonId;
            }));
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Ao fechar a janela (clique no X), apenas esconde na bandeja e continua rodando como driver
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }

            try { uiTimer?.Stop(); } catch { }
            try { emulator?.Dispose(); } catch { }
            try { trayIcon?.Dispose(); } catch { }
            base.OnFormClosing(e);
        }
    }
}
