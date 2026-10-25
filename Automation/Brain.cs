using System;
using System.Linq;
using HarmonyLib;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Actions;
using PavonisInteractive.TerraInvicta.Tasks;
using UnityEngine;

namespace AutomateFleet.Automation
{
    // Once a day, for each automated fleet: stop if a too-strong enemy is coming, go home if it needs resupply,
    // otherwise go after the weakest valid enemy fleet around the body, then any enemy station, then wait at the homeport.
    [HarmonyPatch(typeof(TIFactionState), nameof(TIFactionState.DailyFleetsUpdate))]
    static class Brain
    {
        // Below this fraction of its maximum delta-v the fleet stops and asks for resupply
        const float MinDeltaVFraction = 0.25f;

        static void Postfix(TIFactionState __instance)
        {
            if (!Main.enabled || !__instance.isActivePlayer)
                return;
            foreach (var orders in FleetOrders.All)
            {
                try
                {
                    Tick(orders);
                }
                catch (Exception e)
                {
                    Main.Error($"Automation for {orders.fleet?.displayName} failed: {e}");
                }
            }
        }

        static void Tick(Orders o)
        {
            var fleet = o.fleet;
            var player = fleet?.faction;
            if (fleet == null || !fleet.exists || player == null || !player.isActivePlayer || !player.fleets.Contains(fleet))
            {
                FleetOrders.Remove(fleet, "fleet no longer available");
                return;
            }

            var threat = FindThreat(o, player);
            if (threat != null)
            {
                var when = threat.transferAssigned && threat.trajectory?.arrivalTime != null
                    ? $"arriving {threat.trajectory.arrivalTime}"
                    : "already there";
                Stop(o, $"{threat.displayName} ({threat.faction.displayName}, strength {threat.SpaceCombatValue():N0}) is heading for {o.body.displayName}, {when}. " +
                        $"That's above this fleet's limit of {o.maxEnemyStrength:N0}, so automation has stopped.");
                return;
            }

            // Busy: travelling, fighting, or already carrying out an order
            if (fleet.transferAssigned || fleet.inCombatOrWaitingForCombat || fleet.unavailableForOperations || fleet.CurrentOperations().Count > 0)
                return;

            // Damaged or low on propellant: go home and wait there until the homeport has fixed it up
            if (fleet.NeedsRepair() || (fleet.maxDeltaV_kps > 0f && fleet.currentDeltaV_kps < fleet.maxDeltaV_kps * MinDeltaVFraction))
            {
                if (!TIGameState.Valid(fleet.homeport))
                    Stop(o, $"{fleet.displayName} needs repairs or propellant and has no homeport, so automation has stopped.");
                else if (!AtHomeport(fleet))
                    GoHome(fleet, "resupply");
                return;
            }

            var enemy = o.body.fleetsInOrbit
                .Where(f => IsTarget(o, f) && !f.transferAssigned && !f.dockedAtHab && f.SpaceCombatValue() <= o.maxEnemyStrength)
                .OrderBy(f => f.ref_orbit == fleet.ref_orbit ? 0 : 1)
                .ThenBy(f => f.SpaceCombatValue())
                .FirstOrDefault();
            if (enemy != null)
            {
                if (enemy.ref_orbit != fleet.ref_orbit || fleet.dockedOrLanded)
                    Transfer(fleet, enemy, "intercept " + enemy.displayName);
                // Same orbit: leave it to the game to start the fight
                return;
            }

            var station = o.body.stationsInOrbit
                .Where(h => h.exists && h.faction != player && o.targets.Contains(h.faction))
                .OrderBy(h => h.ref_orbit == fleet.ref_orbit ? 0 : 1)
                .FirstOrDefault();
            if (station != null)
            {
                AttackStation(fleet, station);
                return;
            }

            // Nothing to do: wait at the homeport
            if (TIGameState.Valid(fleet.homeport) && !AtHomeport(fleet))
                GoHome(fleet, "wait for targets");
        }

