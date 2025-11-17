using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using OtpNet;

namespace OtpTrayApp
{
    public class OtpTrayContext : ApplicationContext
    {
        private NotifyIcon trayIcon;
        private OtpInputForm? currentForm;
        private ProcessStatsForm? statsForm;
        private AppSettings settings;
        private System.Windows.Forms.Timer? autoCheckTimer;
        private ResourceMonitor? resourceMonitor;

        public OtpTrayContext()
        {
            settings = AppSettings.Load();
            
            trayIcon = new NotifyIcon()
            {
                Icon = CreateIcon(),
                ContextMenuStrip = CreateContextMenu(),
                Visible = true,
                Text = "z3nTray"
            };

            trayIcon.MouseClick += TrayIcon_Click;

            if (settings.AutoCheckInterval > 0)
            {
                StartAutoCheck();
            }

            // Start resource monitoring if enabled
            if (settings.EnableResourceMonitoring)
            {
                StartResourceMonitoring();
            }
        }

        private void TrayIcon_Click(object? sender, MouseEventArgs e)
        {
            // Показываем форму только по левому клику
            if (e.Button == MouseButtons.Left)
            {
                ShowInputDialog();
            }
        }

        private ContextMenuStrip CreateContextMenu()
        {
            var menu = new ContextMenuStrip();
            var generateItem = new ToolStripMenuItem("New OTP");
            var statsItem = new ToolStripMenuItem("Show ZP processes");
            var killItem = new ToolStripMenuItem("Check & Kill");
            var settingsItem = new ToolStripMenuItem("Settings");
            var exitItem = new ToolStripMenuItem("Exit");

            generateItem.Click += (s, e) => ShowInputDialog();
            statsItem.Click += (s, e) => ShowProcessStats();
            killItem.Click += (s, e) => CheckAndKillNow();
            settingsItem.Click += (s, e) => ShowSettings();
            exitItem.Click += Exit_Click;

            menu.Items.Add(generateItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(statsItem);
            menu.Items.Add(killItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(settingsItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);

            return menu;
        }
        #region OTP
        private void ShowInputDialog()
        {
            if (currentForm != null && !currentForm.IsDisposed)
            {
                currentForm.Activate();
                return;
            }

            currentForm = new OtpInputForm();
            PositionFormNearTray(currentForm);
    
            var result = currentForm.ShowDialog();
            string keyString = currentForm.OtpKey; // Получаем ключ ДО того как форма закроется
    
            currentForm = null;
    
            if (result == DialogResult.OK)
            {
                try
                {
                    string code = GenerateOtp(keyString);
                    Clipboard.SetText(code);
                    trayIcon.ShowBalloonTip(2000, "OTP код скопирован", 
                        $"Код {code} скопирован в буфер обмена", 
                        ToolTipIcon.Info);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка генерации OTP: {ex.Message}", 
                        "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        public static string GenerateOtp(string keyString, int waitIfTimeLess = 5)
        {
            if (string.IsNullOrEmpty(keyString))
                throw new Exception($"invalid input:[{keyString}]");

            var key = Base32Encoding.ToBytes(keyString.Trim());
            var otp = new Totp(key);
            string code = otp.ComputeTotp();
            int remainingSeconds = otp.RemainingSeconds();
            
            if (remainingSeconds <= waitIfTimeLess)
            {
                Thread.Sleep(remainingSeconds * 1000 + 1);
                code = otp.ComputeTotp();
            }
            
            return code;
        }    
        
        #endregion
        
        #region ProcessManager

        private void ShowProcessStats()
        {
            if (statsForm != null && !statsForm.IsDisposed)
            {
                statsForm.Activate();
                return;
            }

            statsForm = new ProcessStatsForm(settings);
            statsForm.FormClosed += (s, e) => statsForm = null;
            statsForm.Show();
        }

        private void CheckAndKillNow()
        {
            try
            {
                var result = ProcessManager.KillProcesses(settings);

                // Create checkpoint if ZennoPoster was killed
                if (result.KilledMain > 0 && resourceMonitor != null)
                {
                    resourceMonitor.CreateCheckpoint("ZennoPosterKilled");
                }

                if (settings.ShowLogs)
                {
                    var message = string.Join("\n", result.Messages);
                    message += $"\n\nУбито по времени: {result.KilledByTime}";
                    message += $"\nУбито по памяти: {result.KilledByMem}";
                    message += $"\nУбито главных: {result.KilledMain}";
                    
                    //trayIcon.ShowBalloonTip(5000, "Result", 
                       // message, ToolTipIcon.Info);

                    MessageBox.Show(message, "Результат Check & Kill",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    // Silent mode - only show balloon tip
                    int total = result.KilledByTime + result.KilledByMem + result.KilledMain;
                    if (total > 0)
                    {
                        trayIcon.ShowBalloonTip(2000, "Процессы завершены",
                            $"Убито процессов: {total}",
                            ToolTipIcon.Info);
                    }
                }
            }
            catch (Exception ex)
            {
                if (settings.ShowLogs)
                {
                    MessageBox.Show($"Ошибка выполнения Check & Kill: {ex.Message}",
                        "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ShowSettings()
        {
            var settingsForm = new SettingsForm(settings);
            if (settingsForm.ShowDialog() == DialogResult.OK)
            {
                settings = settingsForm.Settings;

                // Update auto-check timer
                if (settings.AutoCheckInterval > 0)
                {
                    StartAutoCheck();
                }
                else
                {
                    StopAutoCheck();
                }

                // Update resource monitoring
                if (settings.EnableResourceMonitoring)
                {
                    StartResourceMonitoring();
                }
                else
                {
                    StopResourceMonitoring();
                }

                trayIcon.ShowBalloonTip(1000, "Настройки сохранены",
                    "Параметры менеджера процессов обновлены",
                    ToolTipIcon.Info);
            }
        }

        #endregion
        
        #region Auto-Check Timer

        private void StartAutoCheck()
        {
            StopAutoCheck(); // Stop existing timer if any

            autoCheckTimer = new System.Windows.Forms.Timer();
            autoCheckTimer.Interval = settings.AutoCheckInterval * 60 * 1000; // minutes to milliseconds
            autoCheckTimer.Tick += (s, e) => CheckAndKillNow();
            autoCheckTimer.Start();
        }

        private void StopAutoCheck()
        {
            if (autoCheckTimer != null)
            {
                autoCheckTimer.Stop();
                autoCheckTimer.Dispose();
                autoCheckTimer = null;
            }
        }

        #endregion

        #region Resource Monitoring

        private void StartResourceMonitoring()
        {
            StopResourceMonitoring(); // Stop existing monitor if any

            resourceMonitor = new ResourceMonitor();
            resourceMonitor.Start(settings.ResourceMonitoringIntervalMinutes);
        }

        private void StopResourceMonitoring()
        {
            if (resourceMonitor != null)
            {
                resourceMonitor.Stop();
                resourceMonitor.Dispose();
                resourceMonitor = null;
            }
        }

        #endregion


        #region UI
        private void PositionFormNearTray(Form form)
        {
            // Получаем позицию курсора (где находится иконка в трее)
            var cursorPosition = Cursor.Position;
            
            // Получаем размеры экрана
            var screen = Screen.FromPoint(cursorPosition);
            var workingArea = screen.WorkingArea;
            
            // Рассчитываем позицию формы
            int x = cursorPosition.X - form.Width / 2;
            int y = workingArea.Bottom - form.Height - 10;
            
            // Корректируем если форма выходит за границы экрана
            if (x + form.Width > workingArea.Right)
                x = workingArea.Right - form.Width;
            if (x < workingArea.Left)
                x = workingArea.Left;
                
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(x, y);
        }
        private void Exit_Click(object? sender, EventArgs e)
        {
            // Stop resource monitoring before exit
            StopResourceMonitoring();

            trayIcon.Visible = false;
            Application.Exit();
        }
        private Icon CreateIcon()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream("icon.ico"))
                {
                    if (stream != null)
                    {
                        return new Icon(stream);
                    }
                }
            }
            catch { }

            // Fallback - создаем простую иконку
            Bitmap bitmap = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Black);
                g.FillEllipse(Brushes.White, 2, 2, 12, 12);
                g.DrawString("Z", new Font("Arial", 8, FontStyle.Bold), 
                    Brushes.Red, new PointF(3, 1));
            }
            return Icon.FromHandle(bitmap.GetHicon());
        }
        #endregion
    }
}
