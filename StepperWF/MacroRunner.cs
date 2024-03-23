﻿using CommandMessenger.Transport.Serial;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StepperWF
{
    public class MacroRunner
    {
        public class MyRef<T>
        {
            public T Ref { get; set; }
        }


        public void refreshGUI()
        {
            this.controller.parent.Invalidate();
            this.controller.parent.Update();
            this.controller.parent.Refresh();
            Application.DoEvents();
        }



        public void AddVar( string key, object v ) //storing the ref to a string
        {
            if (null == v)
            {
                v = new MyRef<string> { Ref = " " };
            }
            variables.Add( key, v );
        }



        public void changeVar( string key, object newValue ) //changing any of them
        {
            var ref2 = variables[key] as MyRef<string>;
            if (ref2 == null)
            {
                ref2 = new MyRef<string> { Ref = " " };
            }
            ref2.Ref = newValue.ToString();
        }




        private static readonly log4net.ILog _logger = log4net.LogManager.GetLogger( typeof( MacroRunner ) );
        public string CurrentMacro;
        public SerialTransport serialPort;
        StreamReader fs = null;
        NetworkStream ns = null;
        StepperController controller = null;
        bool socketMode = false;
        public PipeClient pipeClient = null;
        private string[] Macro;
        private int currentline = 0;
        private System.Collections.Generic.Dictionary<string, int> label = new System.Collections.Generic.Dictionary<string, int>();
        private String forceLeftString, forceRightString, opticalString, microSwitchString;
        private String response;
        private Dictionary<string, object> variables = new Dictionary<string, object>();
        private string cannulaLeft, cannulaRight, carriageLeft, carriageRight, doorLeft, doorRight;

        private string ExpandVariables( string instring )
        {
            StringBuilder sb = new StringBuilder();
            int start = 0;
            int i;

            var val = new MyRef<string> { Ref = "" };
            for (i = start ; i < instring.Length ; i++)
                if (instring[i] == '%')
                    for (int j = 1 ; j < instring.Length - i ; j++)
                        if (instring[i + j] == '%')
                        {
                            sb.Append( instring.Substring( start, i - start ) );
                            string key = instring.Substring( i + 1, j - 1 );
                            if (variables.ContainsKey( key ))
                            {
                                val = (MyRef<string>)variables[key];
                                sb.Append( val.Ref );
                                start = i = i + j + 1;
                            }
                            else _logger.Error( "SN " + controller.parent.serialNumber + " " + "Unknown variable:" + val );
                            continue;
                        }
            if ((i - start > 0) && (start < instring.Length))
                sb.Append( instring.Substring( start, i - start ) );
            return sb.ToString();
        }

        private string Evaluate( string instring )
        {
            try
            {
                instring = ExpandVariables( instring );
                DataTable dt = new DataTable();
                var v = dt.Compute( instring, "" );
                return v.ToString();
            }
            catch (Exception ex)
            {
                return instring;
            }
        }

        public MacroRunner( StepperController sc, PipeClient pipeClientin, string filename = null )
        {
            //System.Diagnostics.Debugger.Launch();
            serialPort = sc._serialTransport;
            CurrentMacro = filename;
            pipeClient = pipeClientin;
            controller = sc;
            socketMode = (CurrentMacro == null);
            //GetVersion();
            int currentline = 0;
            if (CurrentMacro != null)
            {
                //Load full macro into memory as array of strings
                Macro = System.IO.File.ReadAllLines( CurrentMacro );
                //Scan macro array for labels, record their line number in Dictionary
                currentline = 0;
                foreach (string line in Macro)
                {
                    string[] line1 = line.Split( '#' ); //Disregard comments
                    if (line1[0].StartsWith( ":" ))
                        label.Add( line1[0].Substring( 1 ).TrimEnd( '\r', '\n', ' ', '\t' ), currentline + 1 );
                    ++currentline;
                }
            }
            AddVar( "response", response );
            AddVar( "forceLeft", forceLeftString );
            AddVar( "forceRight", forceRightString );
            AddVar( "optical", opticalString );
            AddVar( "microSwitch", microSwitchString );
            AddVar( "doorLeft", doorLeft );
            AddVar( "doorRight", doorRight );
            AddVar( "cannulaLeft", cannulaLeft );
            AddVar( "cannulaRight", cannulaRight );
            AddVar( "carriageLeft", carriageLeft );
            AddVar( "carriageRight", carriageRight );


        }

        public async Task<string> readLine()
        {
            //System.Diagnostics.Debugger.Launch();
            string s;
            if (socketMode)
            {
                await pipeClient.receive(); //block until string is received
                s = pipeClient.lastReceive; //retrieve string received
                lock (pipeClient._writerSemaphore)
                {
                    pipeClient.lastReceive = null; //reset lastreceive for next read
                }
            }
            else
            {
                s = currentline >= Macro.Length ? null : Macro[currentline++];
            }
            return s;
        }

        public long MonitorSwitches( long period )
        {
            long startTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            long currentTime = startTime;
            this.controller.SetControlPropertyThreadSafe( controller.parent.button4, "Visible", true );
            refreshGUI();
            while (currentTime - startTime < period)
            {
                readSwitches();
                currentTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                if (this.controller.parent.stopMonitoring)
                {
                    this.controller.parent.stopMonitoring = false;
                    break;
                }
            }
            this.controller.SetControlPropertyThreadSafe( controller.parent.button4, "Visible", false );
            refreshGUI();
            return period;
        }

        public void GetVersion()
        {
            Int32 commandNumber = this.controller.CommandNumber["GetFwVerStr"];
            CommandMessenger.SendCommand cmd = new CommandMessenger.SendCommand( commandNumber );
            cmd.ReqAc = true;
            CommandMessenger.ReceivedCommand responseCmd = this.controller._cmdMessenger.SendCommand( cmd );
            string response = responseCmd.RawString;
            this.controller.SetControlPropertyThreadSafe( controller.parent.label7, "Text", response );
        }

        public float readFlexi( Int16 bank = 1 )
        {
            Int32 commandNumber = controller.CommandNumber["GetFlexiForce"];
            float reslt = 0;
            Int32 response1 = controller.commandStructure[commandNumber].response;
            if (response1 < 0) response1 = commandNumber; //use default response
            Int32 parametersRequired = controller.commandStructure[commandNumber].parameters.Length;
            CommandMessenger.SendCommand cmd = new CommandMessenger.SendCommand( commandNumber, response1,
                                                                                 controller.commandStructure[commandNumber].timeout );
            cmd.AddArgument( bank );
            cmd.ReqAc = true;
            if (cmd.Ok)
            {
                CommandMessenger.ReceivedCommand responseCmd = controller._cmdMessenger.SendCommand( cmd );
                if (responseCmd.RawString != null)
                {
                    string response = responseCmd.RawString;
                    string[] line1 = response.Split( ',' );
                    line1 = line1[2].Split( ';' );
                    response = line1[0];
                    reslt = float.Parse( response );
                }
            }

            return reslt;
        }

        public Int16 readSwitch( Int16 bank = 1 )
        {
            Int32 commandNumber = controller.CommandNumber["GetSwitchSet"];
            Int16 reslt = 0;
            Int32 response1 = controller.commandStructure[commandNumber].response;
            if (response1 < 0) response1 = commandNumber; //use default response
            Int32 parametersRequired = controller.commandStructure[commandNumber].parameters.Length;
            CommandMessenger.SendCommand cmd = new CommandMessenger.SendCommand( commandNumber, response1,
                                                                                 controller.commandStructure[commandNumber].timeout );
            cmd.AddArgument( bank );
            cmd.ReqAc = true;
            if (cmd.Ok)
            {
                CommandMessenger.ReceivedCommand responseCmd = controller._cmdMessenger.SendCommand( cmd );
                if (responseCmd.RawString != null)
                {
                    string response = responseCmd.RawString;
                    string[] line1 = response.Split( ',' );
                    line1 = line1[2].Split( ';' );
                    response = line1[0];
                    reslt = Int16.Parse( response );
                }
            }

            return reslt;
        }
        public void selectAxis( Int32 axis )
        {
            Int32 commandNumber = 17; //set current axis
            Int32 response1 = controller.commandStructure[commandNumber].response;
            if (response1 < 0) response1 = commandNumber; //use default response
            Int32 parametersRequired = controller.commandStructure[commandNumber].parameters.Length;
            CommandMessenger.SendCommand cmd = new CommandMessenger.SendCommand( commandNumber, response1,
                                                                                 controller.commandStructure[commandNumber].timeout );
            cmd.ReqAc = true;
            if (cmd.Ok)
            {
                CommandMessenger.ReceivedCommand responseCmd = controller._cmdMessenger.SendCommand( cmd );
            }
        }

        void changeBox( CheckBox c, int val )
        {
            bool prev = c.Checked;
            if (prev != (val != 0))
                controller.SetControlPropertyThreadSafe( c, "BackColor", Color.FromArgb( 0, 232, 0 ) );
            controller.SetControlPropertyThreadSafe( c, "Checked", val != 0 );
        }


        public void readSwitches()
        {
            //select switch set 1
            Int16 microSwitches = readSwitch( 1 );
            //select switch set 2
            Int16 optical = readSwitch( 2 );
            float forceLeft = readFlexi( 1 );
            float forceRight = readFlexi( 2 );
            forceLeftString = forceLeft.ToString();
            forceRightString = forceRight.ToString();
            opticalString = optical.ToString();
            microSwitchString = microSwitches.ToString();
            controller.parent.textBox1.Text = forceLeft.ToString();
            controller.parent.textBox2.Text = forceRight.ToString();
            cannulaLeft = ((microSwitches & (short)0x01) != 0).ToString();
            cannulaRight = ((microSwitches & (short)0x02) != 0).ToString();
            doorLeft = ((optical & (short)0x01) != 0).ToString();
            doorRight = ((optical & (short)0x02) != 0).ToString();
            carriageLeft = ((optical & (short)0x04) != 0).ToString();
            carriageRight = ((optical & (short)0x08) != 0).ToString();

            //update variables dictionary
            changeVar( "forceLeft", forceLeftString );
            changeVar( "forceRight", forceRightString );
            changeVar( "forceLeft", forceLeft );
            changeVar( "microSwitch", microSwitchString );
            changeVar( "optical", opticalString );
            changeVar( "cannulaLeft", cannulaLeft );
            changeVar( "cannulaRight", cannulaRight );
            changeVar( "doorLeft", doorLeft );
            changeVar( "doorRight", doorRight );
            changeVar( "carriageLeft", carriageLeft );
            changeVar( "carriageRight", carriageRight );

            //update GUI
            changeBox( controller.parent.checkBox3, (microSwitches & (short)0x01) );
            changeBox( controller.parent.checkBox6, (microSwitches & (short)0x02) );
            changeBox( controller.parent.checkBox1, (optical & (short)0x01) );
            changeBox( controller.parent.checkBox2, (optical & (short)0x04) );
            changeBox( controller.parent.checkBox5, (optical & (short)0x08) );
            changeBox( controller.parent.checkBox4, (optical & (short)0x02) );
            controller.SetControlPropertyThreadSafe( controller.parent.textBox1, "BackColor", (forceLeft < -3 || forceLeft > 6) ? System.Drawing.Color.Crimson : System.Drawing.Color.LimeGreen );
            controller.SetControlPropertyThreadSafe( controller.parent.textBox2, "BackColor", (forceLeft < -3 || forceRight > 6) ? System.Drawing.Color.Crimson : System.Drawing.Color.LimeGreen );
            forceLeft = 50 + forceLeft / 2; //empirical scaling
            forceRight = 50 + forceRight / 2;
            forceLeft = Math.Min( Math.Max( forceLeft, 0 ), 100 );
            forceRight = Math.Min( Math.Max( forceRight, 0 ), 100 );
            controller.parent.forceLeft.Value = (int)Math.Round( forceLeft, 0 );
            controller.parent.forceRight.Value = (int)Math.Round( forceRight, 0 );
            refreshGUI();

        }
        public async void RunMacro()
        {
            //  Read in macro stream
            string[] lastCommand;
            string response = "";
            string lastCommandReturnTypes;
            byte[] b = new byte[1024];
            string line;
            while (true)
            {
                line = await readLine();

                if (line == null) break;
                if (line.StartsWith( "\0" )) continue;
                if (line.StartsWith( "#" )) continue;
                if (line.StartsWith( ":" )) continue;
                if (string.IsNullOrEmpty( line )) continue;
                if (string.IsNullOrWhiteSpace( line )) continue;

                Console.WriteLine( "Read line:{0}", line );
                if (line.StartsWith( "END" )) //Terminate program
                {
                    string expr = "";
                    string[] line1 = line.Split( '#' ); //Disregard comments
                    string[] parsedLine = line1[0].Split( ',' );
                    if (string.IsNullOrWhiteSpace( parsedLine[0] )) //Disregard blanks lines
                        continue;
                    if (parsedLine[1] != null)
                        expr = parsedLine[1]; //isolate expression
                    Environment.Exit( Int32.Parse( Evaluate( expr ) ) );
                }
                if (line.StartsWith( "IFRETURNISNOT" )) //conditional execution based on last return
                {
                    string value = "";
                    string expr = "";
                    string[] line1 = line.Split( '#' ); //Disregard comments
                    string[] parsedLine = line1[0].Split( ',' );
                    if (string.IsNullOrWhiteSpace( parsedLine[0] )) //Disregard blanks lines
                        continue;
                    if (parsedLine[1] != null)
                        expr = parsedLine[1]; //isolate expression
                    if (parsedLine[2] != null)
                        value = parsedLine[2]; //isolate target value
                    value = Evaluate( value );
                    expr = Evaluate( expr );

                    if (value != expr) //last return matches value
                        continue; //do nothing, go to read next command
                                  //value is not equal to last response, execute conditional command
                    line = ""; //reassemble rest of conditional command
                    for (int i = 3 ; i < parsedLine.Length ; i++)
                    {
                        line += parsedLine[i];
                        if (i < parsedLine.Length - 1) line += ",";
                    }
                    //continue execution as if it was non-conditional
                }
                if (line.StartsWith( "IFRETURNIS" )) //conditional execution based on last return
                {
                    string value = "";
                    string expr = "";
                    string[] line1 = line.Split( '#' ); //Disregard comments
                    string[] parsedLine = line1[0].Split( ',' );
                    if (string.IsNullOrWhiteSpace( parsedLine[0] )) //Disregard blanks lines
                        continue;
                    if (parsedLine[1] != null)
                        expr = parsedLine[1]; //isolate expression
                    if (parsedLine[2] != null)
                        value = parsedLine[2]; //isolate target value
                    value = Evaluate( value );
                    expr = Evaluate( expr );
                    if (value == Evaluate( expr )) //last return does not match value
                        continue; //do nothing, go to read next command
                                  //value is equal to last response
                    line = ""; //reassemble rest of command
                    for (int i = 3 ; i < parsedLine.Length ; i++)
                    {
                        line += parsedLine[i];
                        if (i < parsedLine.Length - 1) line += ",";
                    }
                    //continue execution as if it was non-conditional
                }
                if (line.StartsWith( "EVALUATE" )) //Set response to evaluation of expression
                {
                    string value = "";
                    string[] line1 = line.Split( '#' ); //Disregard comments
                    string[] parsedLine = line1[0].Split( ',' );
                    if (string.IsNullOrWhiteSpace( parsedLine[0] )) //Disregard blanks lines
                        continue;
                    if (parsedLine[1] != null)
                        value = parsedLine[1]; //isolate target value

                    response = Evaluate( parsedLine[1] );
                    changeVar( "response", response );
                    continue;

                }
                if (line.StartsWith( "SET" )) //set value of global var; create it if needed
                {
                    string variable = "";
                    string value = "";
                    string[] line1 = line.Split( '#' ); //Disregard comments
                    string[] parsedLine = line1[0].Split( ',' );
                    if (string.IsNullOrWhiteSpace( parsedLine[0] )) //Disregard blanks lines
                        continue;
                    if (parsedLine[1] != null)
                        variable = parsedLine[1];
                    if (parsedLine[2] != null)
                        value = Evaluate( parsedLine[2] );
                    if (!variables.ContainsKey( variable ))
                        AddVar( variable, null );
                    changeVar( variable, value );
                    continue;
                }
                if (line.StartsWith( "EXIT" )) //stop macro
                {
                    break;
                }
                if (line.StartsWith( "EXECUTE" )) //stop macro
                {
                    string value = "";

                    string[] line1 = line.Split( '#' ); //Disregard comments
                    string[] parsedLine = line1[0].Split( ',' );
                    if (string.IsNullOrWhiteSpace( parsedLine[0] )) //Disregard blanks lines
                        continue;
                    if (parsedLine[1] != null)
                        value = parsedLine[1];
                    System.Diagnostics.Process.Start( "CMD.exe", "/C " + value );
                    continue;
                }

                if (line.StartsWith( "LOGERROR" )) //write log entry
                {
                    string value = "";
                    string[] line1 = line.Split( '#' ); //Disregard comments
                    string[] parsedLine = line1[0].Split( ',' );
                    if (string.IsNullOrWhiteSpace( parsedLine[0] )) //Disregard blanks lines
                        continue;
                    if (parsedLine[1] != null)
                        value = ExpandVariables( parsedLine[1] );

                    _logger.Error( "SN " + controller.parent.serialNumber + " " + value );
                    continue;
                }
                if (line.StartsWith( "GOTO" ))
                {
                    string value = "";
                    string[] line1 = line.Split( '#' ); //Disregard comments
                    string[] parsedLine = line1[0].Split( ',' );
                    if (string.IsNullOrWhiteSpace( parsedLine[0] )) //Disregard blanks lines
                        continue;
                    if (parsedLine[1] != null)
                        value = parsedLine[1].TrimEnd( '\r', '\n', ' ', '\t' );
                    if (!label.ContainsKey( value ))
                        _logger.Error( "SN " + controller.parent.serialNumber + " " + "Unknown label " + value );
                    else
                    {

                        currentline = label[value];
                        continue;
                    }

                }
                // "Nested" macro calling
                if (line.StartsWith( "@" ))
                {
                    MacroRunner macroRunner = new MacroRunner( controller, pipeClient, line.Substring( 1 ) );
                    macroRunner.RunMacro();
                    continue;
                }
                // Wait for fixed time
                if (line.StartsWith( "SLEEP" ))
                {
                    int delay = 0;
                    string[] line1 = line.Split( '#' ); //Disregard comments
                    string[] parsedLine = line1[0].Split( ',' );
                    if (string.IsNullOrWhiteSpace( parsedLine[0] )) //Disregard blanks lines
                        continue;
                    if (parsedLine[1] != null)
                        delay = Int32.Parse( parsedLine[1] );
                    Thread.Sleep( delay );
                    continue;
                }
                // Wait until status is idle
                if (line.StartsWith( "WAIT" ))
                {
                    string[] line1 = line.Split( '#' ); //Disregard comments
                    string[] parsedLine = line1[0].Split( ',' );
                    if (string.IsNullOrWhiteSpace( parsedLine[0] )) //Disregard blanks lines
                        continue;
                    if (parsedLine[1] != null)
                    {
                        bool motionDone = false;
                        do
                        {
                            Int32.Parse( parsedLine[1] );
                            //serialPort.WriteLine( "/Q" + parsedLine[1] + "R" );
                            CommandMessenger.ReceivedCommand responseCmd;

                            CommandMessenger.SendCommand cmd = new CommandMessenger.SendCommand( 7, 7,
                                                     controller.commandStructure[7].timeout );
                            responseCmd = controller._cmdMessenger.SendCommand( cmd );
                            string[] line2 = responseCmd.RawString.TrimEnd( '\r', '\n' ).Split( ',' );
                            if (line2.Length < 3 || line2[2][0] == '1') continue; //isolate status
                            motionDone = true;
                        } while (!motionDone);

                    }
                    continue;
                }
                // Read switches, update GUI display
                if (line.StartsWith( "READSWITCHES" ))
                {
                    string[] line1 = line.Split( '#' ); //Disregard comments
                    string[] parsedLine = line1[0].Split( ',' );
                    readSwitches();
                    continue;
                }

                // Read switches continuously for period of time, update GUI display
                if (line.StartsWith( "MONITORSWITCHES" ))
                {
                    string[] line1 = line.Split( '#' ); //Disregard comments
                    string[] parsedLine = line1[0].Split( ',' );
                    MonitorSwitches( long.Parse( parsedLine[1] ) );
                    continue;
                }
                // PRINT STATUS ON gui
                if (line.StartsWith( "STATUS" ))
                {
                    string status = "";
                    string[] line1 = line.Split( '#' ); //Disregard comments
                    string[] parsedLine = line1[0].Split( ',' );
                    if (string.IsNullOrWhiteSpace( parsedLine[0] )) //Disregard blanks lines
                        continue;
                    if (parsedLine[1] != null)
                        status = parsedLine[1];
                    controller.SetControlPropertyThreadSafe( controller.parent.textBox3, "Text", status );
                    refreshGUI();
                    continue;
                }
                // Pop up MessageBox
                if (line.StartsWith( "ALERT" ))
                {
                    string[] line1 = line.Split( '#' ); //Disregard comments
                    string[] parsedLine = line1[0].Split( ',' );
                    if (string.IsNullOrWhiteSpace( parsedLine[0] )) //Disregard blanks lines
                        continue;

                    if (parsedLine[1] != null)
                    {
                        MessageBoxButtons buttons = MessageBoxButtons.YesNo;
                        DialogResult result;
                        result = MessageBox.Show( parsedLine[1], "Stepper Alert!", buttons );
                        response = result.ToString();
                        string[] resp1 = response.Split( ',' );
                        changeVar( "response", resp1[0] );
                        continue;
                    }
                }

                if (line.StartsWith( "REPORT" ))
                {

                    string[] line1 = line.Split( '#' ); //Disregard comments
                    string[] parsedLine = line1[0].Split( ',' );
                    if (string.IsNullOrWhiteSpace( parsedLine[0] )) //Disregard blanks lines
                        continue;

                    if (parsedLine[1] != null)
                    {
                        var i = line.IndexOf( ',' );
                        if (i > -1)
                        {
                            pipeClient.client.Send( "Stepper:" + line.Substring( i + 1 ) );
                            continue;
                        }
                    }
                }

                //Actual command
                string[] lin2 = line.Split( '#' ); //kill comments
                if (!(string.IsNullOrWhiteSpace( lin2[0] ) && lin2[0].StartsWith( "\0" )))
                {
                    string[] lin1 = lin2[0].Split( ',' ); //split parameters
                    Int32 commandNumber = -1;
                    try
                    {
                        commandNumber = controller.CommandNumber[lin1[0]];
                    }
                    catch (Exception e)
                    {
                        // invalid command (not in dictionary)
                        Console.WriteLine( e.Message );
                    }

                    Int32 response1 = controller.commandStructure[commandNumber].response;
                    if (response1 < 0) response1 = commandNumber; //use default response
                    Int32 parametersRequired = controller.commandStructure[commandNumber].parameters.Length;
                    CommandMessenger.SendCommand cmd = new CommandMessenger.SendCommand( commandNumber, response1,
                                                         controller.commandStructure[commandNumber].timeout );
                    //remember what this command is and what return types it has
                    lastCommand = lin1;
                    lastCommandReturnTypes = controller.commandStructure[commandNumber].returns;
                    for (Int32 pn = 0 ; pn < parametersRequired ; pn++)
                    {
                        switch (controller.commandStructure[commandNumber].parameters[pn])
                        {
                            case 'i':
                                Int16 pi = Int16.Parse( lin1[pn + 1] );
                                cmd.AddArgument( pi );
                                break;
                            case 'l':
                                Int32 pl = Int32.Parse( lin1[pn + 1] );
                                cmd.AddArgument( pl );
                                break;
                            case 'b':
                                bool pb = bool.Parse( lin1[pn + 1] );
                                break;
                            case 's':
                                cmd.AddArgument( lin1[pn + 1] );
                                break;
                            default:
                                break;

                        }

                    }
                    cmd.ReqAc = true;
                    if (cmd.Ok)
                    {
                        CommandMessenger.ReceivedCommand responseCmd = controller._cmdMessenger.SendCommand( cmd );
                        if (responseCmd.RawString != null)
                        {
                            Console.WriteLine( String.Format( ">>>>{0}<<<{1}", cmd.CmdId, responseCmd.RawString.Trim() ) );
                            string temp = responseCmd.RawString.Trim();
                            string[] temp1 = temp.Split( ',' );
                            if (temp1.Length > 1)
                            {
                                response = temp1[1].TrimEnd( ';', '\r', '\n' );
                                changeVar( "response", response );
                            }

                            if (commandNumber == 5)   //GetFwVersionStr
                            {
                                this.controller.SetControlPropertyThreadSafe( controller.parent.label7, "Text", responseCmd.RawString.Trim() );
                            }
                        }
                        else
                        { //received negative response Id... probably timeout
                            MessageBoxButtons buttons = MessageBoxButtons.YesNo;
                            DialogResult result;
                            result = MessageBox.Show( String.Format( "Command {0} Timeout??", cmd.CmdId ), "Timeout!", buttons );
                            continue;
                        }


                        controller._cmdMessenger.ClearReceiveQueue();
                        controller._cmdMessenger.ClearSendQueue();
                    }
                    else
                        Console.WriteLine( string.Format( "Unknown command {n} issued\n", cmd.CmdId ) );
                }
            }


        }

    }
}