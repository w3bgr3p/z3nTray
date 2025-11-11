using System;
using System.Drawing;
using System.Windows.Forms;

namespace OtpTrayApp
{
    public class ProcessStatsForm : Form
    {
        private RichTextBox txtStats;
        private Button btnRefresh;
        private Button btnKill;
        private Button btnClose;
        private Label lblStatus;
        private GroupBox grpInfo;
        private Label lblTotal;
        private Label lblLimitedByTime;
        private Label lblLimitedByMem;
        private Label lblWithBrowser;
        private Label lblNoBrowser;

        private AppSettings settings;

        public ProcessStatsForm(AppSettings settings)
        {
            this.settings = settings;
            InitializeComponents();
            
            this.Load += (s, e) => 
            {
                // Обновление запускается после отображения окна
                System.Threading.Tasks.Task.Run(() => 
                {
                    System.Threading.Thread.Sleep(100); // даем окну отрисоваться
                    this.Invoke((Action)RefreshStats);
                });
            };
            //RefreshStats();
        }

        private void InitializeComponents()
        {
            this.Text = "Статистика процессов ZennoPoster";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;

            // Info panel
            grpInfo = new GroupBox
            {
                Text = "Info",
                Location = new Point(10, 10),
                Size = new Size(770, 100),
                ForeColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            int xPos = 20;
            lblTotal = CreateInfoLabel("Total: 0", xPos, 25);
            lblWithBrowser = CreateInfoLabel("Known: 0", xPos, 50);
            lblNoBrowser = CreateInfoLabel("unKnown: 0", xPos, 75);

            xPos = 250;
            lblLimitedByTime = CreateInfoLabel("По времени: 0", xPos, 25);
            lblLimitedByMem = CreateInfoLabel("По памяти: 0", xPos, 50);

            grpInfo.Controls.Add(lblTotal);
            grpInfo.Controls.Add(lblWithBrowser);
            grpInfo.Controls.Add(lblNoBrowser);
            grpInfo.Controls.Add(lblLimitedByTime);
            grpInfo.Controls.Add(lblLimitedByMem);
            this.Controls.Add(grpInfo);

            // Stats text
            txtStats = new RichTextBox
            {
                Location = new Point(10, 120),
                Size = new Size(770, 370),  // уменьши до 370
                BackColor = Color.FromArgb(20, 20, 20),
                ForeColor = Color.LightGray,
                Font = new Font("Consolas", 9),
                ReadOnly = true,
                WordWrap = false,
                Text = "Loading...",
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom // ← ДОБАВЬ ЭТО
            };
            this.Controls.Add(txtStats);

            // Status
            lblStatus = new Label
            {
                Location = new Point(10, 510),
                Size = new Size(420, 20),
                ForeColor = Color.Gray,
                Text = "Готов",
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left // ← ДОБАВЬ ЭТО
            };
            this.Controls.Add(lblStatus);

            // Buttons
            btnRefresh = new Button
            {
                Text = "Refresh",
                Location = new Point(520, 510),
                Size = new Size(80, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                AutoSize = false,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnRefresh.Click += (s, e) => RefreshStats();
            this.Controls.Add(btnRefresh);

            btnKill = new Button
            {
                Text = "Kill",
                Location = new Point(610, 510),
                Size = new Size(80, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(200, 50, 50),
                ForeColor = Color.White,
                AutoSize = false,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnKill.Click += BtnKill_Click;
            this.Controls.Add(btnKill);

            btnClose = new Button
            {
                Text = "Close",
                Location = new Point(700, 510),
                Size = new Size(80, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnClose.Click += (s, e) => this.Close();
            this.Controls.Add(btnClose);
        }

        private Label CreateInfoLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(200, 20),
                ForeColor = Color.White
            };
        }

        private void RefreshStats()
        {
            try
            {
 lblStatus.Text = "Обновление...";
                lblStatus.ForeColor = Color.Yellow;
                Application.DoEvents();

                txtStats.Clear();

                var stats = ProcessManager.GetProcessStats(settings);

                // Update statistics
                lblTotal.Text = $"Total: {stats.All.Count}";
                lblWithBrowser.Text = $"Known: {stats.WithBrowser.Count}";
                lblWithBrowser.ForeColor = Color.FromArgb(100, 180, 255);
                lblNoBrowser.Text = $"unKnown: {stats.NoBrowser.Count}";
                lblNoBrowser.ForeColor = Color.Gray;
                lblLimitedByTime.Text = $"По времени: {stats.LimitedByTime.Count}";
                lblLimitedByTime.ForeColor = Color.FromArgb(255, 200, 100);
                lblLimitedByMem.Text = $"По памяти: {stats.LimitedByMem.Count}";
                lblLimitedByMem.ForeColor = Color.FromArgb(255, 150, 100);

                // Display logs
                AppendLog($"=== Обновлено: {DateTime.Now:HH:mm:ss} ===\n", Color.Cyan);
                AppendLog($"Total: {stats.All.Count}\n\n", Color.White);

                if (stats.LimitedByTime.Count > 0)
                {
                    AppendLog($"⏰ По времени ({stats.LimitedByTime.Count}):\n", Color.FromArgb(255, 200, 100));
                    foreach (var item in stats.LimitedByTime)
                        AppendLog($"  {item}\n", Color.FromArgb(255, 200, 100));
                    AppendLog("\n", Color.White);
                }

                if (stats.LimitedByMem.Count > 0)
                {
                    AppendLog($"💾 По памяти ({stats.LimitedByMem.Count}):\n", Color.FromArgb(255, 150, 100));
                    foreach (var item in stats.LimitedByMem)
                        AppendLog($"  {item}\n", Color.FromArgb(255, 150, 100));
                    AppendLog("\n", Color.White);
                }

                if (stats.WithBrowser.Count > 0)
                {
                    AppendLog($"🌐 Known ({stats.WithBrowser.Count}):\n", Color.FromArgb(100, 180, 255));
                    foreach (var item in stats.WithBrowser)
                        AppendLog($"  {item}\n", Color.FromArgb(100, 180, 255));
                    AppendLog("\n", Color.White);
                }

                if (stats.NoBrowser.Count > 0)
                {
                    AppendLog($"⚠ unKnown ({stats.NoBrowser.Count}):\n", Color.Gray);
                    foreach (var item in stats.NoBrowser)
                        AppendLog($"  {item}\n", Color.Gray);
                }

                lblStatus.Text = $"Обновлено: {DateTime.Now:HH:mm:ss}";
                lblStatus.ForeColor = Color.LimeGreen;
            }
            catch (Exception ex)
            {
                AppendLog($"\n❌ Ошибка: {ex.Message}\n", Color.Red);
                lblStatus.Text = "Ошибка обновления";
                lblStatus.ForeColor = Color.Red;
            }
        }

        private void AppendLog(string text, Color color)
        {
            txtStats.SelectionStart = txtStats.TextLength;
            txtStats.SelectionLength = 0;
            txtStats.SelectionColor = color;
            txtStats.AppendText(text);
            txtStats.SelectionColor = txtStats.ForeColor;
        }
        private void BtnKill_Click(object? sender, EventArgs e)
        {
            var result = MessageBox.Show(
                $"Убить процессы по настройкам?\n\n" +
                $"Макс память: {settings.MaxMemoryForInstance}MB\n" +
                $"Макс возраст: {settings.MaxAgeForInstance}мин\n" +
                $"Макс память ZP: {settings.MaxMemoryForZennoposter}MB\n\n" +
                $"Убить старые: {(settings.KillOld ? "ДА" : "НЕТ")}\n" +
                $"Убить тяжелые: {(settings.KillHeavy ? "ДА" : "НЕТ")}\n" +
                $"Убить главный: {(settings.KillMain ? "ДА" : "НЕТ")}",
                "Подтверждение",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (result == DialogResult.Yes)
            {
                ExecuteKiller();
            }
        }

        private void ExecuteKiller()
        {
            try
            {
                lblStatus.Text = "Убиваем процессы...";
                lblStatus.ForeColor = Color.Red;
                Application.DoEvents();

                AppendLog("\n" + new string('=', 50) + "\n", Color.Red);
                AppendLog("☠ ЗАПУСК КИЛЛЕРА\n", Color.Red);
                AppendLog(new string('=', 50) + "\n\n", Color.Red);

                var killResult = ProcessManager.KillProcesses(settings);

                foreach (var msg in killResult.Messages)
                {
                    AppendLog(msg + "\n", Color.White);
                }

                AppendLog($"\n✓ Киллер завершил работу\n", Color.LimeGreen);
                AppendLog($"Убито по времени: {killResult.KilledByTime}\n", Color.Yellow);
                AppendLog($"Убито по памяти: {killResult.KilledByMem}\n", Color.Orange);
                AppendLog($"Убито главных: {killResult.KilledMain}\n", Color.Red);

                lblStatus.Text = "Процессы убиты";
                lblStatus.ForeColor = Color.LimeGreen;

                // Refresh after 1 second
                System.Threading.Thread.Sleep(1000);
                RefreshStats();
            }
            catch (Exception ex)
            {
                AppendLog($"\n❌ Ошибка киллера: {ex.Message}\n", Color.Red);
                lblStatus.Text = "Ошибка выполнения";
                lblStatus.ForeColor = Color.Red;
            }
        }
    }
}