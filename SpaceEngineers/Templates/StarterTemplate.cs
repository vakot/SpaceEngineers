#region Prelude
using System;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;

using VRageMath;
using VRage.Game;
using VRage.Collections;
using Sandbox.ModAPI.Ingame;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using Sandbox.Game.EntityComponents;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;

// Change this namespace for each script you create.
namespace SpaceEngineers.UWBlockPrograms.StarterTemplate {
    public sealed class Program : MyGridProgram {
#endregion Prelude

#region Description
// ------------------------------------------------------------------------------------------------------ \\
// ========= Script Name Goes Here ========= \\
// ------------------------------------------------------------------------------------------------------ \\

// ------------------ DESCRIPTION ------------------ \\

/*
* 
*/

// ---------------- ARGUMENTS LIST ----------------- \\

/*
* 
*/

// ----------------- CONFIGURATION ----------------- \\

/* Change it only before first run of program, rest of time use PB Custom Data */

public readonly static List<string> RunStatus = new List<string>
{
    "[|---]", 
    "[-|--]", 
    "[--|-]", 
    "[---|]", 
    "[--|-]", 
    "[-|--]"
};

public const UpdateFrequency RuntimeUpdateFrequency = UpdateFrequency.Update1; // for activatable scripts (comment for endless scripts)

// -------------------------------------------------------------------------------------------------------- \\
// ========== !!! DONT CHANGE ANYTHING BELOW THIS LINE !!! =========== \\
// -------------------------------------------------------------------------------------------------------- \\
#endregion Description

        #region Variables
        int Counter = 0;

        public enum Status
        {
            Active
        }

        public Status _Status { get; private set; } = Status.Active;

        MyIni _ini = new MyIni();

        public const string IniSectionName = "Script - General",
                            IniKeyName = "Value (def: Value)",

        public string Value { get; private set; } = "Value";

        #endregion Variables

        void Report()
        {
            string Report = $"Script is {_Status} {RunStatus[Runtime.UpdateFrequency == UpdateFrequency.Update10 ? Counter % RunStatus.Count : Counter / 10 % RunStatus.Count]}";

            Report += $"\n------ Block's Info ------\n";

            Report += $"\n------ Runtime Info ------\n";
            Report += $"Instruction's Count: {Runtime.CurrentInstructionCount}/{Runtime.MaxInstructionCount}\n";
            Report += $"Call Chain Depth: {Runtime.CurrentCallChainDepth}/{Runtime.MaxCallChainDepth}\n";
            Report += $"Time Since Last Run: {Runtime.TimeSinceLastRun.Milliseconds}ms\n";
            Report += $"Last Runtime Took: {Runtime.LastRunTimeMs}ms\n";
            Echo(Report);
        }

        Program() 
        {
            Update();
            Report();
            Runtime.UpdateFrequency = UpdateFrequency.Update1; // for endless scripts (comment for activatable scripts)
        }

        void Main(string argument)
        {
            Counter++;
            Report();

            if (_Status == Status.Idle && string.IsNullOrWhiteSpace(argument))
            {
                Runtime.UpdateFrequency = UpdateFrequency.None; // for activatable scripts (comment for endless scripts)
                return;
            }
            else if (!string.IsNullOrWhiteSpace(argument))
            {
                // Runtime.UpdateFrequency = RuntimeUpdateFrequency; // for activatable scripts (comment for endless scripts)
                Update();
                Run(argument);
            } else {
                Run();
            }
        }

        void Run()
        {
            do_empty();
        }

        void Run(string argument)
        {
            if (argument.ToLower() == "argument")
            {
                do_1();
            }
            else if (!string.IsNullOrWhiteSpace(argument))
            {
                do_2();
            }
        }

        void Update()
        {
            ParseIni();
        }

        void ParseIni()
        {
            _ini.Clear();

            if (_ini.TryParse(Me.CustomData))
            {
                Value = _ini.Get(IniSectionName, IniKeyName).ToString(Value);
            }
            else if (!string.IsNullOrWhiteSpace(Me.CustomData))
            {
                _ini.EndContent = Me.CustomData;
            }

            _ini.Set(IniSectionName, IniKeyName, Value);

            string Output = _ini.ToString();
            if (Output != Me.CustomData)
            {
                Me.CustomData = Output;
            }
        }

        #region Control
        #endregion Control

        #region Helpers
        #endregion Helpers

#region PreludeFooter
    }
}
#endregion PreludeFooter