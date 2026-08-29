using System;
using System.Windows.Forms;
using System.IO;

namespace EmuladorKnup360
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            try 
            {
                bool startMinimized = args != null && Array.Exists(args, a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm(startMinimized));
            } 
            catch (Exception ex) 
            {
                MessageBox.Show(ex.Message, "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
