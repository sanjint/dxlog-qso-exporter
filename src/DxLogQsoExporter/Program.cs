using System;
using System.Threading;
using System.Windows.Forms;

namespace DxLogQsoExporter
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += HandleThreadException;
            AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        private static void HandleThreadException(object sender, ThreadExceptionEventArgs e)
        {
            ShowUnhandledException(e.Exception);
        }

        private static void HandleUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception exception)
            {
                ShowUnhandledException(exception);
            }
        }

        private static void ShowUnhandledException(Exception exception)
        {
            MessageBox.Show(
                "An unexpected error occurred:\r\n\r\n" + exception.Message,
                "DXLog QSO Exporter",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
