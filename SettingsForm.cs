using System;
using System.Drawing;
using System.Windows.Forms;

namespace OtpTrayApp
{
    public class SettingsForm : Form
    {
        private TabControl tabControl;

        // Killer tab controls
        private NumericUpDown numMaxMemInstance;
        private NumericUpDown numMaxAgeInstance;
        private NumericUpDown numMaxMemZP;
        private NumericUpDown numAutoCheckInterval;
        private CheckBox chkKillOld;
        private CheckBox chkKillHeavy;
        private CheckBox chkKillMain;

        // Monitor tab controls
        private CheckBox chkEnableResourceMonitoring;
        private NumericUpDown numResourceMonitoringInterval;

        // Interface tab controls
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
            this.Text = "Process Manager Settings";
            this.Size = new Size(550, 500);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimumSize = new Size(450, 400);
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;

            int margin = 10;

            // TabControl
            tabControl = new TabControl
            {
                Location = new Point(margin, margin),
                Size = new Size(this.ClientSize.Width - margin * 2, this.ClientSize.Height - 60),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            this.Controls.Add(tabControl);

            // Create tabs
            CreateKillerTab();
            CreateMonitorTab();
            CreateInterfaceTab();

            // Buttons at bottom
            int buttonWidth = 90;
            int buttonSpacing = 10;
            int buttonY = this.ClientSize.Height - 40;

            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(this.ClientSize.Width - buttonWidth - margin, buttonY),
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
                Text = "Save",
                Location = new Point(this.ClientSize.Width - buttonWidth * 2 - buttonSpacing - margin, buttonY),
                Size = new Size(buttonWidth, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnSave.Click += BtnSave_Click;
            this.Controls.Add(btnSave);
        }

        private void CreateKillerTab()
        {
            var tabPage = new TabPage("Killer")
            {
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White
            };
            tabControl.TabPages.Add(tabPage);

            int yPos = 20;
            int labelWidth = 250;
            int controlWidth = 120;
            int padding = 10;
            int margin = 15;
            int groupBoxWidth = this.ClientSize.Width - margin * 4 - 25; // Account for margins and scrollbar

            // Browser processes group
            var grpBrowser = new GroupBox
            {
                Text = "Browser Processes (zbe1)",
                Location = new Point(margin, yPos),
                Size = new Size(groupBoxWidth, 90),
                ForeColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            tabPage.Controls.Add(grpBrowser);

            var lblMaxMem = CreateLabel("Max Memory (MB):", 20, 25, labelWidth);
            numMaxMemInstance = CreateNumeric(20 + labelWidth + padding, 25, controlWidth, 100, 10000, 100);
            grpBrowser.Controls.Add(lblMaxMem);
            grpBrowser.Controls.Add(numMaxMemInstance);

            var lblMaxAge = CreateLabel("Max Age (min):", 20, 55, labelWidth);
            numMaxAgeInstance = CreateNumeric(20 + labelWidth + padding, 55, controlWidth, 5, 1440, 5);
            grpBrowser.Controls.Add(lblMaxAge);
            grpBrowser.Controls.Add(numMaxAgeInstance);

            yPos += 110;

            // Main process group
            var grpMain = new GroupBox
            {
                Text = "Main Process (ZennoPoster)",
                Location = new Point(margin, yPos),
                Size = new Size(groupBoxWidth, 60),
                ForeColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            tabPage.Controls.Add(grpMain);

            var lblMaxMemZP = CreateLabel("Max Memory (MB):", 20, 25, labelWidth);
            numMaxMemZP = CreateNumeric(20 + labelWidth + padding, 25, controlWidth, 1000, 100000, 1000);
            grpMain.Controls.Add(lblMaxMemZP);
            grpMain.Controls.Add(numMaxMemZP);

            yPos += 80;

            // Kill settings group
            var grpKill = new GroupBox
            {
                Text = "Kill Settings",
                Location = new Point(margin, yPos),
                Size = new Size(groupBoxWidth, 110),
                ForeColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            tabPage.Controls.Add(grpKill);

            chkKillOld = CreateCheckBox("Kill old processes", 20, 25, groupBoxWidth - 40);
            grpKill.Controls.Add(chkKillOld);

            chkKillHeavy = CreateCheckBox("Kill heavy processes", 20, 50, groupBoxWidth - 40);
            grpKill.Controls.Add(chkKillHeavy);

            chkKillMain = CreateCheckBox("Kill main process (DANGEROUS!)", 20, 75, groupBoxWidth - 40);
            chkKillMain.ForeColor = Color.FromArgb(255, 100, 100);
            grpKill.Controls.Add(chkKillMain);

            yPos += 130;

            // Auto-check setting
            var lblAutoCheck = CreateLabel("Auto-check interval (min, 0=off):", margin, yPos, labelWidth);
            numAutoCheckInterval = CreateNumeric(margin + labelWidth + padding, yPos, controlWidth, 0, 1440, 1);
            tabPage.Controls.Add(lblAutoCheck);
            tabPage.Controls.Add(numAutoCheckInterval);
        }

        private void CreateMonitorTab()
        {
            var tabPage = new TabPage("Monitor")
            {
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White
            };
            tabControl.TabPages.Add(tabPage);

            int yPos = 20;
            int labelWidth = 250;
            int controlWidth = 120;
            int padding = 10;
            int margin = 15;
            int groupBoxWidth = this.ClientSize.Width - margin * 4 - 25;

            // Resource monitoring group
            var grpMonitor = new GroupBox
            {
                Text = "Resource Monitoring",
                Location = new Point(margin, yPos),
                Size = new Size(groupBoxWidth, 120),
                ForeColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            tabPage.Controls.Add(grpMonitor);

            chkEnableResourceMonitoring = CreateCheckBox("Enable resource monitoring (for debugging memory leaks)", 20, 25, groupBoxWidth - 40);
            grpMonitor.Controls.Add(chkEnableResourceMonitoring);

            var lblInterval = CreateLabel("Monitoring interval (min):", 20, 55, labelWidth);
            numResourceMonitoringInterval = CreateNumeric(20 + labelWidth + padding, 55, controlWidth, 1, 60, 1);
            grpMonitor.Controls.Add(lblInterval);
            grpMonitor.Controls.Add(numResourceMonitoringInterval);

            yPos += 140;

            // Info label
            var lblInfo = new Label
            {
                Text = "Resource monitoring collects memory usage data from ZennoPoster\n" +
                       "and zbe1 processes. Reports are saved in the ./reports/ directory.\n\n" +
                       "Use the 'Report' button in Process Stats to view the latest report.",
                Location = new Point(margin, yPos),
                Size = new Size(groupBoxWidth, 80),
                ForeColor = Color.FromArgb(180, 180, 180),
                AutoSize = false
            };
            tabPage.Controls.Add(lblInfo);
        }

        private void CreateInterfaceTab()
        {
            var tabPage = new TabPage("Interface")
            {
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White
            };
            tabControl.TabPages.Add(tabPage);

            int yPos = 20;
            int margin = 15;
            int groupBoxWidth = this.ClientSize.Width - margin * 4 - 25;

            // UI settings group
            var grpUI = new GroupBox
            {
                Text = "UI Settings",
                Location = new Point(margin, yPos),
                Size = new Size(groupBoxWidth, 100),
                ForeColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            tabPage.Controls.Add(grpUI);

            chkShowLogs = CreateCheckBox("Show logs", 20, 25, groupBoxWidth - 40);
            grpUI.Controls.Add(chkShowLogs);

            chkShowRawCommandLine = CreateCheckBox("Show raw command line", 20, 50, groupBoxWidth - 40);
            grpUI.Controls.Add(chkShowRawCommandLine);
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
                Size = new Size(width, 28),
                Minimum = min,
                Maximum = max,
                Increment = increment,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
        }

        private CheckBox CreateCheckBox(string text, int x, int y, int width)
        {
            return new CheckBox
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 20),
                ForeColor = Color.White,
                AutoSize = false
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
                MessageBox.Show($"Error saving settings: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
