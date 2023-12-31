﻿using CommandMessenger;
using CommandMessenger.Transport.Serial;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.LinkLabel;

namespace StepperWF
{
    public class StepperController
    {
        public struct CommandStructure
        {
            public string name;
            public string description;
            public string parameters;
            public string returns;
            public int timeout;
            public int response;
        }
        public bool RunLoop { get; set; }
        public SerialTransport _serialTransport = null;
        public CmdMessenger _cmdMessenger = null;
        private int _receivedItemsCount = 0;                        // Counter of number of plain text items received
        private int _receivedBytesCount = 0;                        // Counter of number of plain text bytes received
        long _beginTime = 0;                                        // Start time, 1st item of sequence received 
        long _endTime = 0;                                          // End time, last item of sequence received 
        public Socket client;
        public Dictionary<string, int> CommandNumber = new Dictionary<string, int>();
        public PipeClient pipeClient;

        public CommandStructure[] commandStructure = new CommandStructure[] {
        new CommandStructure{name="Acknowledge",description=" //0  Command to acknowledge that cmd was received",parameters="",returns="i", timeout=1000, response= -1},
        new CommandStructure{name="Error",description="1  Command to report errors",parameters="",returns="", timeout=1000, response= -1},
        new CommandStructure { name = "FloatAddition", description = "2  Command to request add two floats", parameters = "ff",returns="" , timeout=1000, response= -1},
        new CommandStructure { name = "FloatAdditionResult", description = "3  Command to report addition result", parameters = "", returns="f", timeout=1000, response= -1},
        new CommandStructure { name = "GetFwVerNum", description = "4  Command to get firmware version as a float", parameters = "", returns="f", timeout=1000, response= -1},
        new CommandStructure { name = "GetFwVerStr", description = "5  Command to get firmware version as a string", parameters = "" , returns="s", timeout=1000, response= -1},
        new CommandStructure { name = "MoveToPosition", description = "6  Command to move current motor to absolute target position", parameters = "l" ,returns="", timeout=1000, response= 7},
        new CommandStructure { name = "GetCurrentMotorStatus", description = "7  Command to get current motor status", parameters = "" ,returns="ii", timeout=1000, response= -1},
        new CommandStructure { name = "CurrentMotorStop", description = "8  Command to stop current motor move", parameters = "" ,returns="i", timeout=1000, response= 7},
        new CommandStructure { name = "AwaitCurrentMotorMoveDone", description = "9  Command to await motor move completion", parameters = "" ,returns="i", timeout=100000, response= 7},
        new CommandStructure { name = "InitializeAxis", description = "10 Command to init current axis using optical switch", parameters = "" ,returns="fff", timeout=100000, response= 7},
        new CommandStructure { name = "SetMovePolarity", description = "11 Command to set move polarity; false is clockwise positive, reversed (true) is ccw positive", parameters = "b" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "MoveRelative", description = "12 Command to move motor to relative target position", parameters = "l" ,returns="", timeout=1000, response= 7},
        new CommandStructure { name = "GetPosition", description = "13 Command to get current motor position", parameters = "" ,returns="l", timeout=1000, response= -1},
        new CommandStructure { name = "SetMaxTravel", description = "14 Command to set max travel distance", parameters = "l" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "SetMaxSpeed", description = "15 Command to set max motor speed", parameters = "l" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "SetCurrentScaling", description = "16 Command to set motor current scaling (% of max current) -- added in V1.0.1", parameters = "i" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "SetCurrentAxis", description = "17 Command to set current axis (defaults to 1) -- added in V1.0.2", parameters = "i" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "MultiMoveToPosition", description = "18 Command to move multiple motors to absolute target position -- added in V1.0.3", parameters = "llll" ,returns="", timeout=1000, response= 7},
        new CommandStructure { name = "MultiMoveRelative", description = "19 Command to move multiple motors to relative target position -- added in V1.0.4", parameters = "llll" ,returns="i", timeout=1000, response= 7},
        new CommandStructure { name = "SetAcceleration", description = "20 Command to set acceleration value in steps/sec/sec (default is 50,000)", parameters = "l" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "MoveContinuous", description = "21 Command to rotate until stopped", parameters = "b" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "ResetPosition", description = "22 Command to reset the current motor position to zero", parameters = "" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "SetMaxHomeSearchMove", description = "23 Command to set max search distance for home", parameters = "l" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "SetHomingSpeed", description = "24 Command to set homing speed", parameters = "i" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "GetHomeFlagState", description = "25 Command to get home flag state for current axis", parameters = "" ,returns="i", timeout=1000, response= -1},
        new CommandStructure { name = "GetLastInitPosition", description = "26 Command to get last axis init position", parameters = "" ,returns="l", timeout=1000, response= -1},
        new CommandStructure { name = "SetMotorEnabledState", description = "27 Command to set flag specifying whether motor is enabled", parameters = "ib" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "LEDsOff", description = "28 Command to set color of all NeoPixels to Off", parameters = "" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "LEDsIdle", description = "29 Command to set color of all NeoPixels to Blue", parameters = "" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "LEDsRun", description = "30 Command to send green Wipe pattern to NeoPixels", parameters = "" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "LEDsError", description = "31 Command to send red Flash pattern to NeoPixels", parameters = "" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "GetADCRawVoltage", description = "32 Command to get ADC value in unscaled units -- added in V1.0.18", parameters = "" ,returns="f", timeout=1000, response= -1},
        new CommandStructure { name = "SetOutHigh", description = "33 Command to set an output pin high", parameters = "i" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "SetOutLow", description = "34 Command to set an output pin low", parameters = "i" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "SetLimitSwitchPolarity", description = "35 Command to set polarity of switches used; T=default (false if blocked)", parameters = "b" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "SetCurrentLimitSwitch", description = "36 Command to set currently active limit switch (it is auto-set on axis change)", parameters = "i" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "HInitializeXY", description = "37 Command to init X axis of H-Bot using optical switch", parameters = "" ,returns="", timeout=1000, response= 7},
        new CommandStructure { name = "HMoveRelative", description = "38 Command to move X,Y of H-Bot to relative target position", parameters = "" ,returns="", timeout=1000, response= 7},
        new CommandStructure { name = "HMoveToPosition", description = "39 Command to move X,Y of H-Bot to absolute target position; 3 params (1st param is 1, 2, 3 for X, Y, Both)", parameters = "ill" ,returns="", timeout=10000, response= 7},
        new CommandStructure { name = "HGetXY", description = "40 Command to get X,Y coordinate of H-Bot", parameters = "" ,returns="ii", timeout=1000, response= -1},
        new CommandStructure { name = "GetDebugStr", description = "41 Command to get Debug string", parameters = "" ,returns="s", timeout=1000, response= -1},
        new CommandStructure { name = "HMoveDoneMssg", description = "42 Message to communicate that an H-move is complete: HInitializeXY, HMoveRelative, HMoveToPosition", parameters = "" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "SetHoldCurrentScaling", description = "43 for compatibility with L6470 firmware", parameters = "" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "SetMicroStepModeL6470", description = "44 for compatibility with L6470 firmware", parameters = "" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "LEDsSetColor", description = "45 Command to set color of each NeoPixel (3)", parameters = "iii" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "EnableEOT", description = "46 Command to enable all EOT sensors to stop motion on change", parameters = "" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "DisableEOT", description = "47 Command to disable all EOT sensors to stop motion on change", parameters = "" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "Set2209StallThresh", description = "48 Command to set TMC2209 stall threshold value", parameters = "i" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "Set2209MotorCurrent", description = "49 Command to set TMC2209 motor current in mA", parameters = "i" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "Set2209MicroStepMode", description = "50 Command to set TMC2209 microstep mode -- param is 2,4,8,16,...,256", parameters = "i" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "StopOn2209Stall", description = "51 Command to set TMC2209 to stop when threshold exceeded -- param T|F", parameters = "b" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "Init2209", description = "52 Command to initialize the TMC2209", parameters = "" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "SensorOverride", description = "53 Command to stop on any of the 8 sensors", parameters = "ib" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "SensorPolarity", description = "54 Command to set polarity of any of the 8 sensors", parameters = "ib" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "GetSensorState", description = "55 Command to get state of any of the 8 sensors", parameters = "i" ,returns="b", timeout=1000, response= -1},
        new CommandStructure { name = "SetFlexiForceStallThresh", description = "56 Command to set FlexiForce stall threshold value for current motor", parameters = "i" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "StopOnFlexiForceStall", description = "57 Command to set current motor to stop when FlexiForce threshold exceeded -- param T|F", parameters = "b" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "GetFlexiForce", description = "58 Command to get last value of FlexiForce Sensor specified in the param (1 or 2)", parameters = "i" ,returns="i", timeout=1000, response= -1},
        new CommandStructure { name = "GetSwitchSet", description = "59 Command to get last value of Switch Block specified in the param (1 or 2 -- 1=microswitches, 2=optoswitches)", parameters = "i" ,returns="u", timeout=1000, response= -1},
        new CommandStructure { name = "StreamFlexiForceDebug", description = "60 Command to stream FlexiForce debug data -- param T|F", parameters = "b" ,returns="s", timeout=1000, response= -1},
        new CommandStructure { name = "StreamSwitchSetDebug", description = "61 Command to stream Switch settings (debug); Switch Block specified in the param (1 or 2 -- 1=microswitches, 2=optoswitches)", parameters = "" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "CallbacksForSwitchChanges", description = "62 Command to enable or disable callbacks on Switch Changes", parameters = "" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "LEDsSetPattern", description = "63 Command to set color and pattern of each NeoPixel (3) -- added V1.26", parameters = "iii" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "LEDsSetBrightness", description = "64 Command to set brightness of NeoPixels (3) -- added V1.26", parameters = "i" ,returns="", timeout=1000, response= -1},
        new CommandStructure { name = "GetReflectiveSensorValue", description = "65 Command to get last value of Reflective Sensor specified in the param (1 or 2)", parameters = "i" ,returns="i", timeout=1000, response= -1},
        new CommandStructure { name = "StreamReflectiveValues", description = "66 Command to stream Reflective debug data -- param T|F", parameters = "b" ,returns="s", timeout=1000, response= -1},
        new CommandStructure { name = "GetLightSensorValue", description = "67 Command to get last value of Light Sensor specified in the param (1 or 2)", parameters = "i" ,returns="i", timeout=1000, response= -1},
        new CommandStructure { name = "StreamLightValues", description = "68 Command to stream Light debug data -- param T|F", parameters = "b" ,returns="s", timeout=1000, response= -1}
    };
        public MacroRunner macroRunner;
        public string CurrentMacro;
        public Form1 parent;
        public StepperController( string runthis, Form1 parentIn )
        {
            parent = parentIn;
            int i = 0;
            foreach (CommandStructure command in commandStructure)
            {
                CommandNumber.Add( command.name, i++ );
            }
            CurrentMacro = runthis;
            _serialTransport = Setup();
            /* Write CommandStructure
            string json=Newtonsoft.Json.JsonConvert.SerializeObject( commandStructure, Formatting.Indented );
            string docPath = Environment.GetFolderPath( Environment.SpecialFolder.MyDocuments );
            using (StreamWriter outputFile = new StreamWriter( Path.Combine( docPath, "commandStructure.txt" ) ))
            {
                foreach (string line in json.Split('\n'))
                {
                    outputFile.WriteLine( line );
                }
            
            }
            */
            /*Read commandStructure
            string docPath = Environment.GetFolderPath( Environment.SpecialFolder.MyDocuments );
            string json=System.IO.File.ReadAllText( @Path.Combine( docPath, "commandStructure.txt" ) );
            CommandStructure[] loaded = new CommandStructure[] { };
            loaded= (CommandStructure[])Newtonsoft.Json.JsonConvert.DeserializeObject( json );
            */
        }


