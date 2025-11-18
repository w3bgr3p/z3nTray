using System;
using System.Drawing;
using System.Windows.Forms;

namespace OtpTrayApp
{
    public class SettingsForm : Form
    {
        private NumericUpDown numMaxMemInstance;
        private NumericUpDown numMaxAgeInstance;
        private NumericUpDown numMaxMemZP;
        private NumericUpDown numAutoCheckInterval;
        private CheckBox chkKillOld;
        private CheckBox chkKillHeavy;
        private CheckBox chkKillMain;
        private CheckBox chkShowLogs;
        private CheckBox chkShowRawCommandLine;
        private CheckBox chkEnableResourceMonitoring;
        private NumericUpDown numResourceMonitoringInterval;
        private Button btnSave;
        private Button btnCancel;

        public AppSettings Settings { get; private set; }

        public SettingsForm(AppSettings currentSettings)
        {
            Settings = currentSettings.Clone();
            InitializeComponents();
            LoadSettings();
        }

        private void InitializeComponents()
        {
            this.Text = "Настройки менеджера процессов";
            this.Size = new Size(450, 550);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;

            int yPos = 20;
            int labelWidth = 260;
            int controlWidth = 120;
            int padding = 10;
            int margin = 10;

            // Calculate dynamic width based on form client size
            int groupBoxWidth = this.ClientSize.Width - margin * 2;

            // Browser processes settings
            var grpBrowser = new GroupBox
            {
                Text = "Процессы браузера (zbe1)",
                Location = new Point(margin, yPos),
                Size = new Size(groupBoxWidth, 90),
                ForeColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            this.Controls.Add(grpBrowser);
            yPos = 25;

            var lblMaxMem = CreateLabel("Макс память (MB):", 20, yPos, labelWidth);
            numMaxMemInstance = CreateNumeric(20 + labelWidth + padding, yPos, controlWidth, 100, 10000, 100);
            numMaxMemInstance.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            grpBrowser.Controls.Add(lblMaxMem);
            grpBrowser.Controls.Add(numMaxMemInstance);
            yPos += 30;

            var lblMaxAge = CreateLabel("Макс возраст (мин):", 20, yPos, labelWidth);
            numMaxAgeInstance = CreateNumeric(20 + labelWidth + padding, yPos, controlWidth, 5, 1440, 5);
            numMaxAgeInstance.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            grpBrowser.Controls.Add(lblMaxAge);
            grpBrowser.Controls.Add(numMaxAgeInstance);

            // Main process settings
            yPos = 130;
            var grpMain = new GroupBox
            {
                Text = "Главный процесс (ZennoPoster)",
                Location = new Point(margin, yPos),
                Size = new Size(groupBoxWidth, 60),
                ForeColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            this.Controls.Add(grpMain);
            yPos = 25;

            var lblMaxMemZP = CreateLabel("Макс память (MB):", 20, yPos, labelWidth);
            numMaxMemZP = CreateNumeric(20 + labelWidth + padding, yPos, controlWidth, 1000, 100000, 1000);
            numMaxMemZP.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            grpMain.Controls.Add(lblMaxMemZP);
            grpMain.Controls.Add(numMaxMemZP);

            // Kill flags
            yPos = 210;
            var grpKill = new GroupBox
            {
                Text = "Параметры завершения",
                Location = new Point(margin, yPos),
                Size = new Size(groupBoxWidth, 110),
                ForeColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            this.Controls.Add(grpKill);
            yPos = 25;

            chkKillOld = CreateCheckBox("Убивать старые процессы", 20, yPos);
            grpKill.Controls.Add(chkKillOld);
            yPos += 25;

            chkKillHeavy = CreateCheckBox("Убивать тяжелые процессы", 20, yPos);
            grpKill.Controls.Add(chkKillHeavy);
            yPos += 25;

            chkKillMain = CreateCheckBox("Убивать главный процесс (ОПАСНО!)", 20, yPos);
            chkKillMain.ForeColor = Color.FromArgb(255, 100, 100);
            grpKill.Controls.Add(chkKillMain);

            // Auto-check and UI settings
            yPos = 340;
            var lblAutoCheck = CreateLabel("Автопроверка (мин, 0 = выкл):", 20, yPos, labelWidth);
            numAutoCheckInterval = CreateNumeric(20 + labelWidth + padding, yPos, controlWidth, 0, 1440, 1);
            numAutoCheckInterval.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.Controls.Add(lblAutoCheck);
            this.Controls.Add(numAutoCheckInterval);
            yPos += 30;

            chkShowLogs = CreateCheckBox("Показывать логи", 20, yPos);
            this.Controls.Add(chkShowLogs);
            yPos += 30;

            chkShowRawCommandLine = CreateCheckBox("Показывать полную командную строку (сырую)", 20, yPos);
            this.Controls.Add(chkShowRawCommandLine);
            yPos += 30;

            chkEnableResourceMonitoring = CreateCheckBox("Включить мониторинг ресурсов (для отладки утечек памяти)", 20, yPos);
            this.Controls.Add(chkEnableResourceMonitoring);
            yPos += 30;

            var lblResourceInterval = CreateLabel("Интервал мониторинга (мин):", 20, yPos, labelWidth);
            numResourceMonitoringInterval = CreateNumeric(20 + labelWidth + padding, yPos, controlWidth, 1, 60, 1);
            numResourceMonitoringInterval.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.Controls.Add(lblResourceInterval);
            this.Controls.Add(numResourceMonitoringInterval);
            yPos += 50;

            // Buttons - positioned from the right edge
            int buttonWidth = 90;
            int buttonSpacing = 10;

            btnCancel = new Button
            {
                Text = "Отмена",
                Location = new Point(this.ClientSize.Width - buttonWidth - margin, yPos),
                Size = new Size(buttonWidth, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnCancel.Click += (s, e) => this.DialogResult = DialogResult.Cancel;
            this.Controls.Add(btnCancel);

            btnSave = new Button
            {
                Text = "Сохранить",
                Location = new Point(this.ClientSize.Width - buttonWidth * 2 - buttonSpacing - margin, yPos),
                Size = new Size(buttonWidth, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnSave.Click += BtnSave_Click;
            this.Controls.Add(btnSave);
        }

        private Label CreateLabel(string text, int x, int y, int width)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 20),
                ForeColor = Color.White
            };
        }

        private NumericUpDown CreateNumeric(int x, int y, int width, int min, int max, int increment)
        {
            return new NumericUpDown
            {
                Location = new Point(x, y),
                Size = new Size(width, 20),
                Minimum = min,
                Maximum = max,
                Increment = increment
            };
        }

        private CheckBox CreateCheckBox(string text, int x, int y)
        {
            return new CheckBox
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(this.ClientSize.Width - x - 30, 20),
                ForeColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
        }

        private void LoadSettings()
        {
            numMaxMemInstance.Value = Settings.MaxMemoryForInstance;
            numMaxAgeInstance.Value = Settings.MaxAgeForInstance;
            numMaxMemZP.Value = Settings.MaxMemoryForZennoposter;
            numAutoCheckInterval.Value = Settings.AutoCheckInterval;
            chkKillOld.Checked = Settings.KillOld;
            chkKillHeavy.Checked = Settings.KillHeavy;
            chkKillMain.Checked = Settings.KillMain;
            chkShowLogs.Checked = Settings.ShowLogs;
            chkShowRawCommandLine.Checked = Settings.ShowRawCommandLine;
            chkEnableResourceMonitoring.Checked = Settings.EnableResourceMonitoring;
            numResourceMonitoringInterval.Value = Settings.ResourceMonitoringIntervalMinutes;
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            Settings.MaxMemoryForInstance = (int)numMaxMemInstance.Value;
            Settings.MaxAgeForInstance = (int)numMaxAgeInstance.Value;
            Settings.MaxMemoryForZennoposter = (int)numMaxMemZP.Value;
            Settings.AutoCheckInterval = (int)numAutoCheckInterval.Value;
            Settings.KillOld = chkKillOld.Checked;
            Settings.KillHeavy = chkKillHeavy.Checked;
            Settings.KillMain = chkKillMain.Checked;
            Settings.ShowLogs = chkShowLogs.Checked;
            Settings.ShowRawCommandLine = chkShowRawCommandLine.Checked;
            Settings.EnableResourceMonitoring = chkEnableResourceMonitoring.Checked;
            Settings.ResourceMonitoringIntervalMinutes = (int)numResourceMonitoringInterval.Value;

            try
            {
                Settings.Save();
                this.DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения настроек: {ex.Message}",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}