using System;
using System.Collections.Generic;
using AutomateFleet.Automation;
using PavonisInteractive.TerraInvicta;

namespace AutomateFleet.Operations
{
    // "Automate fleet" in the fleet's operations panel: opens the standing-orders window.
    // Modelled on the game's SetContinuousBombardXenoformingOperation (an instant, self-targeted standing order).
    public class AutomateFleetOperation : TISpaceFleetOperationTemplate
    {
        public override OperationTiming GetOperationTiming() => OperationTiming.InstantExecution;

        public override Type GetTargetingMethod() => typeof(TIOperationTargeting_Self);

        public override bool ActorCanPerformOperation(TIGameState actorState, TIGameState target) =>
            base.ActorCanPerformOperation(actorState, target) && OpVisibleToActor(actorState);

        public override bool OpVisibleToActor(TIGameState actorState, TIGameState targetState = null)
        {
            var fleet = actorState?.ref_fleet;
            return Main.enabled && fleet != null && fleet.faction != null && fleet.faction.isActivePlayer && !FleetOrders.IsAutomated(fleet);
        }

        public override List<TIGameState> GetPossibleTargets(TIGameState actorState, TIGameState defaultTarget = null) =>
            new List<TIGameState> { actorState };

        public override int SortOrder() => 16;

        public override float GetDuration_days(TIGameState actorState, TIGameState target, Trajectory trajectory = null) => 0f;

        public override string GetDescription(TIGameState actorState = null, TIGameState target = null) =>
            "Give this fleet standing orders to keep a body's orbits clear of enemy fleets and stations.";

        public override void ExecuteOperation(TIGameState actorState, TIGameState target)
        {
            var fleet = actorState?.ref_fleet;
            if (fleet != null)
                UI.OrdersWindow.Open(fleet);
        }
    }

    // "Stop automation": hands the fleet back to the player
    public class StopFleetAutomationOperation : TISpaceFleetOperationTemplate
    {
        public override OperationTiming GetOperationTiming() => OperationTiming.InstantExecution;

        public override Type GetTargetingMethod() => typeof(TIOperationTargeting_Self);

        public override bool ActorCanPerformOperation(TIGameState actorState, TIGameState target) =>
            base.ActorCanPerformOperation(actorState, target) && OpVisibleToActor(actorState);

        public override bool OpVisibleToActor(TIGameState actorState, TIGameState targetState = null) =>
            FleetOrders.IsAutomated(actorState?.ref_fleet);

        public override List<TIGameState> GetPossibleTargets(TIGameState actorState, TIGameState defaultTarget = null) =>
            new List<TIGameState> { actorState };

        public override int SortOrder() => 16;

        public override float GetDuration_days(TIGameState actorState, TIGameState target, Trajectory trajectory = null) => 0f;

        public override string GetDescription(TIGameState actorState = null, TIGameState target = null) =>
            "Cancel this fleet's standing orders. It keeps whatever it is currently doing.";

        public override void ExecuteOperation(TIGameState actorState, TIGameState target) =>
            FleetOrders.Remove(actorState?.ref_fleet, "stopped by player");
    }
}
