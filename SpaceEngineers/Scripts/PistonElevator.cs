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
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.GUI.TextPanel;

using SpaceEngineers.Shared.SurfaceContentManager;

namespace SpaceEngineers.UWBlockPrograms.PistonElevator
{
    public sealed class Program : MyGridProgram
    {
        #endregion Prelude

#region Description
// ------------------------------------------------------------------------------------------------------ \\
// ========= Vakot Ind. Advanced Piston Elevator Manager Class ========= \\
// ------------------------------------------------------------------------------------------------------ \\

// ------------------ DESCRIPTION ------------------ \\

/*
 *  Piston group will include all parts of your elevator
 *  - Piston's
 *  - Door's (optionally)
 *  - Light's (optionally)
 *  - Sound Block's (optionally)
 *  Script will automatically manage their state to prevent most of posible issues
 *  
 *  Installation:
 *  Build a piston elevator
 *  Place Programmable Block and configurate for yourself
 *  Use!
 */

// ---------------- ARGUMENTS LIST ----------------- \\

/*
 *  Up - Get 1 floor up
 *  Down - Get 1 floor down
 *  [number] - Get to a specific floor
 */

// ----------------- CONFIGURATION ----------------- \\

/* Change it only before first run of program, rest of time use PB Custom Data */

public readonly static List<string> RunStatus = new List<string>()
{
    "[|---]", 
    "[-|--]", 
    "[--|-]", 
    "[---|]", 
    "[--|-]", 
    "[-|--]"
};

public const UpdateFrequency RuntimeUpdateFrequency = UpdateFrequency.Update1;

public const string IniSectionElevatorGeneral = "Elevator Manager - General",
                    IniKeyGroupName = "Elevator group name",
                    IniKeyConnectionPoints = "Piston's connection point's",
                    IniSectionElevatorMovement = "Elevator Manager - Movement",
                    IniKeyAcceleration = "Acceleration (m/s^2)",
                    IniKeyTargetVelocity = "Target velocity (m/s)",
                    IniSectionElevatorLight = "Elevator Manager - Light",
                    IniKeyIdleColor = "Idle color",
                    IniKeyActiveColor = "Active color",
                    IniSectionElevatorFloors = "Elevator Manager - Floors",
                    IniKeyFloors = "Floors list";

// -------------------------------------------------------------------------------------------------------- \\
// ========== !!! DONT CHANGE ANYTHING BELOW THIS LINE !!! =========== \\
// -------------------------------------------------------------------------------------------------------- \\
#endregion Description

        #region Variables
        int Counter = 0;

        public enum Status
        {
            Active,
            Ready,
            Preparing,
            Idle
        }

        public Status _Status { get; private set; } = Status.Idle;

        MyIni _ini = new MyIni();



        // Name of ElevatorGroup
        public string ElevatorGroupName { get; private set; } = "Elevator";

        // Elevator acceleration m/s^2 (every second increase velocity by Acceleration = [value]f)
        private float _Acceleration = 0.5f;
        // Elevator target speed m/s (max elevator up/down velocity)
        private float _TargetVelocity = 5f;

        // Count of piston connection points to elevator
        private int _PistonConnectionPoints = 1;

        // Default light color (when the elevator is idle)
        private Color _IdleColor = new Color(255, 255, 255);
        // Active light color (when the elevator is moving)
        private Color _ActiveColor = new Color(255, 160, 0);

        // Elevator floors list (to set the floor - move all your pistons at the same time to height
        // where you want the floor to be and write [Piston Current Position] value to the list)
        // Be careful and dont use urecheable values, pistons can't move higher or lower than they can
        // Example: { 0f, 2.5f, 5f, 7.5f, 10f }
        private List<float> _Floors = new List<float>() { 0f, 2.5f, 5f, 7.5f, 10f };

        private List<IMyPistonBase> _Pistons = new List<IMyPistonBase>();
        private List<IMyDoor> _Doors = new List<IMyDoor>();
        private List<IMyLightingBlock> _Lights = new List<IMyLightingBlock>();
        private List<IMySoundBlock> _SoundBlocks = new List<IMySoundBlock>();

        private int _Count;
        public int CurrentFloor { get; private set; } = 0;
        public float CurrentVelocity { get; private set; } = 0f;

        public float StartPosition { get; private set; } = 0f;
        public float TargetPosition { get; private set; } = 0f;
        #endregion Variables

