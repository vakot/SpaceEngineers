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

namespace SpaceEngineers.UWBlockPrograms.StatusManager
{
    public sealed class Program : MyGridProgram
    {
#endregion Prelude

#region Description
// ------------------------------------------------------------------------------------------------------- \\
// ========== Vakot Ind. Advanced Ship Status Manager Class =========== \\
// ------------------------------------------------------------------------------------------------------- \\

// ------------------ DESCRIPTION ------------------ \\

/* 
 * Simple script that provides posibility to collect information about ship status.
 * (power consumption, capacity, inventory fill level, etc.)
 * And display them to LCD panel's
 *
 * Minimal launch setup is:
 * - Programmable Block
 *
 * Using tutorial:
 * - Build a Programmable Block
 * - Place script into
 * - Edit settings in CustomData (optionally)
 * - Build LCD panel's
 * - Edit it's Name (add [LCD] tag)
 * - Wait till script update (10s)
 * - Edit LCD panel CustomData
 */

// ---------------- ARGUMENTS LIST ----------------- \\

/* 
 * Current version does not take any argument's
 *
 * CustomData argument's:
 * - ores (display minimalistic ores stats)
 * - ingots (display minimalistic ingots stats)
 * - inventory (display inventory items like in AutoLCD by MMaster)
 * -- i-[ores/ingots/components/tools/ammos/other] (same as inventory, but display only one type of items at the time)
 * - powergraph (display power consumption graph)
 * - inventories (display inventoryes list with status bar)
 * -- assemblers (display assemblers inventoryes list with status bar)
 * -- refineries (display refineries inventoryes list with status bar)
 * -- containers (display containers inventoryes list with status bar)
 * -- reactors (display reactors inventoryes list with status bar)
 * - none (set by default and don't display any content)
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

public const string Version = "1.5",
                    IniSectionGeneral = "General";

// -------------------------------------------------------------------------------------------------------- \\
// ========== !!! DONT CHANGE ANYTHING BELOW THIS LINE !!! =========== \\
// -------------------------------------------------------------------------------------------------------- \\
#endregion Description

        #region Variables
        private static SurfaceContentManager _SurfaceContentManager;
        private static List<IMyTerminalBlock> _PowerProducers = new List<IMyTerminalBlock>();
        private static List<IMyTerminalBlock> _Inventories = new List<IMyTerminalBlock>();

        private static List<float> _PowerConsumptionStory = new List<float>();

        private ItemsQuotas _Quotas = new ItemsQuotas();

        int Counter = 0;
        #endregion Variables

        void Report()
        {
            string Status = $"Ship Status {RunStatus[Runtime.UpdateFrequency == UpdateFrequency.Update10 ? Counter % RunStatus.Count : Counter / 10 % RunStatus.Count]}";
            Status += $"\nNext update in: {10 - Counter / 6}" + "s\n";

            Status += $"\n------ Block's Info ------\n";
            Status += $"Power producer's: {PowerProducersCount}\n";
            Status += $"Inventories: {InventoriesCount}\n";
            Status += $"Surfaces: {_SurfaceContentManager.SurfacesCount()}\n";

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

            _SurfaceContentManager = new SurfaceContentManager(this);

            SetupSurfaces();
            SetupQuotas();
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
            _SurfaceContentManager.DrawContent(6, true);
        }

        void Run(string argument) { }

        void Update()
        {
            Counter = 0;

            _PowerProducers.Clear();
            _Inventories.Clear();

            List<IMyTerminalBlock> Blocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType(Blocks, x => x.IsSameConstructAs(Me));

            _PowerProducers = Blocks.Where(x => x is IMyPowerProducer).ToList();
            _Inventories = Blocks.Where(x => x.HasInventory).ToList();
        }

        void SetupSurfaces()
        {
            _SurfaceContentManager.AddContentType("debug", Debug);

            _SurfaceContentManager.AddContentType("powergraph", DrawPowerGraph);

            _SurfaceContentManager.AddContentType("ores", DrawOresStats);
            _SurfaceContentManager.AddContentType("ingots", DrawIngotsStats);

            _SurfaceContentManager.AddContentType("assemblers", DrawAssemblersList);
            _SurfaceContentManager.AddContentType("refineries", DrawRefineriesList);
            _SurfaceContentManager.AddContentType("containers", DrawCargoContainersList);
            _SurfaceContentManager.AddContentType("reactors", DrawReactorList);
            _SurfaceContentManager.AddContentType("inventories", DrawInventoriesList);

            _SurfaceContentManager.AddContentType("inventory", DrawInventoryList);
            _SurfaceContentManager.AddContentType("i-ores", DrawOresInventoryList);
            _SurfaceContentManager.AddContentType("i-ingots", DrawIngotsInventoryList);
            _SurfaceContentManager.AddContentType("i-components", DrawComponentsInventoryList);
            _SurfaceContentManager.AddContentType("i-tools", DrawToolsInventoryList);
            _SurfaceContentManager.AddContentType("i-ammos", DrawAmmosInventoryList);
            _SurfaceContentManager.AddContentType("i-other", DrawOtherInventoryList);
        }

        #region Quotas
        public void SetupQuotas()
        {
            // ORES
            _Quotas.Add(new ItemsQuotas.ItemQuota("Iron", "Ore", "Iron Ore", "Fe", 100000));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Nickel", "Ore", "Nickel Ore", "Ni", 50000));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Cobalt", "Ore", "Cobalt Ore", "Co", 25000));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Magnesium", "Ore", "Magnesium Ore", "Mg", 25000));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Silicon", "Ore", "Silicon Ore", "Si", 50000));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Silver", "Ore", "Silver Ore", "Ag", 15000));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Gold", "Ore", "Gold Ore", "Au", 15000));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Platinum", "Ore", "Platinum Ore", "Pt", 7500));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Uranium", "Ore", "Uranium Ore", "Ur", 7500));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Ice", "Ore", "", "", 100000));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Stone", "Ore", "", "", -25000));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Scrap", "Ore", "", "", -25000));
            // INGOTS
            _Quotas.Add(new ItemsQuotas.ItemQuota("Iron", "Ingot", "Iron Ingot", "Fe", 100000));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Nickel", "Ingot", "Nickel Ingot", "Ni", 50000));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Cobalt", "Ingot", "Cobalt Ingot", "Co", 25000));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Magnesium", "Ingot", "Magnesium Ingot", "Mg", 25000));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Silicon", "Ingot", "Silicon Ingot", "Si", 50000));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Silver", "Ingot", "Silver Ingot", "Ag", 15000));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Gold", "Ingot", "Gold Ingot", "Au", 15000));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Platinum", "Ingot", "Platinum Ingot", "Pt", 7500));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Uranium", "Ingot", "Uranium Ingot", "Ur", 7500));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Stone", "Ingot", "Gravel", "", 50000));
            // COMPONENTS
            _Quotas.Add(new ItemsQuotas.ItemQuota("Construction", "Component", "", "", 50000));
            _Quotas.Add(new ItemsQuotas.ItemQuota("MetalGrid", "Component", "Metal Grid", "", 15500));
            _Quotas.Add(new ItemsQuotas.ItemQuota("InteriorPlate", "Component", "Interior Plate", "", 55000));
            _Quotas.Add(new ItemsQuotas.ItemQuota("SteelPlate", "Component", "Steel Plate", "", 300000));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Girder", "Component", "Steel Plate", "", 8500));
            _Quotas.Add(new ItemsQuotas.ItemQuota("SmallTube", "Component", "Small Tube", "", 26000));
            _Quotas.Add(new ItemsQuotas.ItemQuota("LargeTube", "Component", "Large Tube", "", 6000));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Motor", "Component", "", "", 16000));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Display", "Component", "", "", 500));
            _Quotas.Add(new ItemsQuotas.ItemQuota("BulletproofGlass", "Component", "Bull. Glass", "", 12000));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Computer", "Component", "", "", 6500));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Reactor", "Component", "Reactor Comp.", "", 2800));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Thrust", "Component", "Thruster Comp.", "", 5600));
            _Quotas.Add(new ItemsQuotas.ItemQuota("GravityGenerator", "Component", "Gravity Comp.", "", 250));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Medical", "Component", "Medical Comp.", "", 120));
            _Quotas.Add(new ItemsQuotas.ItemQuota("RadioCommunication", "Component", "Radio Comp.", "", 250));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Detector", "Component", "Detector Comp.", "", 400));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Explosives", "Component", "", "", 500));
            _Quotas.Add(new ItemsQuotas.ItemQuota("SolarCell", "Component", "Solar Cell", "", 3200));
            _Quotas.Add(new ItemsQuotas.ItemQuota("PowerCell", "Component", "Power Cell", "", 3200));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Superconductor", "Component", "", "", 3000));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Canvas", "Component", "", "", 300));
            _Quotas.Add(new ItemsQuotas.ItemQuota("ZoneChip", "Component", "Zone Chip", "", 100));
            // WEAPONS
            _Quotas.Add(new ItemsQuotas.ItemQuota("SemiAutoPistolItem", "PhysicalGunObject", "S-10 Pistol", "S-10"));
            _Quotas.Add(new ItemsQuotas.ItemQuota("ElitePistolItem", "PhysicalGunObject", "S-10E Pistol", "S-10E"));
            _Quotas.Add(new ItemsQuotas.ItemQuota("FullAutoPistolItem", "PhysicalGunObject", "S-20A Pistol", "S-20A"));
            _Quotas.Add(new ItemsQuotas.ItemQuota("AutomaticRifleItem", "PhysicalGunObject", "MR-20 Rifle", "MR-20"));
            _Quotas.Add(new ItemsQuotas.ItemQuota("PreciseAutomaticRifleItem", "PhysicalGunObject", "MR-8P Rifle", "MR-8P"));
            _Quotas.Add(new ItemsQuotas.ItemQuota("RapidFireAutomaticRifleItem", "PhysicalGunObject", "MR-50A Rifle", "MR-50A"));
            _Quotas.Add(new ItemsQuotas.ItemQuota("UltimateAutomaticRifleItem", "PhysicalGunObject", "MR-30E Rifle", "MR-30E"));
            _Quotas.Add(new ItemsQuotas.ItemQuota("BasicHandHeldLauncherItem", "PhysicalGunObject", "RO-1 Launcher", "RO-1"));
            _Quotas.Add(new ItemsQuotas.ItemQuota("AdvancedHandHeldLauncherItem", "PhysicalGunObject", "PRO-1 Launcher", "PRO-1"));
            // AMMOS
            _Quotas.Add(new ItemsQuotas.ItemQuota("NATO_5p56x45mm", "AmmoMagazine", "5.56x45mm", "", 8000));
            _Quotas.Add(new ItemsQuotas.ItemQuota("SemiAutoPistolMagazine", "AmmoMagazine", "S-10 Mag.", "", 500));
            _Quotas.Add(new ItemsQuotas.ItemQuota("ElitePistolMagazine", "AmmoMagazine", "S-10E Mag.", "", 500));
            _Quotas.Add(new ItemsQuotas.ItemQuota("FullAutoPistolMagazine", "AmmoMagazine", "S-20A Mag.", "", 500));
            _Quotas.Add(new ItemsQuotas.ItemQuota("AutomaticRifleGun_Mag_20rd", "AmmoMagazine", "MR-20 Mag.", "", 1000));
            _Quotas.Add(new ItemsQuotas.ItemQuota("PreciseAutomaticRifleGun_Mag_5rd", "AmmoMagazine", "MR-8P Mag.", "", 1000));
            _Quotas.Add(new ItemsQuotas.ItemQuota("RapidFireAutomaticRifleGun_Mag_50rd", "AmmoMagazine", "MR-50A Mag.", "", 8000));
            _Quotas.Add(new ItemsQuotas.ItemQuota("UltimateAutomaticRifleGun_Mag_30rd", "AmmoMagazine", "MR-30E Mag.", "", 1000));
            _Quotas.Add(new ItemsQuotas.ItemQuota("NATO_25x184mm", "AmmoMagazine", "25x184mm", "", 2500));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Missile200mm", "AmmoMagazine", "200mm Missile", "", 1600));
            // TOOLS
            _Quotas.Add(new ItemsQuotas.ItemQuota("WelderItem", "PhysicalGunObject", "Welder"));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Welder2Item", "PhysicalGunObject", "* Enh. Welder"));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Welder3Item", "PhysicalGunObject", "** Prof. Welder"));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Welder4Item", "PhysicalGunObject", "*** Elite Welder"));
            _Quotas.Add(new ItemsQuotas.ItemQuota("AngleGrinderItem", "PhysicalGunObject", "Angle Grinder"));
            _Quotas.Add(new ItemsQuotas.ItemQuota("AngleGrinder2Item", "PhysicalGunObject", "* Enh. Grinder"));
            _Quotas.Add(new ItemsQuotas.ItemQuota("AngleGrinder3Item", "PhysicalGunObject", "** Prof. Grinder"));
            _Quotas.Add(new ItemsQuotas.ItemQuota("AngleGrinder4Item", "PhysicalGunObject", "*** Elite Grinder"));
            _Quotas.Add(new ItemsQuotas.ItemQuota("HandDrillItem", "PhysicalGunObject", "Hand Drill"));
            _Quotas.Add(new ItemsQuotas.ItemQuota("HandDrill2Item", "PhysicalGunObject", "* Enh. Drill"));
            _Quotas.Add(new ItemsQuotas.ItemQuota("HandDrill3Item", "PhysicalGunObject", "** Prof. Drill"));
            _Quotas.Add(new ItemsQuotas.ItemQuota("HandDrill4Item", "PhysicalGunObject", "*** Elite Drill"));
            // OTHER (ConsumableItem, OxygenContainerObject, GasContainerObject, PhysicalObject, Datapad)
            _Quotas.Add(new ItemsQuotas.ItemQuota("Datapad", "Datapad"));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Medkit", "ConsumableItem"));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Package", "Package"));
            _Quotas.Add(new ItemsQuotas.ItemQuota("Powerkit", "ConsumableItem"));
            _Quotas.Add(new ItemsQuotas.ItemQuota("ClangCola", "ConsumableItem", "Clang Cola"));
            _Quotas.Add(new ItemsQuotas.ItemQuota("CosmicCoffee", "ConsumableItem", "Cosmic Coffee"));
            _Quotas.Add(new ItemsQuotas.ItemQuota("SpaceCredit", "PhysicalObject", "Space Credit"));
            _Quotas.Add(new ItemsQuotas.ItemQuota("OxygenBottle", "OxygenContainerObject", "Oxygen Bottle", "", 5));
            _Quotas.Add(new ItemsQuotas.ItemQuota("HydrogenBottle", "GasContainerObject", "Hydrogen Bottle", "", 20));
        }

        private class ItemsQuotas
        {
            private List<ItemQuota> _Quotas = new List<ItemQuota>();

            public void Add(string name, string type, string customName = "", string shortName = "", float quota = 0)
            {
                _Quotas.Add(new ItemQuota(name, type, customName, shortName, quota));
            }
            public void Add(ItemQuota itemQuota)
            {
                _Quotas.Add(itemQuota);
            }

            public ItemQuota GetByName(string name, string type) {
                return _Quotas.Find(
                    x => x.Name == name
                    && (x.Type == type || $"MyObjectBuilder_{x.Type}" == type)
                );
            }
            public List<ItemQuota> GetByType(string type) => _Quotas.Where(x => x.Type == type).ToList();

            public class ItemQuota
            {
                public string Name { get; private set; }
                public string Type { get; private set; }
                public string CustomName { get; private set; }
                public string ShortName { get; private set; }
                public float Quota { get; private set; }

                public ItemQuota(string name, string type, string customName = "", string shortName = "", float quota = 0)
                {
                    Name = name;
                    Type = type;
                    CustomName = customName != "" ? customName : name;
                    ShortName = shortName;
                    Quota = quota;
                }
            }
        }
        #endregion Quotas

        #region Power
        public int PowerProducersCount => _PowerProducers.Count;
        public int ReactorsCount => _PowerProducers.Where(x => x is IMyReactor).ToList().Count;
        public int BatteriesCount => _PowerProducers.Where(x => x is IMyBatteryBlock).ToList().Count;
        public int SolarPanelsCount => _PowerProducers.Where(x => x is IMySolarPanel).ToList().Count;

        public static float CurrentPowerOutput
        {
            get {
                try
                {
                    float Sum = 0;
                    foreach (IMyPowerProducer Producer in _PowerProducers)
                    {
                        if (Producer.IsWorking)
                        {
                            Sum += Producer.CurrentOutput;
                        }
                    }
                    return Sum;
                }
                catch { return 0; }
            }
            private set {}
        }
        public static float MaxPowerOutput
        {
            get {
                try
                {
                    float Sum = 0;
                    foreach (IMyPowerProducer Producer in _PowerProducers)
                    {
                        if (Producer.IsWorking)
                        {
                            Sum += Producer.MaxOutput;
                        }
                    }
                    return Sum;
                }
                catch { return 0; }
            }
            private set {}
        }
        public static float CurrentStoredPower
        {
            get {
                try
                {
                    float Sum = 0;
                    foreach (IMyBatteryBlock Battery in _PowerProducers.Where(x => x is IMyBatteryBlock).ToList())
                    {
                        if (Battery.IsWorking)
                        {
                            Sum += Battery.CurrentStoredPower;
                        }
                    }
                    return Sum;
                }
                catch { return 0; }
            }
            private set {}
        }
        public static float MaxStoredPower
        {
            get {
                try
                {
                    float Sum = 0;
                    foreach (IMyBatteryBlock Battery in _PowerProducers.Where(x => x is IMyBatteryBlock).ToList())
                    {
                        if (Battery.IsWorking)
                        {
                            Sum += Battery.MaxStoredPower;
                        }
                    }
                    return Sum;
                }
                catch { return 0; }
            }
            private set {}
        }
        public static float AvgStoredPower
        {
            get {
                try { return CurrentStoredPower / _PowerProducers.Where(x => x is IMyBatteryBlock).ToList().Count; }
                catch { return 0; }
            }
            private set {}
        }
        public static float AvgPowerOutput
        {
            get {
                try { return CurrentPowerOutput / _PowerProducers.Count; }
                catch { return 0; }
            }
            private set {}
        }
        #endregion Power

        #region Inventory
        public int InventoriesCount => _Inventories.Count;

        public float CargoCurrentVolume
        {
            get {
                try
                {
                    float Sum = 0;
                    foreach (IMyCargoContainer Inventory in _Inventories)
                    {
                        for (int i = 0; i < Inventory.InventoryCount; i++)
                        {
                            Sum += (float)Inventory.GetInventory(i).CurrentVolume;
                        }
                    }
                    return Sum;
                }
                catch { return 0; }
            }
            private set {}
        }
        public float CargoMaxVolume
        {   
            get {
                try
                {
                    float Sum = 0;
                    foreach (IMyCargoContainer Inventory in _Inventories)
                    {
                        for (int i = 0; i < Inventory.InventoryCount; i++)
                        {
                            Sum += (float)Inventory.GetInventory(i).MaxVolume;
                        }
                    }
                    return Sum;
                }
                catch { return 0; }
            }
            private set {}
        }
        public float AvgCargoCurrentVolume
        {
            get {
                try { return CargoCurrentVolume / _Inventories.Count; }
                catch { return 0; }
            }
            private set {}
        }

        private float GetCargoMaxVolume(IMyInventoryOwner Inventory)
        {
            try
            {
                float Sum = 0;
                for (int i = 0; i < Inventory.InventoryCount; i++)
                {
                    Sum += (float)Inventory.GetInventory(i).MaxVolume;
                }
                return Sum;
            }
            catch { return 0; }
        }
        private float GetCargoCurrentVolume(IMyInventoryOwner Inventory)
        {
            try
            {
                float Sum = 0;
                for (int i = 0; i < Inventory.InventoryCount; i++)
                {
                    Sum += (float)Inventory.GetInventory(i).CurrentVolume;
                }
                return Sum;
            }
            catch { return 0; }
        }
        private float GetCargoFillLevel(IMyInventoryOwner Inventory)
        {
            float Max = GetCargoMaxVolume(Inventory);
            if (Max != 0) return GetCargoCurrentVolume(Inventory) / Max;
            else return 0;
        }

        private float GetInventoryMaxVolume(IMyInventory Inventory)
        {
            try
            {
                return (float)Inventory.MaxVolume;
            }
            catch { return 0; }
        }
        private float GetInventoryCurrentVolume(IMyInventory Inventory)
        {
            try
            {
                return (float)Inventory.CurrentVolume;
            }
            catch { return 0; }
        }
        private float GetInventoryFillLevel(IMyInventory Inventory)
        {
            float Max = GetInventoryMaxVolume(Inventory);
            if (Max > 0) {
                float Current = GetInventoryCurrentVolume(Inventory);
                return Current / Max;
            }
            else return 0;
        }

        private Dictionary<MyItemType, float> GetInventoryItems(string[] TypeId = null, string SubtypeId = null)
        {
            Dictionary<MyItemType, float> Items = new Dictionary<MyItemType, float>();

            foreach (IMyTerminalBlock Block in _Inventories)
            {
                for (int i = 0; i < Block.InventoryCount; i++)
                {
                    try
                    {
                        List<MyInventoryItem> ItemsList = new List<MyInventoryItem>();

                        Block.GetInventory(i).GetItems(ItemsList);

                        foreach (MyInventoryItem Item in ItemsList)
                        {
                            if (TypeId != null && !TypeId.Contains(Item.Type.TypeId.ToString())) continue;
                            if (SubtypeId != null && Item.Type.SubtypeId.ToString() != SubtypeId) continue;

                            if (Item.Amount <= 0) continue;
                            
                            // if exist - change amount
                            if (Items.ContainsKey(Item.Type))
                            {
                                Items[Item.Type] += (float)Item.Amount;
                            }
                            // if not exist - add and set amount
                            else
                            {
                                Items.Add(Item.Type, (float)Item.Amount);
                            }
                            
                        }
                    }
                    catch { }
                }
            }

            return Items;
        }
        #endregion Inventory

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

        private void Debug(SurfaceContentManager.SurfaceManager Manager)
        {
            Manager.AddTextBuilder("--- Debug ---", new Vector2(0f, 0f), new Vector2(1f, 0.15f), FontSize: 1.25f);
            Manager.SaveLine();
        }

        private void DrawPowerGraph(SurfaceContentManager.SurfaceManager Manager)
        {
            _PowerConsumptionStory.Add(CurrentPowerOutput / MaxPowerOutput);
            if (_PowerConsumptionStory.Count > 30) _PowerConsumptionStory.RemoveAt(0);

            Manager.AddTextBuilder("--- Power Consumption ---", new Vector2(0f, 0f), new Vector2(1f, 0.15f), FontSize: 1.25f);
            Manager.AddBorderBuilder(new Vector2(0f, 0.15f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f));
            Manager.AddGraphBuilder(_PowerConsumptionStory, new Vector2(0f, 0.2f), new Vector2(1f, 1f));
        }

        private void DrawOresStats(SurfaceContentManager.SurfaceManager Manager)
        {
            DrawStash(Manager, new string[] { "MyObjectBuilder_Ore" }, "Ore", "Ores");
        }
        private void DrawIngotsStats(SurfaceContentManager.SurfaceManager Manager)
        {
            DrawStash(Manager, new string[] { "MyObjectBuilder_Ingot" }, "Ingot", "Ingot's");
        }
        private void DrawStash(SurfaceContentManager.SurfaceManager Manager, string[] TypeId, string Type, string Title)
        {
            Dictionary<MyItemType, float> Items = GetInventoryItems(TypeId: TypeId);
            List<ItemsQuotas.ItemQuota> Quotas = _Quotas.GetByType(Type);

            Manager.AddTextBuilder($"--- {Title} Stash ---", new Vector2(0f, 0f), new Vector2(1f, 0.15f), FontSize: 1.25f);
            Manager.SaveLine();

            Manager.AddBorderBuilder(new Vector2(0f, 0f), new Vector2(1, (float)Math.Ceiling(Quotas.Count / 3f) * 0.2f + 0.05f));

            float Vertical = 0.025f;
            float Horizontal = 0f;

            foreach (ItemsQuotas.ItemQuota Quota in Quotas)
            {
                bool isReverse = Quota.Quota < 0;
                string Lable = (isReverse ? "!" : "") + (Quota.ShortName != "" ? Quota.ShortName : Quota.Name);

                foreach (string typeId in TypeId)
                {
                    MyItemType ItemType = new MyItemType(typeId, Quota.Name);

                    float Amount = Items.ContainsKey(ItemType) ? Items[ItemType] : 0;
                    float TargetAmount = Math.Abs(Quota.Quota);

                    float Percentage = Amount / TargetAmount;
                    Percentage = isReverse ? 1f - Percentage : Percentage;
                    Percentage = (float)Math.Round(Percentage, 1, MidpointRounding.AwayFromZero);
                    Percentage = Math.Max(Math.Min(Percentage, 1), 0);

                    Manager.AddTextBuilder(Lable, new Vector2(Horizontal, Vertical), new Vector2(Horizontal + 1f / 3f, Vertical + 0.2f), FontSize: 2f, color: Colors[Percentage]);

                    Horizontal += 1f / 3f;
                    if (Horizontal >= 1f)
                    {
                        Horizontal = 0f;
                        Vertical += 0.2f;
                    }
                }
            }
            Manager.SaveLine();
        }

        private void DrawAssemblersList(SurfaceContentManager.SurfaceManager Manager)
        {
            DrawCargoList(Manager, _Inventories.Where(x => x is IMyAssembler).ToList(), "Assembler's List");
        }
        private void DrawRefineriesList(SurfaceContentManager.SurfaceManager Manager)
        {
            DrawCargoList(Manager, _Inventories.Where(x => x is IMyRefinery).ToList(), "Refineries List");
        }
        private void DrawCargoContainersList(SurfaceContentManager.SurfaceManager Manager)
        {
            DrawCargoList(Manager, _Inventories.Where(x => x is IMyCargoContainer).ToList(), "Container's List");
        }
        private void DrawReactorList(SurfaceContentManager.SurfaceManager Manager)
        {
            DrawCargoList(Manager, _Inventories.Where(x => x is IMyReactor).ToList(), "Reactor's List");
        }
        private void DrawInventoriesList(SurfaceContentManager.SurfaceManager Manager)
        {
            DrawCargoList(Manager, _Inventories, "All Inventories List");
        }
        private void DrawCargoList(SurfaceContentManager.SurfaceManager Manager, List<IMyTerminalBlock> Inventories, string Title)
        {
            Manager.AddTextBuilder($"--- {Title} ---", new Vector2(0f, 0f), new Vector2(1f, 0.15f), FontSize: 1.25f);
            Manager.SaveLine();

            int Index = 0;

            foreach (IMyTerminalBlock Inventory in Inventories)
            {
                for (int i = 0; i < Inventory.InventoryCount; i++)
                {
                    float FillLevel = GetInventoryFillLevel(Inventory.GetInventory(i));

                    Manager.AddBorderBuilder(new Vector2(0.1f, 0f), new Vector2(0.8f, 0.1f));
                    
                    Manager.AddTextBuilder($"{++Index} -", new Vector2(-0.1f, 0f), new Vector2(0.1f, 0.1f), Alignment: TextAlignment.RIGHT);
                    Manager.AddSquareProgressBarBuilder(FillLevel, new Vector2(0.1f, 0f), new Vector2(0.8f, 0.1f), 270);
                    Manager.AddTextBuilder(String.Format("{0:0.0}%", FillLevel * 100f), new Vector2(0.75f, 0f), new Vector2(1f, 0.1f), Alignment: TextAlignment.RIGHT);
                    Manager.AddTextBuilder($"[{i}] - {Inventory.CustomName}", new Vector2(0.1f, 0f), new Vector2(0.8f, 0.095f), Alignment: TextAlignment.LEFT, ExtraPadding: true, color: Manager.BackgroundColor, FontSize: 0.7f);

                    Manager.SaveLine();
                }
            }
        }

        private void DrawOresInventoryList(SurfaceContentManager.SurfaceManager Manager)
        {
            Dictionary<string, Dictionary<MyItemType, float>> Groups = new Dictionary<string, Dictionary<MyItemType, float>>() {
                { "Ores", GetInventoryItems(TypeId: new string[] { "MyObjectBuilder_Ore" }) }
            };
            DrawInventoryItemsList(Manager, Groups);
        }
        private void DrawIngotsInventoryList(SurfaceContentManager.SurfaceManager Manager)
        {
            Dictionary<string, Dictionary<MyItemType, float>> Groups = new Dictionary<string, Dictionary<MyItemType, float>>() {
                { "Ingot's", GetInventoryItems(TypeId: new string[] { "MyObjectBuilder_Ingot" }) }
            };
            DrawInventoryItemsList(Manager, Groups);
        }
        private void DrawComponentsInventoryList(SurfaceContentManager.SurfaceManager Manager)
        {
            Dictionary<string, Dictionary<MyItemType, float>> Groups = new Dictionary<string, Dictionary<MyItemType, float>>() {
                { "Component's", GetInventoryItems(TypeId: new string[] { "MyObjectBuilder_Component" }) }
            };
            DrawInventoryItemsList(Manager, Groups);
        }
        private void DrawToolsInventoryList(SurfaceContentManager.SurfaceManager Manager)
        {
            Dictionary<string, Dictionary<MyItemType, float>> Groups = new Dictionary<string, Dictionary<MyItemType, float>>() {
                { "Tools", GetInventoryItems(TypeId: new string[] { "MyObjectBuilder_PhysicalGunObject" }) }
            };
            DrawInventoryItemsList(Manager, Groups);
        }
        private void DrawAmmosInventoryList(SurfaceContentManager.SurfaceManager Manager)
        {
            Dictionary<string, Dictionary<MyItemType, float>> Groups = new Dictionary<string, Dictionary<MyItemType, float>>() {
                { "Ammos", GetInventoryItems(TypeId: new string[] { "MyObjectBuilder_AmmoMagazine" }) }
            };
            DrawInventoryItemsList(Manager, Groups);
        }
        private void DrawOtherInventoryList(SurfaceContentManager.SurfaceManager Manager)
        {
            Dictionary<string, Dictionary<MyItemType, float>> Groups = new Dictionary<string, Dictionary<MyItemType, float>>() {
                { "Other", GetInventoryItems(TypeId: new string[] {
                    "MyObjectBuilder_Datapad",
                    "MyObjectBuilder_ConsumableItem",
                    "MyObjectBuilder_PhysicalObject",
                    "MyObjectBuilder_OxygenContainerObject",
                    "MyObjectBuilder_GasContainerObject"
                })}
            };
            DrawInventoryItemsList(Manager, Groups);
        }
        private void DrawInventoryList(SurfaceContentManager.SurfaceManager Manager)
        {
            DrawInventoryItemsList(Manager);
        }
        private void DrawInventoryItemsList(SurfaceContentManager.SurfaceManager Manager, Dictionary<string, Dictionary<MyItemType, float>> Groups = null)
        {
            Dictionary<MyItemType, float> Items = GetInventoryItems();

            if (Groups == null) Groups = new Dictionary<string, Dictionary<MyItemType, float>>() {
                { "Ores", GetInventoryItems(TypeId: new string[] { "MyObjectBuilder_Ore" }) },
                { "Ingot's", GetInventoryItems(TypeId: new string[] { "MyObjectBuilder_Ingot" }) },
                { "Component's", GetInventoryItems(TypeId: new string[] { "MyObjectBuilder_Component" }) },
                { "Ammos", GetInventoryItems(TypeId: new string[] { "MyObjectBuilder_AmmoMagazine" }) },
                { "Tools", GetInventoryItems(TypeId: new string[] { "MyObjectBuilder_PhysicalGunObject" }) },
                { "Other", GetInventoryItems(TypeId: new string[] {
                    "MyObjectBuilder_Datapad",
                    "MyObjectBuilder_ConsumableItem",
                    "MyObjectBuilder_PhysicalObject",
                    "MyObjectBuilder_OxygenContainerObject",
                    "MyObjectBuilder_GasContainerObject"
                })}
            };

            foreach (string Key in Groups.Keys)
            {
                Manager.AddTextBuilder($"<<{Key} summary>>", new Vector2(0f, 0f), new Vector2(1f, 0.1f));
                Manager.SaveLine();

                foreach (MyItemType ItemType in Groups[Key].Keys)
                {
                    ItemsQuotas.ItemQuota Quota = _Quotas.GetByName(ItemType.SubtypeId, ItemType.TypeId);

                    float Amount = Groups[Key][ItemType];
                    float TargetAmount = Quota != null ? Quota.Quota : 0;
                    string Lable = Quota != null ? Quota.CustomName : ItemType.SubtypeId.ToString();
                    string[] Suffix = { "", "" };
                    if (Amount >= 1000f)
                    {
                        Amount *= 0.001f;
                        Suffix[0] = "k";
                    }
                    if (TargetAmount >= 1000f)
                    {
                        TargetAmount *= 0.001f;
                        Suffix[1] = "k";
                    }
                    if (TargetAmount < 0) TargetAmount = 0;
                    
                    string Value = "";
                    if (ItemType.TypeId == "MyObjectBuilder_Component") Value = "";
                    else if (ItemType.TypeId == "MyObjectBuilder_Ingot") Value = " (kg)";
                    else if (ItemType.TypeId == "MyObjectBuilder_Ore") Value = " (kg)";

                    string Total = TargetAmount > 0
                    ? $"{Math.Round(Amount, 1)}{Suffix[0]} / {Math.Round(TargetAmount, 1)}{Suffix[1]}{Value}"
                    : $"{Math.Round(Amount, 1)}{Suffix[0]}{Value}";

                    Manager.AddTextBuilder(Lable, new Vector2(0f, 0f), new Vector2(0.75f, 0.05f), FontSize: 0.8f, Alignment: TextAlignment.LEFT);
                    Manager.AddTextBuilder(Total, new Vector2(0.25f, 0f), new Vector2(1f, 0.05f), FontSize: 0.8f, Alignment: TextAlignment.RIGHT);
                    Manager.SaveLine();
                }
            }
        }
        #endregion ContentManager

#region PreludeFooter
    }
}
#endregion PreludeFooter