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
            this.Size = new Size(800, 800);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(45, 45, 48);
            this.ForeColor = Color.White;

            int yPos = 20;
            int labelWidth = 260;
            int controlWidth = 120;
            int padding = 10;

            // Browser processes settings
            var grpBrowser = new GroupBox
            {
                Text = "Процессы браузера (zbe1)",
                Location = new Point(10, yPos),
                Size = new Size(390, 90),
                ForeColor = Color.White
            };
            this.Controls.Add(grpBrowser);
            yPos = 25;

            var lblMaxMem = CreateLabel("Макс память (MB):", 20, yPos, labelWidth);
            numMaxMemInstance = CreateNumeric(20 + labelWidth + padding, yPos, controlWidth, 100, 10000, 100);
            grpBrowser.Controls.Add(lblMaxMem);
            grpBrowser.Controls.Add(numMaxMemInstance);
            yPos += 30;

            var lblMaxAge = CreateLabel("Макс возраст (мин):", 20, yPos, labelWidth);
            numMaxAgeInstance = CreateNumeric(20 + labelWidth + padding, yPos, controlWidth, 5, 1440, 5);
            grpBrowser.Controls.Add(lblMaxAge);
            grpBrowser.Controls.Add(numMaxAgeInstance);

            // Main process settings
            yPos = 130;
            var grpMain = new GroupBox
            {
                Text = "Главный процесс (ZennoPoster)",
                Location = new Point(10, yPos),
                Size = new Size(390, 60),
                ForeColor = Color.White
            };
            this.Controls.Add(grpMain);
            yPos = 25;

            var lblMaxMemZP = CreateLabel("Макс память (MB):", 20, yPos, labelWidth);
            numMaxMemZP = CreateNumeric(20 + labelWidth + padding, yPos, controlWidth, 1000, 100000, 1000);
            grpMain.Controls.Add(lblMaxMemZP);
            grpMain.Controls.Add(numMaxMemZP);

            // Kill flags
            yPos = 210;
            var grpKill = new GroupBox
            {
                Text = "Параметры завершения",
                Location = new Point(10, yPos),
                Size = new Size(390, 110),
                ForeColor = Color.White
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
            this.Controls.Add(lblAutoCheck);
            this.Controls.Add(numAutoCheckInterval);
            yPos += 30;

            chkShowLogs = CreateCheckBox("Показывать логи", 20, yPos);
            this.Controls.Add(chkShowLogs);
            yPos += 30;

            chkShowRawCommandLine = CreateCheckBox("Показывать полную командную строку (сырую)", 20, yPos);
            this.Controls.Add(chkShowRawCommandLine);
            yPos += 35;

            // Buttons
            btnSave = new Button
            {
                Text = "Сохранить",
                Location = new Point(210, yPos),
                Size = new Size(90, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White
            };
            btnSave.Click += BtnSave_Click;
            this.Controls.Add(btnSave);

            btnCancel = new Button
            {
                Text = "Отмена",
                Location = new Point(310, yPos),
                Size = new Size(90, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };
            btnCancel.Click += (s, e) => this.DialogResult = DialogResult.Cancel;
            this.Controls.Add(btnCancel);
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
                Size = new Size(350, 20),
                ForeColor = Color.White
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