using System;
using System.Windows.Forms;

namespace MyEmguProject
{
    public partial class StartWindow : Form
    {
        public StartWindow()
        {
            this.Text = "Главное меню";
            this.Size = new Size(400, 300);
            this.StartPosition = FormStartPosition.CenterScreen;

            var label = new Label
            {
                Text = "Добро пожаловать!",
                Location = new Point(100, 100),
                Size = new Size(200, 50),
                Font = new Font("Segoe UI", 16)
            };
            this.Controls.Add(label);

            // Можно добавить кнопки для запуска BoardWindow обратно
            var btnLaunch = new Button
            {
                Text = "Запустить управление LED",
                Size = new Size(250, 60),
                Location = new Point(75, 180),
                BackColor = Color.Green,
                ForeColor = Color.White
            };
            btnLaunch.Click += (s, e) =>
            {
                var boardWin = new BoardWindow();
                boardWin.Show();
                this.Close();
            };
            this.Controls.Add(btnLaunch);
        }
    }
}