        void Report()
        {
            string Report = $"Elevator is {_Status} {RunStatus[Runtime.UpdateFrequency == UpdateFrequency.Update10 ? Counter % RunStatus.Count : Counter / 10 % RunStatus.Count]}";

            Report += $"\n------ Block's Info ------\n";
            Report += $"Piston's: {PistonsCount}\n";

            if (_Status == Status.Active)
            {
                Report += $"Velocity: {CurrentVelocity}m/s\n";
                Report += $"Start Position: {StartPosition}m\n";
                Report += $"Current Position: {CurrentPosition()}m\n";
                Report += $"Target Position: {TargetPosition}m\n";
            }

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
        }

        void Main(string argument)
        {
            Counter++;
            Report();

            if (_Status == Status.Idle && string.IsNullOrWhiteSpace(argument))
            {
                Runtime.UpdateFrequency = UpdateFrequency.None;
                return;
            }
            else if (!string.IsNullOrWhiteSpace(argument))
            {
                Runtime.UpdateFrequency = RuntimeUpdateFrequency;
                Update();
                Run(argument);
            } else {
                Run();
            }
        }

        void Run()
        {
            if (_Status == Status.Preparing) {
                Prepare();
                return;
            }

            if (_Pistons.Count <= 0
            || _Count <= 0
            || _PistonConnectionPoints <= 0
            || _Acceleration <= 0
            || _TargetVelocity <= 0) return;

            if (Math.Abs(CurrentPosition() - TargetPosition) > 0.01f)
            {
                Move();
            }
            else
            {
                Stop();
            }
        }

        void Run(string argument)
        {
            if (argument.ToLower() == "up")
            {
                Start(CurrentFloor + 1);
            }
            else if (argument.ToLower() == "down")
            {
                Start(CurrentFloor - 1);
            }
            else if (!string.IsNullOrWhiteSpace(argument))
            {
                int number;
                Int32.TryParse(argument, out number);

                if (number >= 0 || number < _Floors.Count)
                {
                    Start(number);
                }
            }
        }

        void Update()
        {
            ParseIni();

            _Pistons.Clear();
            _Doors.Clear();
            _Lights.Clear();
            _SoundBlocks.Clear();

            IMyBlockGroup ElevatorGroup = GridTerminalSystem.GetBlockGroupWithName(ElevatorGroupName);

            if (ElevatorGroup == null) return;

            ElevatorGroup.GetBlocksOfType<IMyPistonBase>(_Pistons);
            ElevatorGroup.GetBlocksOfType<IMyDoor>(_Doors);
            ElevatorGroup.GetBlocksOfType<IMyLightingBlock>(_Lights);
            ElevatorGroup.GetBlocksOfType<IMySoundBlock>(_SoundBlocks);

            _Count = _Pistons.Count / Math.Max(_PistonConnectionPoints, 1);
        }

        void ParseIni()
        {
            _ini.Clear();

            string IdleColor = _IdleColor.ToString();
            string ActiveColor = _ActiveColor.ToString();
            string Floors = string.Join(",", _Floors);

            if (_ini.TryParse(Me.CustomData))
            {
                ElevatorGroupName = _ini.Get(IniSectionElevatorGeneral, IniKeyGroupName).ToString(ElevatorGroupName);
                _PistonConnectionPoints = _ini.Get(IniSectionElevatorGeneral, IniKeyConnectionPoints).ToInt32(_PistonConnectionPoints);

                _Acceleration = _ini.Get(IniSectionElevatorMovement, IniKeyAcceleration).ToSingle(_Acceleration);
                _TargetVelocity = _ini.Get(IniSectionElevatorMovement, IniKeyTargetVelocity).ToSingle(_TargetVelocity);

                IdleColor = _ini.Get(IniSectionElevatorLight, IniKeyIdleColor).ToString(IdleColor);
                ActiveColor = _ini.Get(IniSectionElevatorLight, IniKeyActiveColor).ToString(ActiveColor);

                Floors = _ini.Get(IniSectionElevatorFloors, IniKeyFloors).ToString(Floors);
            }
            else if (!string.IsNullOrWhiteSpace(Me.CustomData))
            {
                _ini.EndContent = Me.CustomData;
            }

            _ini.Set(IniSectionElevatorGeneral, IniKeyGroupName, ElevatorGroupName);
            _ini.Set(IniSectionElevatorGeneral, IniKeyConnectionPoints, _PistonConnectionPoints);

            _ini.Set(IniSectionElevatorMovement, IniKeyAcceleration, _Acceleration);
            _ini.Set(IniSectionElevatorMovement, IniKeyTargetVelocity, _TargetVelocity);

            _ini.Set(IniSectionElevatorLight, IniKeyIdleColor, IdleColor);
            _ini.Set(IniSectionElevatorLight, IniKeyActiveColor, ActiveColor);

            _ini.Set(IniSectionElevatorFloors, IniKeyFloors, Floors);

            string Output = _ini.ToString();
            if (Output != Me.CustomData)
            {
                Me.CustomData = Output;
            }

            _IdleColor = TryParseColor(IdleColor);
            _ActiveColor = TryParseColor(ActiveColor);
            _Floors = TryParseList(Floors);
        }

