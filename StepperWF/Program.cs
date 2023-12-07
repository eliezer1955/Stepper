using System;
using System.Windows.Forms;

namespace StepperWF
{
    internal static class Program
    {
        static Form1 myform;
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main( string[] args)
        {

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault( false );
            myform = new Form1(args);
            myform.CmdLineArgs = args;
            Application.Run( myform );
        }
    }
}
