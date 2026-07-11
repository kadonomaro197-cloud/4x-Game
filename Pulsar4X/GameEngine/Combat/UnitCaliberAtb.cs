using System;
using Newtonsoft.Json;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Combat
{
    /// <summary>
    /// A UNIT-CALIBER module — the Enhancers ⚙6.2 "Training / Doctrine (Unit Caliber)" component: a per-hull ELITE
    /// stamp (a veteran cadre / command crew / battle-hardened outfit) that makes an otherwise-identical chassis
    /// fight better. It multiplies this hull's <see cref="ShipCombatValueDB.Firepower"/> and
    /// <see cref="ShipCombatValueDB.Toughness"/> at the moment the spec-sheet is built.
    ///
    /// Why it's a GENUINELY new axis, not a duplicate of an existing multiplier:
    ///  • Doctrine (`FleetDoctrineDB`) is a *switchable fleet/component posture* read at salvo time — every ship in a
    ///    component shares it and it flips on command.
    ///  • Commander (`CombatEngagement.FleetCommanderMult`) is the *flagship officer* scaling the WHOLE fleet uniformly.
    ///  • Caliber is baked PER-HULL AT BUILD. It is the first (and only) thing that makes two ships of identical
    ///    chassis + weapons + armour, in the same fleet under the same admiral, fight differently — the dossier's exact
    ///    acceptance test ("the veteran cruiser ≠ the stock freighter"). It STACKS on doctrine × commander.
    ///
    /// A component (<see cref="IComponentDesignAttribute"/>, CONVENTIONS §6) — designed / researched / built / mounted
    /// / lost like any part (cradle to grave). <see cref="ShipCombatValueDB.Calculate"/> reads the best installed
    /// module's mults, each HEALTH-SCALED toward 1.0 (a shot-off caliber module reverts the hull to a green-crew
    /// baseline — the grave rung, same shape as the inertialess drive's floor). <b>No module → mult 1.0</b> → firepower
    /// and toughness are unchanged and combat is byte-identical (every current ship). Inert on install (the combat
    /// value reads the numbers; install/uninstall are no-ops). Never throws. Lives beside its sole reader.
    /// </summary>
    public class UnitCaliberAtb : BaseDataBlob, IComponentDesignAttribute
    {
        /// <summary>The FIREPOWER multiplier this caliber grants the hull (≥ 0; 1.0 = green-crew baseline, e.g. 1.30 =
        /// an elite crew hits 30% harder). Health-scaled at read, so a damaged module fades toward 1.0.</summary>
        [JsonProperty] public double FirepowerMult { get; internal set; } = 1.0;

        /// <summary>The TOUGHNESS multiplier this caliber grants the hull (≥ 0; 1.0 = baseline, e.g. 1.20 = a
        /// battle-hardened crew soaks 20% more). Health-scaled at read.</summary>
        [JsonProperty] public double ToughnessMult { get; internal set; } = 1.0;

        public UnitCaliberAtb() { }

        // double args for the JSON/NCalc binder (gotcha L7) — the base-mod template feeds AtbConstrArgs(PropertyValue(...)).
        public UnitCaliberAtb(double firepowerMult, double toughnessMult)
        {
            FirepowerMult = firepowerMult < 0 ? 0 : firepowerMult;
            ToughnessMult = toughnessMult < 0 ? 0 : toughnessMult;
        }

        public override object Clone() => new UnitCaliberAtb(FirepowerMult, ToughnessMult);

        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance) { }
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Unit Caliber";
        public string AtbDescription() => $"A veteran-cadre module — an elite crew that makes this hull fight above its chassis: firepower ×{FirepowerMult:0.00}, toughness ×{ToughnessMult:0.00} (fades toward baseline as the module takes damage). Stacks on top of fleet doctrine and the admiral's bonus.";
    }
}
