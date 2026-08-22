using System.Collections.Generic;
using NUnit.Framework;
using Newtonsoft.Json;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// DS-HAULER — a supply convoy as a LOSABLE ground unit (OPERATION BLUEPRINT-TO-STEEL slice DS-hauler-unit,
    /// `docs/ground/UNITS-ON-THE-MAP-DESIGN.md` §3). Today the standing haul route (DS-T1) + the one-shot
    /// <see cref="HexHaulOrder"/> move ore hex→hex as pure bookkeeping — nothing can be intercepted. DS-hauler makes a
    /// real <see cref="GroundUnit"/> carry the ore: LOAD it from a hex, MARCH it (the cargo rides for free as a unit
    /// field), UNLOAD at the destination — and if the hauler is KILLED mid-haul its load is STRANDED onto the hex it
    /// died on (recoverable), not teleported home.
    ///
    /// These gauges pin: the conserved+capped LOAD (+ carry cap), the conserved UNLOAD, the L12 save-safety of the new
    /// <see cref="GroundUnit.MineralCargo"/> field (copy-ctor / GroundForcesDB.Clone / JSON round-trip), the pure
    /// STRAND-ON-DEATH (a cargo-carrying dead unit strands, a cargo-less one drops nothing → byte-identical), and — the
    /// KEY case, end-to-end on a REAL body/grid through the casualty path — that <see cref="GroundForcesProcessor"/>
    /// strands a killed hauler's load onto its current global hex while a cargo-less death drops nothing.
    /// </summary>
    [TestFixture]
    public class GroundHaulerTests
    {
        private const int Iron = 7;      // an ICargoable.ID (the same int GroundHex.Stockpile / DepositMineralId use)
        private const int Copper = 12;
        private const int InvaderFaction = 424242;

        private static void Log(string m) => TestContext.Progress.WriteLine("[hauler] " + m);

        // ─────────────────────────── PURE: load / carry-cap / unload (hand-built hex + unit) ───────────────────────────

        [Test]
        [Description("LoadFromHex moves ore off a hex onto the unit CONSERVED (unit gains exactly what the hex loses), capped by what's on hand; a second good is untouched; amount<=0 loads ALL.")]
        public void LoadFromHex_MovesOntoUnit_Conserved_AndCapped()
        {
            var hex = new GroundHex(0, 0, RegionFeatureType.Plains);
            hex.AddToStockpile(Iron, 500);
            hex.AddToStockpile(Copper, 40);   // the route must not touch a good it isn't hauling
            var unit = new GroundUnit();

            // Partial load: takes exactly the amount, conserved.
            long loaded = GroundHauler.LoadFromHex(hex, unit, Iron, 200);
            Assert.That(loaded, Is.EqualTo(200), "loaded the requested amount");
            Assert.That(hex.StockpileOf(Iron), Is.EqualTo(300), "the hex fell by exactly what was loaded");
            Assert.That(GroundHauler.CargoOf(unit, Iron), Is.EqualTo(200), "the unit gained exactly that (conserved)");
            Assert.That(GroundHauler.CargoOf(unit, Copper), Is.EqualTo(0), "the other good was not touched");

            // Over-request is capped at what's on hand (take-what-fits), and amount<=0 means "all remaining".
            long loadedRest = GroundHauler.LoadFromHex(hex, unit, Iron, 0);
            Assert.That(loadedRest, Is.EqualTo(300), "amount<=0 loads all remaining");
            Assert.That(hex.StockpileOf(Iron), Is.EqualTo(0), "the hex is emptied of iron");
            Assert.That(GroundHauler.CargoOf(unit, Iron), Is.EqualTo(500), "the unit now carries the whole load");

            // Defensive: an empty source / null hex loads nothing and never throws.
            Assert.That(GroundHauler.LoadFromHex(hex, unit, Iron, 100), Is.EqualTo(0), "an empty good loads nothing");
            Assert.That(GroundHauler.LoadFromHex(null, unit, Iron, 100), Is.EqualTo(0), "a null hex is a no-op");
            Log($"loaded 500 iron onto the hauler (hex emptied, copper untouched at {hex.StockpileOf(Copper)})");
        }

        [Test]
        [Description("A carry cap limits a load to the unit's REMAINING capacity across its whole load — a unit can't carry past its limit.")]
        public void LoadFromHex_HonoursCarryCap()
        {
            var hex = new GroundHex(0, 0, RegionFeatureType.Plains);
            hex.AddToStockpile(Iron, 1000);
            var unit = new GroundUnit();

            // Cap 300: the first load fills to the cap; the second (already full) loads nothing.
            long first = GroundHauler.LoadFromHex(hex, unit, Iron, 500, carryCap: 300);
            Assert.That(first, Is.EqualTo(300), "capped at the unit's carry limit, not the 500 requested");
            Assert.That(GroundHauler.TotalCargo(unit), Is.EqualTo(300));
            Assert.That(hex.StockpileOf(Iron), Is.EqualTo(700), "only the loaded amount left the hex (conserved)");

            long second = GroundHauler.LoadFromHex(hex, unit, Iron, 500, carryCap: 300);
            Assert.That(second, Is.EqualTo(0), "a full unit loads nothing more");
            Assert.That(GroundHauler.TotalCargo(unit), Is.EqualTo(300), "still at the cap");
        }

        [Test]
        [Description("UnloadToHex drops the unit's WHOLE load onto a hex, conserved (the hex gains what the unit carried), and empties the unit.")]
        public void UnloadToHex_DropsWholeLoad_Conserved()
        {
            var src = new GroundHex(0, 0, RegionFeatureType.Plains);
            var dst = new GroundHex(1, 0, RegionFeatureType.Plains);
            src.AddToStockpile(Iron, 400);
            src.AddToStockpile(Copper, 60);
            var unit = new GroundUnit();

            GroundHauler.LoadFromHex(src, unit, Iron, 0);     // whole iron load
            GroundHauler.LoadFromHex(src, unit, Copper, 0);   // whole copper load
            Assert.That(GroundHauler.TotalCargo(unit), Is.EqualTo(460), "carrying both goods");

            long dropped = GroundHauler.UnloadToHex(dst, unit);
            Assert.That(dropped, Is.EqualTo(460), "the whole load was dropped");
            Assert.That(dst.StockpileOf(Iron), Is.EqualTo(400), "iron arrived at the destination (conserved)");
            Assert.That(dst.StockpileOf(Copper), Is.EqualTo(60), "copper arrived too");
            Assert.That(GroundHauler.TotalCargo(unit), Is.EqualTo(0), "the unit is emptied after unloading");
            // End-to-end conservation: nothing was created or destroyed across load→unload.
            Assert.That(src.StockpileOf(Iron) + dst.StockpileOf(Iron), Is.EqualTo(400));
            Assert.That(src.StockpileOf(Copper) + dst.StockpileOf(Copper), Is.EqualTo(60));
        }

        // ─────────────────────────── L12: the cargo survives Clone + JSON round-trip ───────────────────────────

        [Test]
        [Description("L12: the new MineralCargo field deep-copies through the GroundUnit copy-ctor and GroundForcesDB.Clone, and survives a JSON round-trip — a moved/saved hauler keeps its load.")]
        public void Cargo_SurvivesCloneAndJsonRoundTrip()
        {
            var unit = new GroundUnit { Name = "Convoy", FactionOwnerID = 1, RegionIndex = 0 };
            unit.MineralCargo[Iron] = 250;
            unit.MineralCargo[Copper] = 75;

            // (a) GroundUnit copy-ctor is an INDEPENDENT deep copy (a shallow ref-copy would fail the isolation check).
            var copy = new GroundUnit(unit);
            Assert.That(GroundHauler.CargoOf(copy, Iron), Is.EqualTo(250), "the copy carries the load");
            copy.MineralCargo[Iron] = 999;
            Assert.That(GroundHauler.CargoOf(unit, Iron), Is.EqualTo(250), "mutating the copy must not touch the original");
            Assert.That(ReferenceEquals(unit.MineralCargo, copy.MineralCargo), Is.False, "the two dicts are distinct objects");

            // (b) GroundForcesDB.Clone (the real cross-manager move path) deep-copies the unit's cargo.
            var forces = new GroundForcesDB();
            forces.Units.Add(unit);
            var clonedForces = (GroundForcesDB)forces.Clone();
            Assert.That(GroundHauler.CargoOf(clonedForces.Units[0], Iron), Is.EqualTo(250), "the cloned roster's unit keeps its load");
            Assert.That(GroundHauler.CargoOf(clonedForces.Units[0], Copper), Is.EqualTo(75));

            // (c) JSON round-trip (the real save path uses [JsonProperty]).
            var json = JsonConvert.SerializeObject(unit);
            var loaded = JsonConvert.DeserializeObject<GroundUnit>(json);
            Assert.That(loaded, Is.Not.Null);
            Assert.That(GroundHauler.CargoOf(loaded, Iron), Is.EqualTo(250), "iron survived the round-trip");
            Assert.That(GroundHauler.CargoOf(loaded, Copper), Is.EqualTo(75), "copper survived the round-trip");
        }

        [Test]
        [Description("An OLD save that lacks the MineralCargo property loads with an empty (never-null) load — the initializer is the byte-identical fallback (Newtonsoft leaves an absent property at its ctor value).")]
        public void Cargo_AbsentInJson_LoadsAsEmptyNotNull()
        {
            // A pre-DS-hauler unit's JSON has no MineralCargo key at all.
            var loaded = JsonConvert.DeserializeObject<GroundUnit>("{\"Name\":\"Old\",\"Health\":500.0}");
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.MineralCargo, Is.Not.Null, "an absent property keeps the initializer's empty dict, not null");
            Assert.That(GroundHauler.TotalCargo(loaded), Is.EqualTo(0), "it carries nothing → byte-identical");
        }

        // ─────────────────────────── STRAND-ON-DEATH: the grave rung ───────────────────────────

        [Test]
        [Description("Pure grave rung: a cargo-carrying dead unit STRANDS its whole load onto the hex it died on (recoverable); a cargo-less unit's death drops NOTHING (byte-identical).")]
        public void StrandOnDeath_CargoCarryingUnit_StrandsOntoHex_CargoLessDropsNothing()
        {
            var hex = new GroundHex(3, 2, RegionFeatureType.Plains);
            hex.AddToStockpile(Iron, 100);   // some ore already sits here — the strand ADDS to it, conserved

            // A cargo-carrying hauler is killed here: its load is dropped onto this hex, recoverable.
            var hauler = new GroundUnit { Health = 0 };
            hauler.MineralCargo[Iron] = 300;
            long stranded = GroundHauler.StrandOnDeath(hex, hauler);
            Assert.That(stranded, Is.EqualTo(300), "the whole load was stranded");
            Assert.That(hex.StockpileOf(Iron), Is.EqualTo(400), "the load landed on the hex it died on (added to what was there)");
            Assert.That(GroundHauler.TotalCargo(hauler), Is.EqualTo(0), "the dead unit no longer holds the load");

            // A cargo-less unit's death strands nothing — the byte-identity case (every unit in a stock game).
            var soldier = new GroundUnit { Health = 0 };
            long nothing = GroundHauler.StrandOnDeath(hex, soldier);
            Assert.That(nothing, Is.EqualTo(0), "a cargo-less death drops nothing");
            Assert.That(hex.StockpileOf(Iron), Is.EqualTo(400), "the hex is unchanged by a cargo-less death (byte-identical)");
        }

        // ─────────── KEY WIRE: the casualty path strands a killed hauler's load, on a REAL body/grid ───────────

        private static GroundUnitDesign MakeHaulerDesign() => new GroundUnitDesign
        {
            UniqueID = "test-ground-hauler",
            Name = "Test Convoy",
            UnitType = GroundUnitType.Infantry,
            Attack = 100, Defense = 10, HitPoints = 500,
            IndustryPointCosts = 100,
            IndustryTypeID = "installation",
            ResourceCosts = new Dictionary<string, long>(),
        };

        [Test]
        [Description("KEY: GroundForcesProcessor's casualty step strands a KILLED hauler's load onto its current global hex (recoverable, conserved) while a cargo-less death on another hex drops nothing — end-to-end on a real body/grid, byte-identical for the cargo-less unit.")]
        public void CasualtyPath_StrandsAKilledHaulersLoad_ButACargoLessDeathDropsNothing()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var design = MakeHaulerDesign();

            // Two units on different regions → different global muster hexes (so their stockpiles don't collide).
            var hauler = GroundForces.RaiseUnit(body, design, s.Faction.Id, regionIndex: 0);
            var soldier = GroundForces.RaiseUnit(body, design, InvaderFaction, regionIndex: 1);

            // Load the hauler from the very hex it stands on: 200 stay on the hex, 300 ride the unit.
            var grid = PlanetGridFactory.EnsureGridForBody(body);
            Assert.That(grid, Is.Not.Null, "the real body has a surface grid");
            var haulerHex = grid.HexAt(hauler.GlobalQ, hauler.GlobalR);
            Assert.That(haulerHex, Is.Not.Null, "the raised hauler musters on a real land hex");
            haulerHex.AddToStockpile(Iron, 500);
            long loaded = GroundHauler.LoadFromHex(haulerHex, hauler, Iron, 300);
            Assert.That(loaded, Is.EqualTo(300), "loaded 300 onto the hauler off its own hex");
            Assert.That(haulerHex.StockpileOf(Iron), Is.EqualTo(200), "200 left on the source hex");

            // Capture positions BEFORE the units are removed by the casualty step.
            int hQ = hauler.GlobalQ, hR = hauler.GlobalR;
            // Regions 0 and 1 muster on distinct band-centre columns; guard so an impossible collision is inconclusive,
            // not a false red (the two hexes' stockpiles would otherwise overlap).
            Assume.That(soldier.GlobalQ != hQ || soldier.GlobalR != hR, "regions 0 and 1 muster on distinct global hexes");
            var soldierHex = grid.HexAt(soldier.GlobalQ, soldier.GlobalR);
            long soldierHexBefore = soldierHex?.StockpileOf(Iron) ?? 0;

            // Kill both units, then run ONE ground tick — the casualty step strands the hauler's load, then removes both.
            hauler.Health = 0;
            soldier.Health = 0;
            new GroundForcesProcessor().ProcessEntity(body, 3600);

            var forces = body.GetDataBlob<GroundForcesDB>();
            Assert.That(forces.Units.Count, Is.EqualTo(0), "both dead units were removed");

            // The hauler's load is now on the hex it died on — recoverable, and conserved (200 leftover + 300 stranded).
            var deathHex = grid.HexAt(hQ, hR);
            Assert.That(deathHex.StockpileOf(Iron), Is.EqualTo(500),
                "the killed hauler's 300 was stranded onto its current hex (200 leftover + 300 = the full 500, conserved — not teleported home)");

            // The cargo-less death dropped nothing — byte-identical.
            long soldierHexAfter = grid.HexAt(soldier.GlobalQ, soldier.GlobalR)?.StockpileOf(Iron) ?? 0;
            Assert.That(soldierHexAfter, Is.EqualTo(soldierHexBefore), "a cargo-less unit's death strands nothing (byte-identical)");
            Log($"killed hauler stranded 300 iron onto hex ({hQ},{hR}); cargo-less death dropped nothing");
        }
    }
}