        public SerialTransport Setup()
        {
            if (_serialTransport != null)
                return _serialTransport;
            //get name of comport associated to Stepper (as obtained by Listports.py)
            ComPortMap map = new ComPortMap();
            var comport = map.GetComPort( "Stepper" );
            // Create Serial Port object
            _serialTransport = new SerialTransport
            {
                CurrentSerialSettings = { PortName = comport, BaudRate = 115200, Timeout = -1 } // object initializer
            };

            // Initialize the command messenger with the Serial Port transport layer
            // Set if it is communicating with a 16- or 32-bit Arduino board
            _cmdMessenger = new CmdMessenger( _serialTransport, BoardType.Bit16, ',', ';' );
            _cmdMessenger.PrintLfCr = false;

            // Attach the callbacks to the Command Messenger
            AttachCommandCallBacks();

            // Start listening
            _cmdMessenger.Connect();

            _receivedItemsCount = 0;
            _receivedBytesCount = 0;

            // Clear queues 
            _cmdMessenger.ClearReceiveQueue();
            _cmdMessenger.ClearSendQueue();

            Thread.Sleep( 100 );
            return _serialTransport;

        }
        // This is the list of recognized commands. These can be commands that can either be sent or received. 


        // In order to receive, attach a callback function to these events
        enum StepperCommand
        {
            // Commands
            kAcknowledge, //0  Command to acknowledge that cmd was received
            kError, //1  Command to report errors
            kFloatAddition, //2  Command to request add two floats
            kFloatAdditionResult, //3  Command to report addition result
            kGetFwVerNum, //4  Command to get firmware version as a float
            kGetFwVerStr, //5  Command to get firmware version as a string
            kMoveToPosition, //6  Command to move current motor to absolute target position
            kGetCurrentMotorStatus, //7  Command to get current motor status
            kCurrentMotorStop, //8  Command to stop current motor move
            kAwaitCurrentMotorMoveDone, //9  Command to await motor move completion
            kInitializeAxis, //10 Command to init current axis using optical switch
            kSetMovePolarity, //11 Command to set move polarity; false is clockwise positive, reversed (true) is ccw positive
            kMoveRelative, //12 Command to move motor to relative target position
            kGetPosition, //13 Command to get current motor position
            kSetMaxTravel, //14 Command to set max travel distance
            kSetMaxSpeed, //15 Command to set max motor speed
            kSetCurrentScaling, //16 Command to set motor current scaling (% of max current) -- added in V1.0.1
            kSetCurrentAxis, //17 Command to set current axis (defaults to 1) -- added in V1.0.2
            kMultiMoveToPosition, //18 Command to move multiple motors to absolute target position -- added in V1.0.3
            kMultiMoveRelative, //19 Command to move multiple motors to relative target position -- added in V1.0.4
            kSetAcceleration, //20 Command to set acceleration value in steps/sec/sec (default is 50,000)
            kMoveContinuous, //21 Command to rotate until stopped
            kResetPosition, //22 Command to reset the current motor position to zero
            kSetMaxHomeSearchMove, //23 Command to set max search distance for home
            kSetHomingSpeed, //24 Command to set homing speed
            kGetHomeFlagState, //25 Command to get home flag state for current axis
            kGetLastInitPosition, //26 Command to get last axis init position
            kSetMotorEnabledState, //27 Command to set flag specifying whether motor is enabled
            kLEDsOff, //28 Command to set color of all NeoPixels to Off
            kLEDsIdle, //29 Command to set color of all NeoPixels to Blue
            kLEDsRun, //30 Command to send green Wipe pattern to NeoPixels
            kLEDsError, //31 Command to send red Flash pattern to NeoPixels
            kGetADCRawVoltage, //32 Command to get ADC value in unscaled units -- added in V1.0.18
            kSetOutHigh, //33 Command to set an output pin high
            kSetOutLow, //34 Command to set an output pin low
            kSetLimitSwitchPolarity, //35 Command to set polarity of switches used; T=default (false if blocked)
            kSetCurrentLimitSwitch, //36 Command to set currently active limit switch (it is auto-set on axis change)
            kHInitializeXY, //37 Command to init X axis of H-Bot using optical switch
            kHMoveRelative, //38 Command to move X,Y of H-Bot to relative target position
            kHMoveToPosition, //39 Command to move X,Y of H-Bot to absolute target position; 3 params (1st param is 1, 2, 3 for X, Y, Both)
            kHGetXY, //40 Command to get X,Y coordinate of H-Bot
            kGetDebugStr, //41 Command to get Debug string
            kHMoveDoneMssg, //42 Message to communicate that an H-move is complete: HInitializeXY, HMoveRelative, HMoveToPosition
            kSetHoldCurrentScaling, //43 for compatibility with L6470 firmware
            kSetMicroStepModeL6470, //44 for compatibility with L6470 firmware
            kLEDsSetColor, //45 Command to set color of each NeoPixel (3)
            kEnableEOT, //46 Command to enable all EOT sensors to stop motion on change
            kDisableEOT, //47 Command to disable all EOT sensors to stop motion on change
            kSet2209StallThresh, //48 Command to set TMC2209 stall threshold value
            kSet2209MotorCurrent, //49 Command to set TMC2209 motor current in mA
            kSet2209MicroStepMode, //50 Command to set TMC2209 microstep mode -- param is 2,4,8,16,...,256
            kStopOn2209Stall, //51 Command to set TMC2209 to stop when threshold exceeded -- param T|F
            kInit2209, //52 Command to initialize the TMC2209
            kSensorOverride, //53 Command to stop on any of the 8 sensors
            kSensorPolarity, //54 Command to set polarity of any of the 8 sensors
            kGetSensorState, //55 Command to get state of any of the 8 sensors
            kSetFlexiForceStallThresh, //56 Command to set FlexiForce stall threshold value for current motor
            kStopOnFlexiForceStall, //57 Command to set current motor to stop when FlexiForce threshold exceeded -- param T|F
            kGetFlexiForce, //58 Command to get last value of FlexiForce Sensor specified in the param (1 or 2)
            kGetSwitchSet, //59 Command to get last value of Switch Block specified in the param (1 or 2 -- 1=microswitches, 2=optoswitches)
            kStreamFlexiForceDebug, //60 Command to stream FlexiForce debug data -- param T|F
            kStreamSwitchSetDebug, //61 Command to stream Switch settings (debug); Switch Block specified in the param (1 or 2 -- 1=microswitches, 2=optoswitches)
            kCallbacksForSwitchChanges, //62 Command to enable or disable callbacks on Switch Changes
            kLEDsSetPattern, //63 Command to set color and pattern of each NeoPixel (3) -- added V1.26
            kLEDsSetBrightness, //64 Command to set brightness of NeoPixels (3) -- added V1.26
            kGetReflectiveSensorValue, //65 Command to get last value of Reflective Sensor specified in the param (1 or 2)
            kStreamReflectiveValues, //66 Command to stream Reflective debug data -- param T|F
            kGetLightSensorValue, //67 Command to get last value of Light Sensor specified in the param (1 or 2)
            kStreamLightValues, //68 Command to stream Light debug data -- param T|F
                                //------------------------------------------------------------------------------------------------------------------------------------------
        };

