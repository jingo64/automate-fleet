using System.Collections.Generic;
using System.Linq;
using PavonisInteractive.TerraInvicta;

namespace AutomateFleet.Automation
{
    // Standing orders for one automated fleet. Kept in memory only: automation is off after loading a save.
    public class Orders
    {
        public TISpaceFleetState fleet;
        public TISpaceBodyState body;
        public HashSet<TIFactionState> targets = new HashSet<TIFactionState>();
        public float maxEnemyStrength;
    }

    public static class FleetOrders
    {
        static readonly Dictionary<TISpaceFleetState, Orders> orders = new Dictionary<TISpaceFleetState, Orders>();

        public static IEnumerable<Orders> All => orders.Values.ToList();

        public static bool IsAutomated(TISpaceFleetState fleet) => fleet != null && orders.ContainsKey(fleet);

        public static Orders Get(TISpaceFleetState fleet) => fleet != null && orders.TryGetValue(fleet, out var o) ? o : null;

        public static void Set(Orders o)
        {
            orders[o.fleet] = o;
            Main.Log($"Automating {o.fleet.displayName}: patrol {o.body.displayName}, max enemy strength {o.maxEnemyStrength:N0}, targets {string.Join(", ", o.targets.Select(t => t.displayName))}");
        }

        public static void Remove(TISpaceFleetState fleet, string reason)
        {
            if (fleet != null && orders.Remove(fleet))
                Main.Log($"Stopped automating {fleet.displayName}: {reason}");
        }

        public static void Clear() => orders.Clear();

        // Shown in the faction list and used to spot threats; the player may still target factions they're not at war with
        public static bool AtWar(TIFactionState player, TIFactionState other) =>
            other != null && other != player && !other.permanentAlly(player) && player.AI_AtWarWithFaction(other);
    }
}
