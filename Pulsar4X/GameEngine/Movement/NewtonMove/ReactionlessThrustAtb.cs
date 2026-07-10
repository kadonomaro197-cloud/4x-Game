using Pulsar4X.Engine;
using Pulsar4X.Datablobs;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;

namespace Pulsar4X.Movement
{
    /// <summary>
    /// A REACTIONLESS drive — the Exotic-propulsion "no propellant, infinite delta-V" component (Propulsion ⚙2). An
    /// ordinary rocket makes thrust by throwing reaction mass out the back, so its <see cref="NewtonThrustAbilityDB.ThrustInNewtons"/>
    /// = exhaust velocity × fuel-burn rate, and its delta-V is fuel-limited (Tsiolkovsky). A reactionless drive
    /// (gravitic / inertialess / warp-field) pushes without a reaction mass: it sets <see cref="NewtonThrustAbilityDB.ThrustInNewtons"/>
    /// DIRECTLY and burns NO fuel, so it never runs dry and its delta-V is effectively unlimited
    /// (<see cref="NewtonThrustAbilityDB.ReactionlessDeltaV"/>). The payoff on the combat/closing resolver: a
    /// reactionless fleet's maneuver reserve (`FleetCombat.DeltaVFloor` → `FleetCombatStateDB.ManeuverBudget`) never
    /// depletes, so it can kite forever. The cost is power/tech + mass (the physics-breaker's price).
    ///
    /// A component (<see cref="IComponentDesignAttribute"/>, CONVENTIONS §6), designed / built / installed / lost like
    /// any drive — cradle to grave (a shot-off drive strands the ship). Sets <see cref="NewtonThrustAbilityDB.Reactionless"/>
    /// true on install, which pins the delta-V and skips the fuel machinery (<see cref="NewtonThrustAbilityDB.SetFuel"/>
    /// / `CargoTransferProcessor.UpdateMassFuelAndDeltaV` guard). No effect on any ship that doesn't mount one →
    /// byte-identical.
    ///
    /// v1 scope (flagged): this delivers the COMBAT/closing payoff (infinite maneuver budget) + the strategic delta-V
    /// readout. The deeper in-space burn model — `NewtonianMovementProcessor` executing an actual burn without
    /// consuming fuel — is a separate follow-up; the resolver reads <see cref="NewtonThrustAbilityDB.ThrustInNewtons"/>
    /// (evasion / who-dictates-range) and <see cref="NewtonThrustAbilityDB.DeltaV"/> (maneuver budget), both of which
    /// this sets correctly.
    /// </summary>
    public class ReactionlessThrustAtb : IComponentDesignAttribute
    {
        /// <summary>Thrust the drive produces, in Newtons — set DIRECTLY (not exhaust-velocity × burn-rate), because a
        /// reactionless drive expends no reaction mass. Feeds acceleration → Evasion / who-dictates-range.</summary>
        public double ThrustInNewtons;

        /// <summary>A nominal exhaust velocity (m/s) carried for the future in-space burn model; unused by the delta-V
        /// (which is pinned unlimited for a reactionless drive), so it never limits maneuvering.</summary>
        public double ExhaustVelocity;

        public ReactionlessThrustAtb(double thrustInNewtons, double exhaustVelocity)
        {
            ThrustInNewtons = thrustInNewtons;
            ExhaustVelocity = exhaustVelocity;
        }

        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance)
        {
            NewtonThrustAbilityDB db;
            if (!parentEntity.HasDataBlob<NewtonThrustAbilityDB>())
            {
                db = new NewtonThrustAbilityDB(""); // reactionless: no fuel type
                parentEntity.SetDataBlob(db);
            }
            else
            {
                db = parentEntity.GetDataBlob<NewtonThrustAbilityDB>();
            }

            db.Reactionless = true;                 // pins delta-V unlimited + skips the fuel machinery
            db.ThrustInNewtons += ThrustInNewtons;  // set directly — no exhaust-velocity × burn-rate
            if (ExhaustVelocity > db.ExhaustVelocity) db.ExhaustVelocity = ExhaustVelocity;
            // Pin the (unlimited) delta-V now, before any cargo update: SetFuel with Reactionless=true takes the guard
            // path and assigns DeltaV = ReactionlessDeltaV (DeltaV's own setter is private — SetFuel is the accessor).
            db.SetFuel(0, 0);
        }

        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Reactionless Drive";
        public string AtbDescription() => $"A reactionless drive producing {ThrustInNewtons:0} N with NO propellant — unlimited delta-V, never runs dry, never loses its maneuver reserve (the fleet can kite forever).";
    }
}
