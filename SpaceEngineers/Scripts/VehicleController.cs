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

namespace SpaceEngineers.UWBlockPrograms.VehicleController
{
    public sealed class Program : MyGridProgram
    {
        #endregion Prelude

#region Description
// -------------------------------------------------------------------------------------------------------- \\
// ============= Vakot Ind. Simple Vehicle Controller Script ============= \\
// -------------------------------------------------------------------------------------------------------- \\

// ------------------ DESCRIPTION ------------------ \\

/*
 *  Script use all found wheel's suspension in all subgrid's (exclude connected with connector's).
 *  You also can exclude wheel's suspension from being controlled using "Ignore" keyword.
 *  The keyword is case insensitive.
 *  Exclude example: "Wheel Suspension 3x3 Right iGnOrE"
 *
 *  Script have the light manager. It use the same light source for Brake and Reverse Light's.
 *  You also can include light's to being controlled using [BrakeLightTag] keyword (Set in CONFIGURATION).
 *  The keyword is case insensitive.
 *  Include example: "Interior Light - BrAkE"
 *
 *  To print information to LCD use [LCDTag] keyword (Set in CONFIGURATION) and write to custom data @<display-index>.
 *
 *  Main controller will be choosed by this priority:
 *  1. Controller in under control at moment
 *  2. Controller with [ControllerName] name value
 *  3. Main ship controller
 *  4. First found controller which is not Remote Control
 *  5. First found controller even if it Remote Control
 *
 *  If your main grid does not contain at least 1 wheel suspension - to stop vehicle use "Space" button or "Brake" run argument.
 *  If Safe Mode is enabled, handbrake will be automatically enabled when you leave the cockpit
 *
 *  Installation:
 *  Build a vehicle
 *  Place Programmable Block on it and configurate for yourself
 *  Drive!
 */

// ---------------- ARGUMENTS LIST ----------------- \\

/*
 *  Up - Set suspension height to [MaxHeight] value
 *  Down - Set suspension height to [MinHeight] value
 *  UpDown - Switch between Up & Down
 *  Friction - Toggle suspension friction between [MinFriction] & [MaxFriction] values
 *  Power - Toggle suspension power-limit between [TargetPower] & [MaxPower] values
 *  Climb - Run Friction & Power arguments in the same time (use to climb a steep slope)
 *  Cruise - Toggle cruise control on/off (you can manage cruise speed with acceleration button's [w, s])
 *  Safe - Toggle safe mode on/off
 *  Brake - Toggle handbrake on/off (work only if your main grid does not contains any wheel's suspension)
 *
 *  LCD CustomData argument's:
 *  - speed (displays minimalistic speed stats)
 *  - dashboard (displays large pane dashboard of vehicle statuses)
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
public const string Version = "2.3",
                    IniSectionGeneral = "General";
public const string IniSectionControllerGeneral = "Vehilce Controller - General",
                    IniKeyControllerName = "Controller name",
                    IniKeyAutomaticNewWheelsAdd = "Automatic add wheels (every 10s) (def: true)",
                    IniKeySubgridControl = "Subgrid control (def: true)",
                    IniKeyCruiseAutoOff = "Cruise auto off (def: false)",
                    IniKeySmoothDriving = "Smooth driving (torque) (def: true)",
                    IniKeyUseCustomAirShock = "Custom air shock (def: true)",

                    IniSectionControllerLight = "Vehilce Controller - Light",
                    IniKeyBrakeLightKeyword = "Light keyword",
                    IniKeyReverseManagement = "Reverse light's management (def: true)",
                    IniKeyHabariteManagement = "Habarite light's management (def: true)",

                    IniSectionControllerStrength = "Vehilce Controller - Strength (in %)",
                    IniKeyTargetStrength = "Target strength (def: 10)",

                    IniSectionControllerSpeed = "Vehilce Controller - Speed (in km/h)",
                    IniKeyForwardSpeedLimit = "Forward speed limit (def: 130)",
                    IniKeyBackwardSpeedLimit = "Backward speed limit (def: 20)",

                    IniSectionControllerSteer = "Vehilce Controller - Steer (in deg)",
                    IniKeySteerAngle = "Steer angle (def: 18)",

                    IniSectionControllerPower = "Vehilce Controller - Power (in %)",
                    IniKeyMinPower = "Min power (def: 10)",
                    IniKeyTargetPower = "Target power (def: 40)",
                    IniKeyMaxPower = "Max power (def: 100)",

                    IniSectionControllerFriction = "Vehilce Controller - Friction (in %)",
                    IniKeyMinFriction = "Min friction (def: 35)",
                    IniKeyMaxFriction = "Max friction (def:100)",

                    IniSectionControllerHeight = "Vehilce Controller - Height (in m)",
                    IniKeyMinHeight = "Min height (def: 0)",
                    IniKeyMaxHeight = "Max height (def: 10)";

// -------------------------------------------------------------------------------------------------------- \\
// ========== !!! DONT CHANGE ANYTHING BELOW THIS LINE !!! =========== \\
// -------------------------------------------------------------------------------------------------------- \\
#endregion Description

        #region Variables
        private MyIni _ini = new MyIni();

        private SurfaceContentManager _SurfaceContentManager;
        private static List<IMyMotorSuspension> _Suspensions = new List<IMyMotorSuspension>();
        private static List<IMyLightingBlock> _Lights = new List<IMyLightingBlock>();
        private static IMyShipController _Controller;

        #region Config
        public string ControllerName { get; private set; } = "";
        public string BrakeLightTag { get; private set; } = "Brake";

        // Determins is subrid wheel's should be controlled
        public bool SubgridControl { get; private set; } = true;

        // Make vehicle stop if player leave cockpit
        public bool SafeMode { get; private set; } = true;

        // Reverse light
        public bool ReverseManagement { get; private set; } = true;
        // Don't off light, just make it less intensive
        public bool HabariteManagement { get; private set; } = true;

        // If true will add a new wheel (if old is missin) every 10 seconds (on script update)
        public bool AutomaticNewWheelsAdd { get; private set; } = true;
        // If true - turn Cruise Mode off when cruise speed less then 6m/s
        public bool CruiseAutoOff { get; private set; } = false;
        // Make vehicle accelerate smooth and add the torque bar
        public bool SmoothDriving { get; private set; } = true;
        // Replace vanilla AirShock system
        public bool UseCustomAirShock { get; private set; } = true;

        public float TargetStrength { get; private set; } = 10;

        public static float ForwardSpeedLimit { get; private set; } = 130;
        public static float BackwardSpeedLimit { get; private set; } = 20;

        public float SteerAngle { get; private set; } = 18;

        public float MinPower { get; private set; } = 10;
        public float TargetPower { get; private set; } = 40;
        public float MaxPower { get; private set; } = 100;

        public float MinFriction { get; private set; } = 35;
        public float MaxFriction { get; private set; } = 100;

        public float MinHeight { get; private set; } = 0;
        public float MaxHeight { get; private set; } = 10;
        #endregion Config

        private static bool _isCustomHandBrake;
        private static bool _isBrakeEnabled = true;

        private static bool _CruiseMode = false;
        private static float _CruiseSpeed = 0;

        private static float _CurrentTorque = 0;
        private static float _PropulsionMultiplier = 0;

        private static float _CurrentPowerLimit;
        private static float _CurrentPower => _Suspensions.Count > 0 ? _Suspensions[0].Power : 0f;
        private static float _CurrentPropulsion => _Suspensions.Count > 0 ? Math.Abs(_Suspensions[0].PropulsionOverride) : 0f;
        private static float _CurrentFriction;
        private static float _CurrentHeight;
        private static float _CurrentStrength;

        int Counter = 0;
        #endregion Variables

        void Report()
        {
            string Status = $"Vehicle Controller {RunStatus[Runtime.UpdateFrequency == UpdateFrequency.Update10 ? Counter % RunStatus.Count : Counter / 10 % RunStatus.Count]}";
            Status += $"\nNext update in: {10 - Counter / 6}" + "s\n";

            Status += $"\n------ Block's Info ------\n";
            Status += $"Controller: {ControllerNameSafe}\n";
            Status += $"Suspension's: {SuspensionsCount}\n";
            Status += $"Light's: {LightsCount}\n";

            Status += $"\n------ Runtime Info ------\n";
            Status += $"Instruction's Count: {Runtime.CurrentInstructionCount}/{Runtime.MaxInstructionCount}\n";
            Status += $"Call Chain Depth: {Runtime.CurrentCallChainDepth}/{Runtime.MaxCallChainDepth}\n";
            Status += $"Time Since Last Run: {Runtime.TimeSinceLastRun.Milliseconds}ms\n";
            Status += $"Last Runtime Took: {Runtime.LastRunTimeMs}ms\n";
            Echo(Status);
        }

        Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;

            _CurrentPowerLimit = TargetPower;
            _CurrentFriction = MinFriction;
            _CurrentHeight = -MaxHeight;
            _CurrentStrength = TargetStrength;

            _SurfaceContentManager = new SurfaceContentManager(this);
            _SurfaceContentManager.AddContentType("dashboard", DrawDashboard);
            _SurfaceContentManager.AddContentType("speed", DrawSpeed);

            Update();
            Report();
        }

        void Main(string argument)
        {
            if (++Counter % 60 == 0) Update();

            Report();

            if (!string.IsNullOrWhiteSpace(argument))
            {
                Run(argument);
            }
            else
            {
                Run();
            }
        }

        void Run()
        {
            if (!IsValid(_Controller))
            {
                _Controller = GetShipController();
                return;
            }
            // if (!_Controller.IsUnderControl) _Controller = GetShipController();

            ManageSafeMode();
            ManageTorque();
            ManageLights();
            ManageCruise();
            ManageWheels();

            _SurfaceContentManager.DrawContent(6, true);
        }

        void Run(string argument)
        {
            if (argument.ToLower() == "safe")
            {
                SafeMode = !SafeMode;
            }
            else if (argument.ToLower() == "power")
            {
                _CurrentPowerLimit = Math.Round(_CurrentPowerLimit) == TargetPower ? MaxPower : TargetPower;
            }
            else if (argument.ToLower() == "friction")
            {
                _CurrentFriction = Math.Round(_CurrentFriction) == MinFriction ? MaxFriction : MinFriction;
            }
            else if (argument.ToLower() == "climb")
            {
                _CurrentPowerLimit = Math.Round(_CurrentPowerLimit) == TargetPower ? MaxPower : TargetPower;
                _CurrentFriction = Math.Round(_CurrentFriction) == MinFriction ? MaxFriction : MinFriction;
            }
            else if (argument.ToLower() == "up")
            {
                _CurrentHeight = -MaxHeight;
            }
            else if (argument.ToLower() == "down")
            {
                _CurrentHeight = MinHeight;
            }
            else if (argument.ToLower() == "updown")
            {
                _CurrentHeight = Math.Round(_CurrentHeight) == MinHeight ? -MaxHeight : MinHeight;
            }
            else if (argument.ToLower() == "brake")
            {
                if (_isCustomHandBrake)
                    _isBrakeEnabled = !_isBrakeEnabled;
                else
                    _isBrakeEnabled = false;
            }
            else if (argument.ToLower() == "cruise")
            {
                _CruiseMode = !_CruiseMode;
                _CruiseSpeed = (float)Math.Round(GetShipVelocityForward());
            }
        }

        void Update()
        {
            Counter = 0;
            
            ParseIni();

            _Lights.Clear();
            this.GridTerminalSystem.GetBlocksOfType<IMyLightingBlock>(
                _Lights,
                x => x.CustomName.ToLower().Contains(BrakeLightTag.ToLower())
                && x.IsSameConstructAs(this.Me)
            );

            _Suspensions.Clear();
            this.GridTerminalSystem.GetBlocksOfType<IMyMotorSuspension>(
                _Suspensions,
                x => !x.CustomName.ToLower().Contains("ignore")
                && x.IsSameConstructAs(this.Me)
            );

            if (AutomaticNewWheelsAdd) AddWheels();

            _Controller = GetShipController();
            if (!IsValid(_Controller)) return;

            _isCustomHandBrake = false;
            foreach (IMyMotorSuspension Suspension in _Suspensions)
            {
                if (Suspension.CubeGrid != _Controller.CubeGrid)
                {
                    _isCustomHandBrake = true;
                    break;
                }
                Suspension.IsParkingEnabled = true;
                Suspension.Brake = true;
            }
            if (_isCustomHandBrake)
            {
                foreach (IMyMotorSuspension Suspension in _Suspensions)
                {
                    Suspension.IsParkingEnabled = false;
                    Suspension.Brake = false;
                }
            }

            _SurfaceContentManager.Update();
        }

        void ParseIni()
        {
            IMyTerminalBlock Block = this.Me as IMyTerminalBlock;

            _ini.Clear();

            if (_ini.TryParse(Block.CustomData))
            {
                ControllerName = _ini.Get(IniSectionControllerGeneral, IniKeyControllerName).ToString(ControllerName);
                AutomaticNewWheelsAdd = _ini.Get(IniSectionControllerGeneral, IniKeyAutomaticNewWheelsAdd).ToBoolean(AutomaticNewWheelsAdd);
                SubgridControl = _ini.Get(IniSectionControllerGeneral, IniKeySubgridControl).ToBoolean(SubgridControl);
                CruiseAutoOff = _ini.Get(IniSectionControllerGeneral, IniKeyCruiseAutoOff).ToBoolean(CruiseAutoOff);
                SmoothDriving = _ini.Get(IniSectionControllerGeneral, IniKeySmoothDriving).ToBoolean(SmoothDriving);
                UseCustomAirShock = _ini.Get(IniSectionControllerGeneral, IniKeyUseCustomAirShock).ToBoolean(UseCustomAirShock);

                BrakeLightTag = _ini.Get(IniSectionControllerLight, IniKeyBrakeLightKeyword).ToString(BrakeLightTag);
                ReverseManagement = _ini.Get(IniSectionControllerGeneral, IniKeyReverseManagement).ToBoolean(ReverseManagement);
                HabariteManagement = _ini.Get(IniSectionControllerGeneral, IniKeyHabariteManagement).ToBoolean(HabariteManagement);

                TargetStrength = _ini.Get(IniSectionControllerStrength, IniKeyTargetStrength).ToSingle(TargetStrength);

                ForwardSpeedLimit = _ini.Get(IniSectionControllerSpeed, IniKeyForwardSpeedLimit).ToSingle(ForwardSpeedLimit);
                BackwardSpeedLimit = _ini.Get(IniSectionControllerSpeed, IniKeyBackwardSpeedLimit).ToSingle(BackwardSpeedLimit);

                SteerAngle = _ini.Get(IniSectionControllerSteer, IniKeySteerAngle).ToSingle(SteerAngle);

                MinPower = _ini.Get(IniSectionControllerPower, IniKeyMinPower).ToSingle(MinPower);
                TargetPower = _ini.Get(IniSectionControllerPower, IniKeyTargetPower).ToSingle(TargetPower);
                MaxPower = _ini.Get(IniSectionControllerPower, IniKeyMaxPower).ToSingle(MaxPower);

                MinFriction = _ini.Get(IniSectionControllerFriction, IniKeyMinFriction).ToSingle(MinFriction);
                MaxFriction = _ini.Get(IniSectionControllerFriction, IniKeyMaxFriction).ToSingle(MaxFriction);

                MinHeight = _ini.Get(IniSectionControllerHeight, IniKeyMinHeight).ToSingle(MinHeight);
                MaxHeight = _ini.Get(IniSectionControllerHeight, IniKeyMaxHeight).ToSingle(MaxHeight);
            }
            else if (!string.IsNullOrWhiteSpace(Block.CustomData))
            {
                _ini.EndContent = Block.CustomData;
            }

            _ini.Set(IniSectionControllerGeneral, IniKeyControllerName, ControllerName);
            _ini.Set(IniSectionControllerGeneral, IniKeyAutomaticNewWheelsAdd, AutomaticNewWheelsAdd);
            _ini.Set(IniSectionControllerGeneral, IniKeySubgridControl, SubgridControl);
            _ini.Set(IniSectionControllerGeneral, IniKeyCruiseAutoOff, CruiseAutoOff);
            _ini.Set(IniSectionControllerGeneral, IniKeySmoothDriving, SmoothDriving);
            _ini.Set(IniSectionControllerGeneral, IniKeyUseCustomAirShock, UseCustomAirShock);

            _ini.Set(IniSectionControllerLight, IniKeyBrakeLightKeyword, BrakeLightTag);
            _ini.Set(IniSectionControllerLight, IniKeyReverseManagement, ReverseManagement);
            _ini.Set(IniSectionControllerLight, IniKeyHabariteManagement, HabariteManagement);

            _ini.Set(IniSectionControllerStrength, IniKeyTargetStrength, TargetStrength);

            _ini.Set(IniSectionControllerSpeed, IniKeyForwardSpeedLimit, ForwardSpeedLimit);
            _ini.Set(IniSectionControllerSpeed, IniKeyBackwardSpeedLimit, BackwardSpeedLimit);

            _ini.Set(IniSectionControllerSteer, IniKeySteerAngle, SteerAngle);

            _ini.Set(IniSectionControllerPower, IniKeyMinPower, MinPower);
            _ini.Set(IniSectionControllerPower, IniKeyTargetPower, TargetPower);
            _ini.Set(IniSectionControllerPower, IniKeyMaxPower, MaxPower);

            _ini.Set(IniSectionControllerFriction, IniKeyMinFriction, MinFriction);
            _ini.Set(IniSectionControllerFriction, IniKeyMaxFriction, MaxFriction);

            _ini.Set(IniSectionControllerHeight, IniKeyMinHeight, MinHeight);
            _ini.Set(IniSectionControllerHeight, IniKeyMaxHeight, MaxHeight);

            string Output = _ini.ToString();
            if (Output != Block.CustomData)
            {
                Block.CustomData = Output;
            }
        }

        public string ControllerNameSafe => IsValid(_Controller) ? _Controller.CustomName : "Controller is not exist";
        public int SuspensionsCount => _Suspensions.Count;
        public int LightsCount => _Lights.Count;

        #region Helpers
        private bool IsValid(IMyTerminalBlock Block)
        {
            return (Block != null) && !Block.Closed;
        }

        private IMyShipController GetShipController()
        {
            List<IMyShipController> ControllerList = new List<IMyShipController>();
            this.GridTerminalSystem.GetBlocksOfType<IMyShipController>(ControllerList);

            foreach (IMyShipController Controller in ControllerList)
                if (Controller.ControlWheels && Controller.CanControlShip && Controller.CustomName.ToLower() == ControllerName.ToLower())
                    return Controller;

            foreach (IMyShipController Controller in ControllerList)
                if (Controller.ControlWheels && Controller.CanControlShip && Controller.IsMainCockpit)
                    return Controller;

            foreach (IMyShipController Controller in ControllerList)
                if (Controller.ControlWheels && Controller.CanControlShip && !(Controller is IMyRemoteControl))
                    return Controller;

            foreach (IMyShipController Controller in ControllerList)
                if (Controller.ControlWheels && Controller.CanControlShip)
                    return Controller;

            return null;
        }

        private static float GetShipVelocityForward()
        {
            Vector3D Velocity = _Controller.GetShipVelocities().LinearVelocity;
            return (float)Velocity.Dot(_Controller.WorldMatrix.Forward);
        }
        private static float GetShipVelocityDown()
        {
            Vector3D Velocity = _Controller.GetShipVelocities().LinearVelocity;
            return (float)Velocity.Dot(_Controller.WorldMatrix.Down);
        }
        private float GetTorque()
        {
            float Torque = _CurrentTorque * _CurrentPowerLimit / 100;
            Torque = Math.Min(Torque, _CurrentPowerLimit);
            return Math.Min(Math.Max(MinPower, Torque), Math.Max(TargetPower, _CurrentPowerLimit));
        }

        private Vector3D GetAverageWheelsPosition()
        {
            Vector3D Sum = Vector3D.Zero;
            int Count = 0;

            foreach (IMyMotorSuspension Suspension in _Suspensions)
            {
                if (Suspension.IsAttached)
                {
                    Sum += Suspension.GetPosition();
                    Count++;
                }
            }

            if (Count == 0) return Vector3D.Zero;
            return Sum / Count;
        }
        #endregion Helpers

        #region Changers
        private void Setup()
        {
            foreach (IMyMotorSuspension Suspension in _Suspensions)
            {
                Suspension.Friction = _CurrentFriction;
                Suspension.Height = _CurrentHeight;
            }
        }
        #endregion Changers

        #region Controllers
        private void ManageSafeMode()
        {
            if (!SafeMode || _Controller.IsUnderControl) return;

            _CruiseMode = false;
            _CruiseSpeed = 0;

            if (!_isCustomHandBrake)
                _Controller.HandBrake = true;
            else
                _isBrakeEnabled = true;
        }
        private void ManageCruise()
        {
            bool isBrake = _Controller.HandBrake
                            || (_isCustomHandBrake && _isBrakeEnabled);

            if (isBrake) _CruiseMode = false;
            if (!_CruiseMode)
            {
                _CruiseSpeed = 0;
                return;
            }

            _CruiseSpeed = (float)Math.Round(Math.Min(_CruiseSpeed - _Controller.MoveIndicator.Z, ForwardSpeedLimit / 3.6f));

            if (_CruiseSpeed < 6 && CruiseAutoOff)
            {
                _CruiseMode = false;
                _CruiseSpeed = 0;
                return;
            }

            _CruiseSpeed = Math.Max(_CruiseSpeed, 6);
        }
        private void ManageTorque()
        {
            if (!SmoothDriving)
            {
                _CurrentTorque = 100;
                _PropulsionMultiplier = 1;
                return;
            }

            float Velocity = Math.Abs(GetShipVelocityForward());

            bool isAccelerate = _Controller.MoveIndicator.Z != 0 || _CruiseMode;
            bool isForwardOffset = Velocity * 3.6f < ForwardSpeedLimit * 0.9f;
            bool isBackwardOffset = Velocity * 3.6f < BackwardSpeedLimit * 0.9f;

            _CurrentTorque = isAccelerate
                ? Math.Min(_CurrentTorque * (_Controller.MoveIndicator.Y > 0 || Velocity <= 6 ? 1.15f : 1.05f), 100)
                : Math.Max(_CurrentTorque * 0.95f, 5);

            _PropulsionMultiplier = isForwardOffset && _Controller.MoveIndicator.Z < 0
                ? Math.Min(_PropulsionMultiplier * 1.01f, 360 / ForwardSpeedLimit)
                : (isBackwardOffset && _Controller.MoveIndicator.Z > 0
                ? Math.Min(_PropulsionMultiplier * 1.01f, 360 / BackwardSpeedLimit)
                : Math.Max(_PropulsionMultiplier * 0.9f, 1)
                );
        }
        private void ManageLights()
        {
            bool isReverse = ReverseManagement
                            && GetShipVelocityForward() < -0.5f
                            && _Controller.MoveIndicator.Z > 0;

            bool isBrake = _Controller.HandBrake
                            || _Controller.MoveIndicator.Z > 0
                            || _Controller.MoveIndicator.Y > 0
                            || (_isCustomHandBrake && _isBrakeEnabled);

            foreach (IMyLightingBlock Light in _Lights)
            {
                Light.Enabled = true;

                Light.Intensity = isBrake || isReverse ? 2.5f : 0.5f;
                Light.Radius = isBrake || isReverse ? 2.5f : 0.5f;
                Light.Color = isReverse ? new Color(250, 230, 180) : new Color(255, 30, 30);
            }
        }
        private void ManageWheels()
        {
            Setup();
            SteerControl();
            StrengthControl();

            if (_CruiseMode)
                CruiseControl();
            else
                ManualControl();

            BrakeControl();

            foreach (IMyMotorSuspension Suspension in _Suspensions)
            {
                if (UseCustomAirShock)
                    Suspension.AirShockEnabled = false;
                else
                    Suspension.AirShockEnabled = true;
            }
        }

        private void AddWheels()
        {
            foreach (IMyMotorSuspension Suspension in _Suspensions)
            {
                for (int i = 0; i <= 10; ++i)
                {
                    if (!Suspension.IsAttached) Suspension.ApplyAction("Add Top Part");
                    else break;
                }
            }
        }
        private void ManualControl()
        {
            float Velocity = GetShipVelocityForward();
            float Torque = GetTorque();
            Vector3D AverageWheelsPosition = GetAverageWheelsPosition();

            foreach (IMyMotorSuspension Suspension in _Suspensions)
            {
                Vector3D Difference = Suspension.GetPosition() - AverageWheelsPosition;
                Vector3D Left = _Controller.WorldMatrix.Left;
                Vector3D Right = _Controller.WorldMatrix.Right;

                bool isLeft = Vector3D.Dot(Difference, Left) > 0;

                float Sign = Math.Sign(Vector3D.Dot(Suspension.WorldMatrix.Up, isLeft ? Left : Right));

                Suspension.SetValueFloat("Speed Limit", _Controller.MoveIndicator.Z > 0 && ReverseManagement ? BackwardSpeedLimit : ForwardSpeedLimit);

                Suspension.Power = Torque;

                float Propulsion = _Controller.MoveIndicator.Z;
                Propulsion *= Suspension.Power * _PropulsionMultiplier / 100;

                if (Velocity > ForwardSpeedLimit / 3.6f)
                    Propulsion = (Velocity - ForwardSpeedLimit / 3.6f) / _Suspensions.Count;
                else if (Math.Abs(Velocity) > BackwardSpeedLimit / 3.6f && Velocity < 0)
                    Propulsion = (Velocity + BackwardSpeedLimit / 3.6f) / _Suspensions.Count;

                if (_CurrentPowerLimit / 100 <= Math.Abs(Propulsion))
                    Propulsion = _CurrentPowerLimit / 100 * (Propulsion > 0 ? 1 : -1);

                Suspension.PropulsionOverride = Math.Max(Math.Min(Propulsion * Sign, 1), -1) * (isLeft ? -1 : 1);
            }

        }
        private void CruiseControl()
        {
            float Velocity = GetShipVelocityForward();
            float Torque = GetTorque();
            Vector3D AverageWheelsPosition = GetAverageWheelsPosition();

            foreach (IMyMotorSuspension Suspension in _Suspensions)
            {
                Vector3D Difference = Suspension.GetPosition() - AverageWheelsPosition;
                Vector3D Left = _Controller.WorldMatrix.Left;
                Vector3D Right = _Controller.WorldMatrix.Right;

                bool isLeft = Vector3D.Dot(Difference, Left) > 0;

                float Sign = Math.Sign(Vector3D.Dot(Suspension.WorldMatrix.Up, isLeft ? Left : Right));

                Suspension.SetValueFloat("Speed Limit", Convert.ToSingle(Math.Round(_CruiseSpeed * 3.6f)));

                Suspension.Power = Torque;

                float Propulsion = (Velocity - _CruiseSpeed) / (_Suspensions.Count / 2);
                Propulsion *= (Suspension.Power * _PropulsionMultiplier / 100);
                Suspension.PropulsionOverride = Math.Max(Math.Min(Propulsion * Sign, 1), -1) * (isLeft ? -1 : 1);
            }
        }
        private void SteerControl()
        {
            Vector3D AverageWheelsPosition = GetAverageWheelsPosition();

            foreach (IMyMotorSuspension Suspension in _Suspensions)
            {
                Vector3D Difference = Suspension.GetPosition() - AverageWheelsPosition;
                Vector3D Backward = _Controller.WorldMatrix.Backward;

                bool isBackward = Vector3D.Dot(Difference, Backward) > 0;

                float Dot = (float)Math.Abs(Vector3D.Dot(Vector3D.Normalize(Difference), Suspension.WorldMatrix.Up));

                Suspension.MaxSteerAngle = SteerAngle * (float)Math.PI / 180.0f * (1 - Dot);
                Suspension.SteeringOverride = _Controller.MoveIndicator.X * (isBackward ? -1 : 1);
            }
        }
        private void BrakeControl()
        {
            if (!_Controller.HandBrake && !(_isCustomHandBrake && _isBrakeEnabled) && !(_Controller.MoveIndicator.Y > 0)) return;

            Vector3D AverageWheelsPosition = GetAverageWheelsPosition();
            float Velocity = GetShipVelocityForward();

            foreach (IMyMotorSuspension Suspension in _Suspensions)
            {
                Vector3D Difference = Suspension.GetPosition() - AverageWheelsPosition;
                Vector3D Left = _Controller.WorldMatrix.Left;

                bool isLeft = Vector3D.Dot(Difference, Left) > 0;

                Suspension.SetValueFloat("Speed Limit", 0);

                float Propulsion = Velocity / 2.5f;

                Suspension.PropulsionOverride = Math.Max(Math.Min(Propulsion, 1), -1) * (isLeft ? -1 : 1);
            }
        }
        private void StrengthControl()
        {
            Vector3D AverageWheelsPosition = GetAverageWheelsPosition();
            float VelocityForward = GetShipVelocityForward();
            float VelocityDown = GetShipVelocityDown();

            foreach (IMyMotorSuspension Suspension in _Suspensions)
            {
                Vector3D Difference = Suspension.GetPosition() - AverageWheelsPosition;
                Vector3D Left = _Controller.WorldMatrix.Left;
                Vector3D Backward = _Controller.WorldMatrix.Backward;

                float Multiplier = (float)Math.Abs(Vector3D.Dot(Difference, Backward));
                bool isLeft = Vector3D.Dot(Difference, Left) > 0;
                float VelocityDifference = (VelocityForward > 0 ? ForwardSpeedLimit : BackwardSpeedLimit) / 3.6f - Math.Abs(VelocityForward);

                float StrengthDelta = TargetStrength / (_Suspensions.Count * 2);
                float Strength;

                if (Math.Pow(VelocityDown, 2) > Math.Abs(VelocityForward))
                    Strength = Math.Max(TargetStrength * (float)Math.Pow(VelocityDown * 0.1f, 2), TargetStrength);
                else
                    Strength = Math.Max(Math.Min(TargetStrength * Math.Abs(VelocityForward * 0.1f) * 0.5f, _CurrentStrength + StrengthDelta), TargetStrength);

                _CurrentStrength = Math.Min(Strength, 100);

                // tilt on steering
                float MinStrength = TargetStrength * 0.75f;
                if (_Controller.MoveIndicator.X > 0 && isLeft)
                    Strength -= Math.Abs(VelocityDifference) * _Controller.MoveIndicator.X;
                else if (_Controller.MoveIndicator.X < 0 && !isLeft)
                    Strength += Math.Abs(VelocityDifference) * _Controller.MoveIndicator.X;
                Strength = Math.Max(Math.Min(Strength, 100), MinStrength);

                Suspension.Strength = Math.Max(Strength * Multiplier / 4, Strength);
            }
        }
        #endregion Controllers

        #region ContentManager
        private readonly Dictionary<float, Color> Colors = new Dictionary<float, Color>() {
            { 0.0f, new Color(220, 30, 30) },
            { 0.1f, new Color(201, 49, 30) },
            { 0.2f, new Color(182, 68, 30) },
            { 0.3f, new Color(163, 87, 30) },
            { 0.4f, new Color(144, 106, 30) },
            { 0.5f, new Color(125, 125, 30) },
            { 0.6f, new Color(106, 114, 30) },
            { 0.7f, new Color(87, 163, 30) },
            { 0.8f, new Color(68, 182, 30) },
            { 0.9f, new Color(49, 201, 30) },
            { 1.0f, new Color(30, 220, 30) }
        };
        private void DrawDashboard(SurfaceContentManager.SurfaceManager Manager)
        {
            // Cruise
            Manager.AddBorderBuilder(new Vector2(0f, 0f), new Vector2(1f, 0.15f), new Vector2(Math.Min(1f - _CruiseSpeed / (ForwardSpeedLimit / 3.6f), 0.85f), 0));
            Manager.AddTextBuilder(
                "Cruise speed:",
                new Vector2(0f, 0f), new Vector2(1f, 0.15f),
                FontSize: 1.5f,
                Alignment: TextAlignment.LEFT, ExtraPadding: true
            );
            Manager.AddTextBuilder(
                _CruiseMode ? $"{_CruiseSpeed}m/s" : "Off",
                new Vector2(0f, 0f), new Vector2(1f, 0.15f),
                FontSize: 1.5f,
                Alignment: TextAlignment.RIGHT, ExtraPadding: true
            );

            // Speed
            float Velocity = Math.Abs(GetShipVelocityForward());
            Manager.AddCircleProgressBarBuilder(
                Velocity / (ForwardSpeedLimit / 3.6f), 0.15f,
                new Vector2(0.15f, 0.15f), new Vector2(0.85f, 0.85f),
                225, -45, Reverse: true
            );
            Manager.AddTextBuilder(
                String.Format("{0:0.00}", Velocity),
                new Vector2(0f, 0f), new Vector2(1f, 0.85f),
                FontSize: 2.75f
            );
            Manager.AddTextBuilder(
                "m/s",
                new Vector2(0f, 0.275f), new Vector2(1f, 0.375f),
                Alignment: TextAlignment.CENTER,
                FontSize: 1.2f
            );

            // Torque
            float Torque = _CurrentTorque * 0.01f;
            Manager.AddCircleProgressBarBuilder(
                Torque, 1f,
                new Vector2(0.35f, 0.55f), new Vector2(0.65f, 0.85f),
                360, 90, Reverse: true
            );
            Manager.AddTextBuilder(
                $"{Math.Round(Torque * 6000)}\nRPM",
                new Vector2(0.35f, 0.55f), new Vector2(0.65f, 0.85f),
                color: Colors[(float)Math.Round(1f - Torque, 1, MidpointRounding.AwayFromZero)]
            );

            // Power & Friction
            float Power = _CurrentPower * 0.01f;
            float Friction = _CurrentFriction * 0.01f;
            Manager.AddBorderBuilder(new Vector2(0f, 0.15f), new Vector2(0.15f, 0.5f));
            Manager.AddBorderBuilder(new Vector2(0f, 0.5f), new Vector2(0.15f, 0.85f));
            Manager.AddSquareProgressBarBuilder(Power, new Vector2(0f, 0.15f), new Vector2(0.15f, 0.5f));
            Manager.AddSquareProgressBarBuilder(Friction, new Vector2(0f, 0.5f), new Vector2(0.15f, 0.85f));
            Manager.AddTextBuilder(
                "Power",
                new Vector2(0f, 0.3f), new Vector2(0.15f, 0.5f),
                FontSize: 1.5f,
                color: Colors[(float)Math.Round(Power, 1, MidpointRounding.AwayFromZero)]
            );
            Manager.AddTextBuilder(
                "Friction",
                new Vector2(0f, 0.65f), new Vector2(0.15f, 0.85f),
                FontSize: 1.5f,
                color: Colors[(float)Math.Round(Friction, 1, MidpointRounding.AwayFromZero)]
            );

            // EMPTY RIGHT
            Manager.AddBorderBuilder(new Vector2(0.85f, 0.15f), new Vector2(1, 0.5f));
            Manager.AddBorderBuilder(new Vector2(0.85f, 0.5f), new Vector2(1, 0.85f));
            Manager.AddSquareProgressBarBuilder(0, new Vector2(0.85f, 0.15f), new Vector2(1, 0.5f));
            Manager.AddSquareProgressBarBuilder(0, new Vector2(0.85f, 0.5f), new Vector2(1, 0.85f));
            Manager.AddTextBuilder("Empty", new Vector2(0.85f, 0.3f), new Vector2(1, 0.5f), FontSize: 1.5f);
            Manager.AddTextBuilder("Empty", new Vector2(0.85f, 0.65f), new Vector2(1, 0.85f), FontSize: 1.5f);

            // EMPTY BOTTOM
            Manager.AddBorderBuilder(new Vector2(0f, 0.85f), new Vector2(1f, 1f));
            Manager.AddTextBuilder("Empty", new Vector2(0f, 0.85f), new Vector2(1f, 1f), FontSize: 1.5f);
        }
        private void DrawSpeed(SurfaceContentManager.SurfaceManager Manager)
        {
            // Speed
            float Velocity = Math.Abs(GetShipVelocityForward());
            Manager.AddCircleProgressBarBuilder(
                Velocity / (ForwardSpeedLimit / 3.6f), 0.15f,
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                225, -45, Reverse: true
            );
            Manager.AddTextBuilder(
                String.Format("{0:0.00}", Velocity),
                new Vector2(0f, 0f), new Vector2(1f, 0.85f),
                FontSize: 4.5f
            );
            Manager.AddTextBuilder(
                "m/s",
                new Vector2(0f, 0.225f), new Vector2(1f, 0.325f),
                Alignment: TextAlignment.CENTER, FontSize: 2f
            );

            // Torque
            float Torque = _CurrentTorque * 0.01f;
            Manager.AddCircleProgressBarBuilder(
                _CurrentTorque * 0.01f, 1f,
                new Vector2(0.25f, 0.65f), new Vector2(0.75f, 1f),
                360, 90, Reverse: true);
            Manager.AddTextBuilder(
                $"{Math.Round(Torque * 6000)}\nRPM",
                new Vector2(0.25f, 0.65f), new Vector2(0.75f, 1f),
                FontSize: 1.2f,
                color: Colors[(float)Math.Round(1f - Torque, 1, MidpointRounding.AwayFromZero)]
            );
        }
        #endregion ContentManager

#region PreludeFooter
    }
}
#endregion PreludeFooter