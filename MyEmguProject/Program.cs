using System;
using System.Windows.Forms;

namespace MyEmguProject
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new BoardWindow());
        }
    }
}