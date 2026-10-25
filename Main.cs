using System.Reflection;
using HarmonyLib;
using UnityModManagerNet;

namespace AutomateFleet
{
    public static class Main
    {
        public static bool enabled;
        public static UnityModManager.ModEntry mod;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            mod = modEntry;
            modEntry.OnToggle = OnToggle;
            new Harmony(modEntry.Info.Id).PatchAll(Assembly.GetExecutingAssembly());
            Operations.Registration.RegisterIfReady();
            modEntry.Logger.Log("AutomateFleet loaded");
            return true;
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            enabled = value;
            if (!value)
                Automation.FleetOrders.Clear();
            return true;
        }

        public static void Log(string message) => mod?.Logger.Log(message);

        public static void Error(string message) => mod?.Logger.Error(message);
    }
}