        private delegate void SetControlPropertyThreadSafeDelegate(
                         System.Windows.Forms.Control control,
                         string propertyName,
                         object propertyValue );

        public void SetControlPropertyThreadSafe(
            Control control,
            string propertyName,
            object propertyValue )
        {
            if (control.InvokeRequired)
            {
                control.Invoke( new SetControlPropertyThreadSafeDelegate
                ( SetControlPropertyThreadSafe ),
                new object[] { control, propertyName, propertyValue } );
            }
            else
            {
                control.GetType().InvokeMember(
                    propertyName,
                    BindingFlags.SetProperty,
                    null,
                    control,
                    new object[] { propertyValue } );
            }
        }

        /// Attach command call backs. 
        private void AttachCommandCallBacks()
        {
            _cmdMessenger.Attach( OnUnknownCommand );
            _cmdMessenger.Attach( (int)StepperCommand.kGetCurrentMotorStatus, OnGetCurrentMotorStatus );
            _cmdMessenger.Attach( (int)StepperCommand.kAcknowledge, OnAcknowledge );
            _cmdMessenger.Attach( (int)StepperCommand.kError, OnError );
            _cmdMessenger.Attach( (int)StepperCommand.kGetDebugStr, OnGetDebugStr );
            _cmdMessenger.Attach( (int)StepperCommand.kHMoveDoneMssg, OnHMoveDoneMssg );
            _cmdMessenger.Attach( (int)StepperCommand.kGetFwVerStr, OnGetFwVerStr );

        }

