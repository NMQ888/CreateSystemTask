using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CreateSystemTask
{
    internal static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            if (arguments.Length == 3 && String.Equals(arguments[1], "--run-task", StringComparison.OrdinalIgnoreCase))
            {
                Environment.ExitCode = TaskExecutor.RunScheduledTask(arguments[2]) ? 0 : 1;
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}
