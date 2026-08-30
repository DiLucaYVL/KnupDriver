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
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            } 
            catch (Exception ex) 
            {
                MessageBox.Show(ex.Message, "Erro no Painel de Controle", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

