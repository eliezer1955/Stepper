﻿using System;
using System.IO;
using System.Windows.Forms;

namespace StepperWF
{
    internal static class Program
    {

        private static readonly log4net.ILog _logger = log4net.LogManager.GetLogger(typeof(Program));

        static Form1 myform;
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main( string[] args)
        {
            var configFile = new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log4net.config"));
            log4net.Config.XmlConfigurator.Configure(configFile);
            _logger.Info("StepperDiag  is starting...");

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault( false );
            myform = new Form1(args);
            myform.CmdLineArgs = args;
            Application.Run( myform );
        }
    }
}
