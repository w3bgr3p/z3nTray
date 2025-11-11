using System;
using System.Drawing;
using System.Windows.Forms;

namespace OtpTrayApp
{
    public class OtpInputForm : Form
    {
        private TextBox txtOtpKey;
        private Button btnGenerate;
        private Button btnCancel;
        private Label lblInstruction;

        public string OtpKey => txtOtpKey.Text;

        public OtpInputForm()
        {
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            // Настройка формы
            this.Text = "OTP tool";
            this.Size = new Size(400, 180);
            this.StartPosition = FormStartPosition.Manual; // Изменено с CenterScreen
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.TopMost = true; // Форма поверх других окон

            // Метка с инструкцией
            lblInstruction = new Label
            {
                Text = "input SECRET:",
                Location = new Point(20, 20),
                Size = new Size(350, 20),
                Font = new Font("Iosevka", 10)
            };

            // Поле ввода
            txtOtpKey = new TextBox
            {
                Location = new Point(20, 50),
                Size = new Size(340, 25),
                Font = new Font("Iosevka", 10)
            };

            // Кнопка генерации
            btnGenerate = new Button
            {
                Text = "Generate",
                Location = new Point(180, 95),
                Size = new Size(120, 30),
                DialogResult = DialogResult.OK,
                Font = new Font("Iosevka", 10)
            };
            btnGenerate.Click += BtnGenerate_Click;

            // Кнопка отмены
            btnCancel = new Button
            {
                Text = "X",
                Location = new Point(310, 95),
                Size = new Size(30, 30),
                DialogResult = DialogResult.Cancel,
                Font = new Font("Iosevka", 10)
            };

            // Добавляем контролы на форму
            this.Controls.Add(lblInstruction);
            this.Controls.Add(txtOtpKey);
            this.Controls.Add(btnGenerate);
            this.Controls.Add(btnCancel);

            // Устанавливаем кнопки по умолчанию
            this.AcceptButton = btnGenerate;
            this.CancelButton = btnCancel;

            // Фокус на поле ввода
            this.Shown += (s, e) => txtOtpKey.Focus();
        }

        private void BtnGenerate_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtOtpKey.Text))
            {
                MessageBox.Show("input SECRET", 
                    "Warn", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
                txtOtpKey.Focus();
            }
        }
    }
}