        #region Control
        private void Start(int Floor)
        {
            _Status = Status.Preparing;
            StartPosition = CurrentPosition();
            CurrentFloor = Math.Max(Math.Min(Floor, _Floors.Count - 1), 0);
            TargetPosition = _Floors[CurrentFloor] * _Count;
            Prepare();
        }

        private void Move()
        {
            _Status = Status.Active;

            CurrentVelocity = GetVelocity();

            int Direction = StartPosition >= TargetPosition ? -1 : 1;

            foreach (IMyPistonBase Piston in _Pistons)
            {
                Piston.Velocity = Math.Min(_TargetVelocity / _Count, CurrentVelocity) * Direction;
            }
        }

        private void Stop()
        {
            _Status = Status.Idle;
            CurrentVelocity = 0;

            foreach (IMyPistonBase Piston in _Pistons)
            {
                Piston.Velocity = 0;
            }

            foreach (IMyDoor Door in _Doors)
            {
                Door.ApplyAction("OnOff_On");
                Door.OpenDoor();
            }

            foreach (IMyLightingBlock Light in _Lights)
            {
                Light.Color = _IdleColor;
                if (Light.CustomData.ToLower().Contains("off")) Light.ApplyAction("OnOff_Off");
            }

            foreach (IMySoundBlock SoundBlock in _SoundBlocks)
            {
                SoundBlock.Volume = 0;
                SoundBlock.Stop();
            }
        }
        #endregion Control

        #region Helpers
        public int PistonsCount => _Pistons.Count;

        public float CurrentPosition()
        {
            float Sum = 0;
            foreach (IMyPistonBase Piston in _Pistons)
            {
                Sum += Piston.CurrentPosition;
            }
            return Sum;
        }

        public float GetVelocity()
        {
            float _CurrentPosition = CurrentPosition();

            float TotalDistance = Math.Abs(TargetPosition - StartPosition);
            float CurrentOffset = Math.Abs(_CurrentPosition - StartPosition);

            float Midpoint = TotalDistance / 2;

            float Velocity = CurrentOffset <= Midpoint ? _Acceleration * CurrentOffset : _Acceleration * (TotalDistance - CurrentOffset);

            return Math.Max(Math.Min(Velocity, _TargetVelocity), 0.05f);
        }

        private Color TryParseColor(string Str)
        {
            try
            {
                if (Str[0] != '{' || Str[Str.Length - 1] != '}') throw new Exception();

                string[] Split = Str.Substring(1, Str.Length - 2).Split(' ');
                if (Split.Length != 4) throw new Exception();

                int[] RGBA = new int[] { 0, 0, 0, 255 };
                for(int i = 0; i < Split.Length; i++)
                {
                    RGBA[i] = int.Parse(Split[i].Substring(2, Split[i].Length - 2));
                }

                return new Color(RGBA[0], RGBA[1], RGBA[2], RGBA[3]);
            }
            catch { return Color.Transparent; }
        }

        private List<float> TryParseList(string Str)
        {
            try
            {
                string[] Split = Str.Split(',');

                List<float> Floors = new List<float>();

                foreach (string str in Split) Floors.Add(Convert.ToSingle(str));

                if (Floors.Count <= 0) throw new Exception();
                return Floors;
            }
            catch { return _Floors; }
        }
        #endregion Helpers

        #region Changers
        private void Prepare()
        {
            foreach (IMyPistonBase Piston in _Pistons)
            {
                Piston.MaxLimit = TargetPosition / _Count;
                Piston.MinLimit = TargetPosition / _Count;
            }

            foreach (IMyDoor Door in _Doors)
            {
                Door.CloseDoor();
                if (Door.OpenRatio == 0)
                {
                    _Status = Status.Ready;
                    Door.ApplyAction("OnOff_Off");
                }
                else
                {
                    Door.ApplyAction("OnOff_On");
                }
            }

            foreach (IMyLightingBlock Light in _Lights)
            {
                Light.ApplyAction("OnOff_On");
                Light.Color = _ActiveColor;
            }

            foreach (IMySoundBlock SoundBlock in _SoundBlocks)
            {
                SoundBlock.ApplyAction("OnOff_On");
                if (SoundBlock.Volume == 0 && SoundBlock.IsSoundSelected)
                {
                    SoundBlock.LoopPeriod = 1800;
                    SoundBlock.Volume = 50;
                    SoundBlock.Play();
                }
            }
        }
        #endregion Changers

#region PreludeFooter
    }
}
#endregion PreludeFooter