        // Called when a received command has no attached function.
        void OnUnknownCommand( ReceivedCommand arguments )
        {
            Console.WriteLine( "Command without attached callback received" );
        }

        void OnGetCurrentMotorStatus( ReceivedCommand arguments )
        {
            Console.WriteLine( "Status received" );
        }

        void OnAcknowledge( ReceivedCommand arguments )
        {
            Console.WriteLine( "Acknowledge received" );
        }

        void OnError( ReceivedCommand arguments )
        {
            Console.WriteLine( "Error received" );
        }
        void OnGetDebugStr( ReceivedCommand arguments )
        {
            Console.WriteLine( "Debug string received" );
        }
        void OnHMoveDoneMssg( ReceivedCommand arguments )
        {
            Console.WriteLine( "HMoveDone received" );
        }
        void OnGetFwVerStr( ReceivedCommand arguments )
        {
            SetControlPropertyThreadSafe( parent.button3, "Text", arguments.Arguments[0] );
            //parent.SetStatus(arguments.Arguments[0]);
        }
        public void Exit()
        {
            // Stop listening
            _cmdMessenger.Disconnect();

            // Dispose Command Messenger
            _cmdMessenger.Dispose();

            // Dispose Serial Port object
            _serialTransport.Dispose();

            // Pause before stop
            //Console.WriteLine( "Press any key to stop..." );
            //Console.ReadKey();
        }


        async public Task SocketMode( string[] CmdLineArgs )
        {
            PipeClient pipeClient = new PipeClient();
            var mr = new MacroRunner( this, pipeClient, null );
            //Thread macroThread = new Thread( new ThreadStart( mr.RunMacro ) );
            mr.RunMacro();
        }
    }
}