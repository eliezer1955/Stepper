using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StepperWF
{
    public partial class Form1 : Form
    {
        public string CurrentMacro = "stepper.tst.txt";
        public StepperController stepperController;
        public string[] CmdLineArgs;
        public bool stopMonitoring = false;
        public Form1(String[] args)
        {
            //System.Diagnostics.Debugger.Launch();
            CmdLineArgs = args;
            InitializeComponent();
            button2.Text = CurrentMacro;
            stepperController = new StepperController(CurrentMacro, this);
            if(CmdLineArgs.Length>0)
            {
                Thread runner = new Thread(() => stepperController.SocketMode( CmdLineArgs ));
                runner.Start();
            }

        }
        // run macro
        private void button1_Click(object sender, EventArgs e)
        {
            Control[] macro = this.Controls.Find("button2", true);
            string CurrentMacro = macro[0].Text;
            MacroRunner macroRunner = new MacroRunner(stepperController,null, CurrentMacro);
            macroRunner.RunMacro();
        }

        // Select macro
        private void button2_Click(object sender, EventArgs e)
        {
            var picker = new OpenFileDialog();
            picker.FileName = CurrentMacro;
            picker.DefaultExt = "txt";
            picker.InitialDirectory = Environment.CurrentDirectory;
            picker.Filter = "txt files (*.txt)|*.txt";
            if (picker.ShowDialog() == DialogResult.OK)
            {
                CurrentMacro = picker.FileName;
                button2.Text = CurrentMacro;

            }
        }

        public void SetStatus(string s)
        {
            button3.Text = s;
        }

        private void checkBox5_CheckedChanged( object sender, EventArgs e )
        {

        }

        private void checkBox3_CheckedChanged( object sender, EventArgs e )
        {

        }

        private void Form1_Load( object sender, EventArgs e )
        {

        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void label7_Click( object sender, EventArgs e )
        {

        }

        private void forceLeft_Click(object sender, EventArgs e)
        {

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {

        }

        private void button4_Click(object sender, EventArgs e)
        {
            stopMonitoring = true;
        }
    }
}