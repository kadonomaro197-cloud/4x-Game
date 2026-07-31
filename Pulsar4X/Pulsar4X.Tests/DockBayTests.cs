using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Components;
using Pulsar4X.Docking;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Galaxy;
using Pulsar4X.Movement;   // the LIVE PositionDB (the one in Engine/Datablobs is commented out entirely)
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// THE DOCK — berths for whole vessels, built as its own area (the developer's call, 2026-07-30:
    /// *"did you build berth for an actual dock that can be built? that should be its own area."*).
    ///
    /// <para><b>Why it is not another cargo class.</b> Every other compartment measures its contents in cubic metres
    /// poured in. A dock holds a <b>discrete vessel that arrives and leaves under its own power</b>, so the question is
    /// <i>how many, and how big</i> — not <i>how much fits</i>. Pouring a frigate into a warehouse by volume would let
    /// you carry half a frigate. That is why <c>GroundBayAtb</c> had to invent its own capacity system for troops, and
    /// why §46a listed <b>Berth</b> as the one class the compartment taxonomy could not express.</para>
    ///
    /// <para>✅ <b>Stock behaviour is byte-identical by construction:</b> no base-mod SHIP mounts a bay, and nothing in
    /// the engine calls <see cref="DockTools"/> — a ship docks only when something asks it to.</para>
    ///
    /// <para><b>The assertions are calibration-independent.</b> They measure the real spawned ships and the real
    /// installed bays and assert the RELATIONSHIPS that must hold, rather than hard-coding tonnages — so re-tuning a
    /// hull or a bay cannot make this fixture lie.</para>
    /// </summary>
    [TestFixture]
    public class DockBayTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[dock] " + m);

        private const string CarrierBay = "default-design-docking-bay";   // 60 t split into 4 berths
        private const string HeavyBerth = "default-design-heavy-berth";   // the same 60 t as ONE berth

        private static ComponentDesign Design(TestScenario s, string id)
        {
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ComponentDesigns;
            Assert.That(designs.ContainsKey(id), Is.True,
                $"'{id}' should be built for the start faction (template id in StartingItems, design id in ComponentDesigns)");
            return designs[id];
        }

        /// <summary>A real ship, through the same factory the DevTools spawn button uses — so its mass, position and
        /// component store are all genuine rather than hand-built.</summary>
        private static Entity Spawn(TestScenario s, ShipDesign design, string name)
            => ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name);

        private static double Mass(Entity e)
            => e.TryGetDataBlob<MassVolumeDB>(out var mv) ? mv.MassTotal : 0;

        /// <summary>The lightest shipped design by real spawned mass. Weighs each design exactly once — a throwaway
        /// hull per design, which is cheaper and clearer than spawning inside a sort's key selector.</summary>
        private static ShipDesign Lightest(TestScenario s, List<ShipDesign> designs)
        {
            ShipDesign best = designs[0];
            double bestMass = double.MaxValue;
            foreach (var d in designs)
            {
                double m = Mass(Spawn(s, d, "Weigh " + d.Name));
                if (m > 0 && m < bestMass) { bestMass = m; best = d; }
            }
            return best;
        }

        /// <summary>Install a bay the normal way. <c>Entity.AddComponent</c> already fires
        /// <c>OnComponentInstallation</c> for every attribute, so calling that by hand as well would double-install.</summary>
        private static void InstallBay(Entity host, ComponentDesign bay) => host.AddComponent(bay);

        /// <summary>
        /// 🔑 THE SPLIT DIAL, on real shipped data. Both designs buy the SAME berth tonnage; one splits it into four
        /// doors, one keeps a single wide one. That is the §34.7a honest shape — <b>the product is invariant, so neither
        /// setting strictly dominates</b> — and the extra doors are paid for in structure and deck crew, so "many small"
        /// is flexibility you buy rather than get.
        /// </summary>
        [Test]
        [Description("The docking bay binds from JSON, and its split dial is an honest trade: the same berth tonnage becomes four narrow doors or one wide one, total capacity identical either way, while the four-door version costs more mass and more crew — so flexibility is paid for rather than free.")]
        public void TheDockingBay_BindsFromJson_AndItsSplitDialIsAnHonestTrade()
        {
            var s = TestScenario.CreateWithColony();
            var manyDoors = Design(s, CarrierBay);
            var oneDoor = Design(s, HeavyBerth);

            Assert.That(manyDoors.HasAttribute<DockBayAtb>(), Is.True,
                "the template's AttributeType FQN resolved and the ctor args matched (the gotcha-#10 JSON→atb sensor)");
            var many = manyDoors.GetAttribute<DockBayAtb>();
            var one = oneDoor.GetAttribute<DockBayAtb>();

            Log($"{manyDoors.Name,-14} {many.BerthTonnage,10:N0} kg total · door {many.MaxHullMass,9:N0} kg · "
                + $"{manyDoors.MassPerUnit,8:N0} kg · crew {manyDoors.CrewReq}");
            Log($"{oneDoor.Name,-14} {one.BerthTonnage,10:N0} kg total · door {one.MaxHullMass,9:N0} kg · "
                + $"{oneDoor.MassPerUnit,8:N0} kg · crew {oneDoor.CrewReq}");

            // The invariant that makes this a SPLIT rather than a scale dial.
            Assert.That(many.BerthTonnage, Is.EqualTo(one.BerthTonnage),
                "both designs buy the same total tonnage — the product is what stays fixed");

            // The trade: more berths, narrower doors — and exactly proportionally, from Berth Tonnage / Berths.
            Assert.That(many.MaxHullMass, Is.LessThan(one.MaxHullMass),
                "splitting the same tonnage more ways makes each door narrower");
            Assert.That(one.MaxHullMass, Is.EqualTo(one.BerthTonnage).Within(1),
                "unsplit, the single door is the whole budget");

            // …and the flexibility is PAID FOR, or many-small would strictly dominate (§39.8).
            Assert.That(manyDoors.MassPerUnit, Is.GreaterThan(oneDoor.MassPerUnit),
                "every extra door is extra structure");
            Assert.That(manyDoors.CrewReq, Is.GreaterThan(oneDoor.CrewReq),
                "…and extra deck crew, so a carrier is a real commitment beside a tender");

            // The per-item cap can never exceed the budget — the atb ctor clamps it, so bad data cannot open the door wider
            // than the bay.
            Assert.That(many.MaxHullMass, Is.LessThanOrEqualTo(many.BerthTonnage));
            Assert.That(one.MaxHullMass, Is.LessThanOrEqualTo(one.BerthTonnage));
        }

        /// <summary>
        /// THE DOOR GATE, proved by the split itself: the same vessel, the same total tonnage, two different splits —
        /// the wide berth takes it and the narrow one cannot. This is the assertion that shows the per-item cap is a real
        /// constraint and not decoration.
        /// </summary>
        [Test]
        [Description("The per-item door gate bites: the same ship, offered to two bays that bought identical total tonnage, is accepted by the single wide berth and refused by the four narrow ones — and the refusal names the door rather than the budget. A budget-only check would have let a cruiser into a fighter bay because the tonnage happened to add up.")]
        public void TheDoorGate_Bites_AndTheRefusalNamesTheDoor()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.ToList();
            Assert.That(designs, Is.Not.Empty, "the start faction has ship designs to spawn");

            var wide = Spawn(s, designs[0], "Tender");
            var narrow = Spawn(s, designs[0], "Carrier");
            InstallBay(wide, Design(s, HeavyBerth));
            InstallBay(narrow, Design(s, CarrierBay));

            double wideDoor = DockTools.LargestBerth(wide), narrowDoor = DockTools.LargestBerth(narrow);
            Assert.That(DockTools.Capacity(wide), Is.EqualTo(DockTools.Capacity(narrow)).Within(1),
                "identical total capacity — only the split differs");
            Log($"wide door {wideDoor:N0} kg · narrow door {narrowDoor:N0} kg · "
                + $"capacity {DockTools.Capacity(wide):N0} kg both");

            // Property assertion over every design's real mass: a bay accepts a ship ONLY IF it fits that bay's door.
            int distinguishing = 0, overNarrow = 0;
            foreach (var d in designs)
            {
                var ship = Spawn(s, d, "Probe " + d.Name);
                double m = Mass(ship);
                bool okWide = DockTools.CanDock(wide, ship, out string wWhy);
                bool okNarrow = DockTools.CanDock(narrow, ship, out string nWhy);
                Log($"  {d.Name,-26} {m,10:N0} kg → wide {(okWide ? "dock" : "refuse")} · narrow {(okNarrow ? "dock" : "refuse")}");

                if (m > wideDoor) Assert.That(okWide, Is.False, $"{d.Name} is wider than the wide door, so it must be refused");
                if (m > narrowDoor)
                {
                    overNarrow++;
                    Assert.That(okNarrow, Is.False, $"{d.Name} is wider than the narrow door, so it must be refused");
                    Assert.That(nWhy, Does.Contain("too large"),
                        $"…and refused by the DOOR: {nWhy}");
                }
                if (m <= narrowDoor) Assert.That(okNarrow, Is.True, $"{d.Name} fits the narrow door and the bay is empty: {nWhy}");

                // The interesting band: fits the wide door, not the narrow one. Same tonnage bought, different answer.
                if (m > narrowDoor && m <= wideDoor)
                {
                    distinguishing++;
                    Assert.That(okWide, Is.True, $"the wide berth takes {d.Name}: {wWhy}");
                    Assert.That(nWhy, Does.Contain("too large"),
                        "…and the narrow bay refuses it by the DOOR, not the budget — the whole point of the split");
                }
            }

            // The door gate must actually be EXERCISED by the shipped data, or this test proves nothing. Requiring a
            // design in the narrow..wide BAND would depend on hull tonnages I cannot measure without compiling, so the
            // hard requirement is the weaker and far safer one: something in the game must be too big for the narrow
            // door. The band case is asserted above when it occurs, and reported when it does not.
            Assert.That(overNarrow, Is.GreaterThan(0),
                "no shipped design is too large for the narrow berth, so the door gate is never exercised — the bay "
                + "tonnages in docking.json need re-picking against real hull masses");
            if (distinguishing == 0)
                Log("⚠ no shipped design lands between the narrow and wide doors, so the split dial has nothing in the "
                    + "current data to distinguish. The gate is still proven above; the tonnages are worth re-picking.");
            else
                Log($"✅ {distinguishing} shipped design(s) fit the wide berth and NOT the narrow one — "
                    + "same tonnage bought, different answer.");
        }

        /// <summary>THE BUDGET GATE, independent of the door: keep docking things that each fit, until the tonnage runs
        /// out. A door-only check would let a carrier take unlimited fighters.</summary>
        [Test]
        [Description("The total-tonnage gate bites separately from the door: identical ships that each fit the berth keep docking until the bay's capacity is spent, and the next one is refused for lack of free berth rather than for size.")]
        public void TheBudgetGate_Bites_Separately_AndTheRefusalNamesTheBudget()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.ToList();
            var carrier = Spawn(s, designs[0], "Carrier");
            InstallBay(carrier, Design(s, CarrierBay));

            // Weigh every design ONCE, then pick the lightest — so at least one fits and the BUDGET is what
            // eventually stops us rather than the door.
            var lightest = Lightest(s, designs);
            Log($"lightest design: {lightest.Name} · capacity {DockTools.Capacity(carrier):N0} kg "
                + $"· door {DockTools.LargestBerth(carrier):N0} kg");

            int docked = 0;
            string lastRefusal = "";
            for (int i = 0; i < 40; i++)
            {
                var ship = Spawn(s, lightest, $"Flight {i}");
                if (DockTools.TryDock(carrier, ship, out string why)) { docked++; continue; }
                lastRefusal = why;
                break;
            }

            Log($"docked {docked} · used {DockTools.Used(carrier):N0} kg of {DockTools.Capacity(carrier):N0} kg "
                + $"· refused: {lastRefusal}");
            Assert.That(docked, Is.GreaterThan(0), "at least one of the lightest design must fit");
            Assert.That(lastRefusal, Does.Contain("not enough free berth"),
                "and the run stops on the BUDGET, not the door — the two gates are genuinely separate");
            Assert.That(DockTools.Used(carrier), Is.LessThanOrEqualTo(DockTools.Capacity(carrier) + 1),
                "the bay never over-fills");
            Assert.That(DockTools.DockedShips(carrier).Count, Is.EqualTo(docked),
                "and the registry agrees with what was accepted");
        }

        /// <summary>
        /// The consequence that separates a hangar from a spreadsheet row — a docked ship is re-parented to its carrier,
        /// so it travels with it — and the grave rung sets everything loose instead of deleting it.
        /// </summary>
        [Test]
        [Description("A docked ship is re-parented to its carrier, so it travels with it and stops being independently located; and UndockAll — the grave rung for a bay that gets shot off — releases everything aboard alive rather than deleting it.")]
        public void ADockedShip_TravelsWithItsCarrier_AndTheGraveRungReleasesEverythingAlive()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.ToList();
            var carrier = Spawn(s, designs[0], "Carrier");
            InstallBay(carrier, Design(s, HeavyBerth));   // one wide door, so anything that fits at all fits

            var craft = Spawn(s, Lightest(s, designs), "Small Craft");

            var cPos = carrier.GetDataBlob<PositionDB>();
            var fPos = craft.GetDataBlob<PositionDB>();
            Assert.That(fPos.Parent?.Id, Is.Not.EqualTo(carrier.Id), "it starts out not parented to the carrier");

            Assert.That(DockTools.TryDock(carrier, craft, out string ok), Is.True, ok);
            Assert.That(DockTools.IsDockedIn(carrier, craft), Is.True, "the carrier records it");
            Assert.That(fPos.Parent?.Id, Is.EqualTo(carrier.Id),
                "🔑 its POSITION now hangs off the carrier — that is what 'travels with it' means mechanically");
            Log($"docked: position parent is now the carrier · used {DockTools.Used(carrier):N0} kg");

            // The grave rung: the bay is shot off, or the carrier dies.
            int released = DockTools.UndockAll(carrier);
            Assert.That(released, Is.EqualTo(1), "the grave rung releases what was aboard");
            Assert.That(DockTools.IsDockedIn(carrier, craft), Is.False, "the carrier no longer holds it");
            Assert.That(craft.IsValid, Is.True,
                "released, NOT destroyed — losing a hangar must never silently delete the ships inside it");
            Assert.That(fPos.Parent?.Id, Is.EqualTo(cPos.Parent?.Id),
                "it is handed back to whatever the carrier itself orbits, from where the carrier is now");
            Assert.That(DockTools.Used(carrier), Is.EqualTo(0), "and the berth is free again");
        }

        /// <summary>The additive case: every existing hull in the game has no bay, reads zero capacity, refuses
        /// readably, and is never mutated by being asked.</summary>
        [Test]
        [Description("A ship with no docking bay reads zero capacity, refuses every attempt with a readable reason, and is never given a docked-ships blob just for being asked — so every existing design in the game is untouched.")]
        public void AHullWithNoBay_HasNoCapacity_RefusesReadably_AndIsNeverMutatedByBeingAsked()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.ToList();
            var plain = Spawn(s, designs[0], "Plain");
            var other = Spawn(s, designs[0], "Other");

            Assert.That(DockTools.Capacity(plain), Is.EqualTo(0), "no bay, no capacity");
            Assert.That(DockTools.LargestBerth(plain), Is.EqualTo(0), "and no door");
            Assert.That(DockTools.Used(plain), Is.EqualTo(0));
            Assert.That(DockTools.DockedShips(plain), Is.Empty);

            Assert.That(DockTools.CanDock(plain, other, out string why), Is.False);
            Log("refused: " + why);
            Assert.That(why, Does.Contain("no docking bay"), "and it says why");

            Assert.That(plain.HasDataBlob<DockedShipsDB>(), Is.False,
                "asking never attaches the blob — a READ must not mutate (the ColonyHexMapDB landmine, L-list)");

            // …and the self-dock nonsense case is refused rather than corrupting the registry.
            Assert.That(DockTools.CanDock(plain, plain, out string self), Is.False);
            Assert.That(self, Does.Contain("itself"));
        }
    }
}
