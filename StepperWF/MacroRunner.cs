using CommandMessenger.Transport.Serial;
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StepperWF
{
    public class MacroRunner
    {
        private static readonly log4net.ILog _logger = log4net.LogManager.GetLogger(typeof(MacroRunner));
        public string CurrentMacro;
        public SerialTransport serialPort;
        StreamReader fs = null;
        NetworkStream ns = null;
        StepperController controller = null;
        bool socketMode = false;
        public PipeClient pipeClient = null;

        public MacroRunner(StepperController sc, PipeClient pipeClientin, string filename = null)
        {
            //System.Diagnostics.Debugger.Launch();
            serialPort = sc._serialTransport;
            CurrentMacro = filename;
            pipeClient = pipeClientin;
            controller = sc;
            socketMode = (CurrentMacro == null);
            //GetVersion();
            if (CurrentMacro != null)
                fs = new StreamReader(CurrentMacro);
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
                s = fs.ReadLine();
            }
            return s;
        }

        public long MonitorSwitches(long period)
        {
            long startTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            long currentTime = startTime;
            while (currentTime - startTime < period)
            {
                readSwitches();
            }
            return period;
        }

        public void GetVersion()
        {
            Int32 commandNumber = this.controller.CommandNumber["GetFwVerStr"];
            CommandMessenger.SendCommand cmd = new CommandMessenger.SendCommand(commandNumber);
            cmd.ReqAc = true;
            CommandMessenger.ReceivedCommand responseCmd = this.controller._cmdMessenger.SendCommand(cmd);
            string response = responseCmd.RawString;
            this.controller.SetControlPropertyThreadSafe(controller.parent.label7, "Text", response);
        }

        public float readFlexi(Int16 bank = 1)
        {
            Int32 commandNumber = controller.CommandNumber["GetFlexiForce"];
            float reslt = 0;
            Int32 response1 = controller.commandStructure[commandNumber].response;
            if (response1 < 0) response1 = commandNumber; //use default response
            Int32 parametersRequired = controller.commandStructure[commandNumber].parameters.Length;
            CommandMessenger.SendCommand cmd = new CommandMessenger.SendCommand(commandNumber, response1,
                                                                                 controller.commandStructure[commandNumber].timeout);
            cmd.AddArgument(bank);
            cmd.ReqAc = true;
            if (cmd.Ok)
            {
                CommandMessenger.ReceivedCommand responseCmd = controller._cmdMessenger.SendCommand(cmd);
                string response = responseCmd.RawString;
                string[] line1 = response.Split(',');
                line1 = line1[2].Split(';');
                response = line1[0];
                reslt = float.Parse(response);
            }

            return reslt;
        }

        public Int16 readSwitch(Int16 bank = 1)
        {
            Int32 commandNumber = controller.CommandNumber["GetSwitchSet"];
            Int16 reslt = 0;
            Int32 response1 = controller.commandStructure[commandNumber].response;
            if (response1 < 0) response1 = commandNumber; //use default response
            Int32 parametersRequired = controller.commandStructure[commandNumber].parameters.Length;
            CommandMessenger.SendCommand cmd = new CommandMessenger.SendCommand(commandNumber, response1,
                                                                                 controller.commandStructure[commandNumber].timeout);
            cmd.AddArgument(bank);
            cmd.ReqAc = true;
            if (cmd.Ok)
            {
                CommandMessenger.ReceivedCommand responseCmd = controller._cmdMessenger.SendCommand(cmd);
                string response = responseCmd.RawString;
                string[] line1 = response.Split(',');
                line1 = line1[2].Split(';');
                response = line1[0];
                reslt = Int16.Parse(response);
            }

            return reslt;
        }
        public void selectAxis(Int32 axis)
        {
            Int32 commandNumber = 17; //set current axis
            Int32 response1 = controller.commandStructure[commandNumber].response;
            if (response1 < 0) response1 = commandNumber; //use default response
            Int32 parametersRequired = controller.commandStructure[commandNumber].parameters.Length;
            CommandMessenger.SendCommand cmd = new CommandMessenger.SendCommand(commandNumber, response1,
                                                                                 controller.commandStructure[commandNumber].timeout);
            cmd.ReqAc = true;
            if (cmd.Ok)
            {
                CommandMessenger.ReceivedCommand responseCmd = controller._cmdMessenger.SendCommand(cmd);
            }
        }

        public void readSwitches()
        {
            //selectAxis( 1 );
            Int16 microSwitches = readSwitch(1);
            //selectAxis( 2 );
            Int16 optical = readSwitch(2);
            float forceLeft = readFlexi(1);
            float forceRight = readFlexi(2);
            controller.parent.textBox1.Text = forceLeft.ToString();
            controller.parent.textBox2.Text = forceRight.ToString();
            controller.SetControlPropertyThreadSafe(controller.parent.checkBox3, "Checked", (microSwitches & (short)0x01) != 0);
            controller.SetControlPropertyThreadSafe(controller.parent.checkBox6, "Checked", (microSwitches & (short)0x02) != 0);
            controller.SetControlPropertyThreadSafe(controller.parent.checkBox1, "Checked", (optical & (short)0x01) != 0);
            controller.SetControlPropertyThreadSafe(controller.parent.checkBox2, "Checked", (optical & (short)0x04) != 0);
            controller.SetControlPropertyThreadSafe(controller.parent.checkBox5, "Checked", (optical & (short)0x08) != 0);
            controller.SetControlPropertyThreadSafe(controller.parent.checkBox4, "Checked", (optical & (short)0x02) != 0);
            forceLeft = 50 + forceLeft / 2; //empirical scaling
            forceRight = 50 + forceRight / 2;
            forceLeft = Math.Min(Math.Max(forceLeft, 0), 100);
            forceRight = Math.Min(Math.Max(forceRight, 0), 100);
            //controller.SetControlPropertyThreadSafe( controller.parent.forceLeft, "Value", Math.Round(forceLeft,0) );
            //controller.SetControlPropertyThreadSafe( controller.parent.forceRight, "Value", Math.Round(forceRight,0) );
            controller.parent.forceLeft.Value = (int)Math.Round(forceLeft, 0);
            controller.parent.forceRight.Value = (int)Math.Round(forceRight, 0);

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
                if (line.StartsWith("\0")) continue;
                if (line.StartsWith("#")) continue;
                if (string.IsNullOrEmpty(line)) continue;
                if (string.IsNullOrWhiteSpace(line)) continue;

                Console.WriteLine("Read line:{0}", line);
                if (line.StartsWith("IFRETURNISNOT")) //conditional execution based on last return
                {
                    string value = "";
                    string[] line1 = line.Split('#'); //Disregard comments
                    string[] parsedLine = line1[0].Split(',');
                    if (string.IsNullOrWhiteSpace(parsedLine[0])) //Disregard blanks lines
                        continue;
                    if (parsedLine[1] != null)
                        value = parsedLine[1]; //isolate target value

                    if (value == response) //last return matches value
                        continue; //do nothing, go to read next command
                    //value is not equal to last response, execute conditional command
                    line = ""; //reassemble rest of conditional command
                    for (int i = 2; i < parsedLine.Length; i++)
                    {
                        line += parsedLine[i];
                        if (i < parsedLine.Length - 1) line += ",";
                    }
                    //coninue execution as if it was non-conditional
                }
                if (line.StartsWith("IFRETURNIS")) //conditional execution based on last return
                {
                    string value = "";
                    string[] line1 = line.Split('#'); //Disregard comments
                    string[] parsedLine = line1[0].Split(',');
                    if (string.IsNullOrWhiteSpace(parsedLine[0])) //Disregard blanks lines
                        continue;
                    if (parsedLine[1] != null)
                        value = parsedLine[1]; //isolate target value

                    if (value != response) //last return does not match value
                        continue; //do nothing, go to read next command
                    //value is equal to last response
                    line = ""; //reassemble rest of command
                    for (int i = 2; i < parsedLine.Length; i++)
                    {
                        line += parsedLine[i];
                        if (i < parsedLine.Length - 1) line += ",";
                    }
                    //coninue execution as if it was non-conditional
                }
                if (line.StartsWith("LOGERROR")) //write log entry
                {
                    string value = "";
                    string[] line1 = line.Split('#'); //Disregard comments
                    string[] parsedLine = line1[0].Split(',');
                    if (string.IsNullOrWhiteSpace(parsedLine[0])) //Disregard blanks lines
                        continue;
                    if (parsedLine[1] != null)
                        value = parsedLine[1];

                    _logger.Error(value);
                    continue;
                }

                // "Nested" macro calling
                if (line.StartsWith("@"))
                {
                    MacroRunner macroRunner = new MacroRunner(controller, pipeClient, line.Substring(1));
                    macroRunner.RunMacro();
                    continue;
                }
                // Wait for fixed time
                if (line.StartsWith("SLEEP"))
                {
                    int delay = 0;
                    string[] line1 = line.Split('#'); //Disregard comments
                    string[] parsedLine = line1[0].Split(',');
                    if (string.IsNullOrWhiteSpace(parsedLine[0])) //Disregard blanks lines
                        continue;
                    if (parsedLine[1] != null)
                        delay = Int32.Parse(parsedLine[1]);
                    Thread.Sleep(delay);
                    continue;
                }
                // Wait until status is idle
                if (line.StartsWith("WAIT"))
                {
                    string[] line1 = line.Split('#'); //Disregard comments
                    string[] parsedLine = line1[0].Split(',');
                    if (string.IsNullOrWhiteSpace(parsedLine[0])) //Disregard blanks lines
                        continue;
                    if (parsedLine[1] != null)
                    {
                        bool motionDone = false;
                        do
                        {
                            Int32.Parse(parsedLine[1]);
                            //serialPort.WriteLine( "/Q" + parsedLine[1] + "R" );
                            CommandMessenger.ReceivedCommand responseCmd;

                            CommandMessenger.SendCommand cmd = new CommandMessenger.SendCommand(7, 7,
                                                     controller.commandStructure[7].timeout);
                            responseCmd = controller._cmdMessenger.SendCommand(cmd);
                            string[] line2 = responseCmd.RawString.TrimEnd('\r', '\n').Split(',');
                            if (line2.Length < 3 || line2[2][0] == '1') continue; //isolate status
                            motionDone = true;
                        } while (!motionDone);

                    }
                    continue;
                }
                // Read switches, update GUI display
                if (line.StartsWith("READSWITCHES"))
                {
                    string[] line1 = line.Split('#'); //Disregard comments
                    string[] parsedLine = line1[0].Split(',');
                    readSwitches();
                    continue;
                }

                // Read switches continuously for period of time, update GUI display
                if (line.StartsWith("MONITORSWITCHES"))
                {
                    string[] line1 = line.Split('#'); //Disregard comments
                    string[] parsedLine = line1[0].Split(',');
                    MonitorSwitches(long.Parse(parsedLine[1]));
                    continue;
                }
                // Pop up MessageBox
                if (line.StartsWith("ALERT"))
                {
                    string[] line1 = line.Split('#'); //Disregard comments
                    string[] parsedLine = line1[0].Split(',');
                    if (string.IsNullOrWhiteSpace(parsedLine[0])) //Disregard blanks lines
                        continue;

                    if (parsedLine[1] != null)
                    {
                        MessageBoxButtons buttons = MessageBoxButtons.YesNo;
                        DialogResult result;
                        result = MessageBox.Show(parsedLine[1], "Stepper Alert!", buttons);
                        response = result.ToString();
                        continue;
                    }
                }

                if (line.StartsWith("REPORT"))
                {

                    string[] line1 = line.Split('#'); //Disregard comments
                    string[] parsedLine = line1[0].Split(',');
                    if (string.IsNullOrWhiteSpace(parsedLine[0])) //Disregard blanks lines
                        continue;

                    if (parsedLine[1] != null)
                    {
                        var i = line.IndexOf(',');
                        if (i > -1)
                        {
                            pipeClient.client.Send("Stepper:" + line.Substring(i + 1));
                            continue;
                        }
                    }
                }

                //Actual command
                string[] lin2 = line.Split('#'); //kill comments
                if (!(string.IsNullOrWhiteSpace(lin2[0]) && lin2[0].StartsWith("\0")))
                {
                    string[] lin1 = lin2[0].Split(','); //split parameters
                    Int32 commandNumber = -1;
                    try
                    {
                        commandNumber = controller.CommandNumber[lin1[0]];
                    }
                    catch (Exception e)
                    {
                        // invalid command (not in dictionary)
                        Console.WriteLine(e.Message);
                    }

                    Int32 response1 = controller.commandStructure[commandNumber].response;
                    if (response1 < 0) response1 = commandNumber; //use default response
                    Int32 parametersRequired = controller.commandStructure[commandNumber].parameters.Length;
                    CommandMessenger.SendCommand cmd = new CommandMessenger.SendCommand(commandNumber, response1,
                                                         controller.commandStructure[commandNumber].timeout);
                    //remember what this command is and what return types it has
                    lastCommand = lin1;
                    lastCommandReturnTypes = controller.commandStructure[commandNumber].returns;
                    for (Int32 pn = 0; pn < parametersRequired; pn++)
                    {
                        switch (controller.commandStructure[commandNumber].parameters[pn])
                        {
                            case 'i':
                                Int16 pi = Int16.Parse(lin1[pn + 1]);
                                cmd.AddArgument(pi);
                                break;
                            case 'l':
                                Int32 pl = Int32.Parse(lin1[pn + 1]);
                                cmd.AddArgument(pl);
                                break;
                            case 'b':
                                bool pb = bool.Parse(lin1[pn + 1]);
                                break;
                            case 's':
                                cmd.AddArgument(lin1[pn + 1]);
                                break;
                            default:
                                break;

                        }

                    }
                    cmd.ReqAc = true;
                    if (cmd.Ok)
                    {
                        CommandMessenger.ReceivedCommand responseCmd = controller._cmdMessenger.SendCommand(cmd);
                        if (responseCmd.RawString != null)
                        {
                            Console.WriteLine(String.Format(">>>>{0}<<<{1}", cmd.CmdId, responseCmd.RawString.Trim()));
                            if (commandNumber == 5)   //GetFwVersionStr
                            {
                                this.controller.SetControlPropertyThreadSafe(controller.parent.label7, "Text", responseCmd.RawString.Trim());
                            }
                        }
                        else
                        { //received negative response Id... probably timeout
                            MessageBoxButtons buttons = MessageBoxButtons.YesNo;
                            DialogResult result;
                            result = MessageBox.Show(String.Format("Command {0} Timeout??", cmd.CmdId), "Timeout!", buttons);
                            continue;
                        }


                        controller._cmdMessenger.ClearReceiveQueue();
                        controller._cmdMessenger.ClearSendQueue();
                    }
                    else
                        Console.WriteLine(string.Format("Unknown command {n} issued\n", cmd.CmdId));
                }
            }


        }

    }
}