        static bool AtHomeport(TISpaceFleetState fleet) => fleet.dockedOrLanded && fleet.ref_hab == fleet.homeport;

        static void GoHome(TISpaceFleetState fleet, string purpose)
        {
            var home = fleet.homeport;
            if (fleet.ref_orbit == home.ref_orbit && home.ref_orbit != null && !fleet.dockedOrLanded)
            {
                fleet.faction.playerControl.StartAction(new ApproachDockAction(home, fleet));
                Main.Log($"{fleet.displayName}: docking at homeport {home.displayName} to {purpose}");
                return;
            }
            // No delta-v reserve needed for the trip home
            Transfer(fleet, home, $"return to homeport {home.displayName} to {purpose}", 0f);
        }

        static bool IsTarget(Orders o, TISpaceFleetState f) =>
            f != null && f.exists && f.faction != o.fleet.faction && o.targets.Contains(f.faction);

        // A targeted or at-war fleet above the limit that is around the body, or heading for it (or for this fleet)
        static TISpaceFleetState FindThreat(Orders o, TIFactionState player)
        {
            return player.KnownFleets
                .Where(f => f != null && f.exists && f.faction != player
                    && (o.targets.Contains(f.faction) || FleetOrders.AtWar(player, f.faction))
                    && f.SpaceCombatValue() > o.maxEnemyStrength)
                .FirstOrDefault(f =>
                {
                    if (f.transferAssigned)
                    {
                        var t = f.trajectory;
                        return t.destinationFleet == o.fleet || t.destination?.ref_spaceBody == o.body;
                    }
                    return f.ref_spaceBody == o.body;
                });
        }

        static void Transfer(TISpaceFleetState fleet, TIGameState target, string purpose, float reserveFraction = MinDeltaVFraction)
        {
            var op = OperationsManager.fleetOperations.OfType<TransferOperation>().FirstOrDefault();
            if (op == null)
                return;
            // Same route planner the AI uses, keeping some of the fleet's delta-v in reserve
            AIDailyFactionPlanner.SelectTrajectoryAsync(fleet, target, reserveFraction, out var result, trajectory =>
            {
                if (trajectory == null || !fleet.exists || fleet.transferAssigned || !FleetOrders.IsAutomated(fleet))
                    return;
                if (op.OnOperationConfirm(fleet, target, null, trajectory))
                    Main.Log($"{fleet.displayName}: transferring to {purpose}");
            });
            if (result != null && result.Result.ToString().StartsWith("Fail"))
                Main.Log($"{fleet.displayName}: no route to {purpose} ({result.Result})");
        }

        static void AttackStation(TISpaceFleetState fleet, TIHabState station)
        {
            if (fleet.dockedLocation?.ref_hab == station)
            {
                var destroy = OperationsManager.fleetOperations.OfType<DestroyHabOperation>().FirstOrDefault();
                if (destroy != null && destroy.ActorCanPerformOperation(fleet, station))
                {
                    if (destroy.OnOperationConfirm(fleet, station))
                        Main.Log($"{fleet.displayName}: destroying {station.displayName}");
                }
                return;
            }
            if (fleet.ref_orbit == station.ref_orbit && !fleet.dockedOrLanded)
            {
                // Docking with a defended enemy station starts the fight; once docked we destroy it next day
                fleet.faction.playerControl.StartAction(new ApproachDockAction(station, fleet));
                Main.Log($"{fleet.displayName}: approaching {station.displayName}");
                return;
            }
            Transfer(fleet, station, "attack " + station.displayName);
        }

        static void Stop(Orders o, string message)
        {
            FleetOrders.Remove(o.fleet, message);
            var controls = UnityEngine.Object.FindObjectOfType<GeneralControlsController>();
            controls?.PauseSpeedNoToggle();
            UI.AlertWindow.Show("Fleet automation stopped", message);
        }
    }
}
