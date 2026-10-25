using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using PavonisInteractive.TerraInvicta;

namespace AutomateFleet.Operations
{
    // Fleet operations are a plain code list (OperationsManager.fleetOperations); add ours to it
    [HarmonyPatch(typeof(OperationsManager), nameof(OperationsManager.Initalize))]
    static class Registration
    {
        static void Postfix() => RegisterIfReady();

        // Also called from Load in case the game built the list before the mod loaded
        public static void RegisterIfReady()
        {
            var list = OperationsManager.fleetOperations;
            if (list == null || list.Count == 0)
                return;
            if (!list.Any(o => o is AutomateFleetOperation))
                list.Add(new AutomateFleetOperation());
            if (!list.Any(o => o is StopFleetAutomationOperation))
                list.Add(new StopFleetAutomationOperation());
        }
    }

    // Operation names and icons are looked up by class name; ours have no entries in the game's files
    static class Text
    {
        public static readonly Dictionary<string, string> Strings = new Dictionary<string, string>
        {
            { nameof(AutomateFleetOperation) + ".displayName", "Automate Fleet" },
            { nameof(AutomateFleetOperation) + ".description", "Give this fleet standing orders to keep a body's orbits clear." },
            { nameof(StopFleetAutomationOperation) + ".displayName", "Stop Automation" },
            { nameof(StopFleetAutomationOperation) + ".description", "Cancel this fleet's standing orders." },
        };
    }

    [HarmonyPatch(typeof(Loc), nameof(Loc.T), typeof(string))]
    static class LocPatch
    {
        static bool Prefix(string key, ref string __result)
        {
            if (key == null || !Text.Strings.TryGetValue(key, out var text))
                return true;
            __result = text;
            return false;
        }
    }
}
