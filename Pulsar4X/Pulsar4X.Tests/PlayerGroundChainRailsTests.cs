using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Components;    // ComponentDesign, ComponentInstance
using Pulsar4X.Engine;        // Entity, Game
using Pulsar4X.Factions;      // FactionInfoDB, FactionFactory
using Pulsar4X.Galaxy;        // PlanetRegionsFactory, PlanetRegionsDB, SystemBodyInfoDB
using Pulsar4X.GroundCombat;
using Pulsar4X.Industry;      // IndustryJob
using Pulsar4X.Movement;      // PositionDB (repositioning the transport for the offensive landing)
using Pulsar4X.Ships;         // ShipFactory
using Pulsar4X.Storage;       // CargoStorageDB (the industry-completion hook)

namespace Pulsar4X.Tests
{
    /// <summary>
    /// OPERATION EARTHFALL — the PLAYER GROUND-CHAIN RAILS gauge (P8.1b). Proves the GENERIC player unit-creation →
    /// battalion → defend/invade RAILS are green + generic, so the developer's OWN live-designed Space Marine (a sealed +
    /// veteran-cadre + power-armor unit they build IN the game) rides working rails. Per developer decision #2 this fixture
    /// does NOT hard-code a Space Marine — it assembles a GENERIC ground unit from stock base-mod parts (a frame + a rifle
    /// + plating) exactly the way the Entity Assembler UI does (`GroundUnitAssembly.RegisterAssembledDesign`), builds and
    /// fields it through the REAL industry completion path, forms it into a battalion, sets stance/ROE, and runs the two
    /// scenario directions the developer described:
    ///
    ///   • DEFEND — a landed UMF invasion force attacks Earth and the player defense battalion defeats it; AND at the
    ///     moment of the counterattack the UMF brain READS the odds against the player's battalion IN FORCE and goes
    ///     Defensive / withdraws (the exact moment that answers the developer's "is the AI smart enough" question).
    ///   • INVADE — the units embark onto a transport (`LoadTroopsOrder`) and land offensively on another body
    ///     (`LandTroopsOrder`, region-index addressed per decision #5) — both directions, off the planet and onto another.
    ///
    /// Engine-only → CI (`rest` shard). Combat runs at SalvoScale 1.0 (as `GroundForcesTests.RegionCombat_StrongerGarrisonWins`),
    /// so the fight resolves in a bounded loop.
    /// </summary>
    [TestFixture]
    public class PlayerGroundChainRailsTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[player-rails] " + m);
        private const string TroopBay = "default-design-troop-bay";

        /// <summary>Assemble + register a GENERIC player ground unit from stock base-mod parts (frame + rifle + plating) —
        /// the exact single API a designer UI calls (mirrors GroundUnitFieldingTests). NOT a bespoke marine (decision #2).</summary>
        private static GroundUnitDesign RegisterGenericTrooper(FactionInfoDB faction, string id, string name)
        {
            ComponentDesign Part(string p) => (ComponentDesign)faction.IndustryDesigns[p];
            return GroundUnitAssembly.RegisterAssembledDesign(
                faction, id, name,
                Part("default-design-human-frame"),
                new List<(ComponentDesign, int)>
                {
                    (Part("default-design-ground-rifle"), 1),
                    (Part("default-design-ground-plating"), 1),
                });
        }

        // ───────────────────────── DEFEND — build → field → form up → defeat the invader → brain reacts ───────────────

        [Test]
        [Description("The GENERIC player chain, DEFEND direction: assemble a stock-parts unit in-engine (RegisterAssembledDesign), "
                   + "build+field a squad via the real industry completion path, FormUp into a battalion, set stance/ROE, then a "
                   + "landed UMF force attacks Earth: the player battalion defeats it AND the UMF brain reads the odds and goes "
                   + "Defensive/withdraws (the developer's counterattack moment). Generic rails — the live-designed marine rides them.")]
        public void PlayerChain_AssembleFieldFormUp_DefendsEarth_AndUmfBrainWithdraws()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();
            var body = s.StartingBody;
            int player = s.Faction.Id;

            // 1) ASSEMBLE + REGISTER a generic buildable design.
            var design = RegisterGenericTrooper(faction, "ef-player-trooper", "Player Trooper");
            Assert.That(faction.IndustryDesigns.ContainsKey(design.UniqueID), Is.True, "the assembled design is registered buildable on the faction");
            Assert.That(design.Attack, Is.GreaterThan(0), "the assembled unit has real firepower (emerged from the rifle)");

            // 2) BUILD + FIELD a squad through the REAL industry completion hook (the exact call IndustryTools makes at
            //    build-complete — GroundUnitFieldingTests idiom), so the fielded units ride the real production rails.
            var storage = s.Colony.GetDataBlob<CargoStorageDB>();
            const int squad = 6;
            for (int i = 0; i < squad; i++)
                design.OnConstructionComplete(s.Colony, storage, "line", new IndustryJob(faction, design.UniqueID), design);

            var forces = body.GetDataBlob<GroundForcesDB>();
            var fielded = forces.Units.Where(u => u.FactionOwnerID == player && u.DesignId == design.UniqueID).ToList();
            Assert.That(fielded.Count, Is.EqualTo(squad), "the completed builds fielded the whole squad on Earth");
            int homeRegion = fielded[0].RegionIndex;

            // 3) FORM UP into a battalion (the player's formation — same rails the AI uses).
            var formed = GroundAssembly.FormUpLoose(body, player, "1st Marines");
            Assert.That(formed.Count, Is.GreaterThanOrEqualTo(1), "the squad formed into a battalion");
            var battalion = formed[0];
            Assert.That(GroundFormationTools.MemberCount(forces, battalion), Is.EqualTo(squad), "every fielded unit joined the battalion");

            // 4) SET stance / ROE via the real levers (the human commands the battalion — a dug-in defense).
            GroundFormationDoctrine.SetEngagementStance(battalion, GroundEngagementStance.HoldGround);
            battalion.StanceFamily = GroundTactics.Defensive;
            Assert.That(battalion.Engagement, Is.EqualTo(GroundEngagementStance.HoldGround), "ROE set to Hold Ground");

            // 5) A landed UMF invasion force attacks Earth — a lone weakened lander in the defenders' region.
            const int umf = 850100;
            var umfDesign = new GroundUnitDesign
            { UniqueID = "ef-umf-lander", Name = "UMF Lander", UnitType = GroundUnitType.Infantry, Attack = 50, Defense = 5, HitPoints = 300 };
            var umfUnit = GroundForces.RaiseUnit(body, umfDesign, umf, homeRegion);

            // THE COUNTERATTACK MOMENT — the UMF brain reads the odds against the player battalion IN FORCE and reacts.
            double playerStrength = GroundFormationTools.FormationStrength(forces, battalion);
            Assert.That(playerStrength, Is.GreaterThanOrEqualTo(umfUnit.Attack * GroundTactics.RetreatLossRatio),
                "precondition: the player's counterattack is heavy enough to trip the brain's retreat bar");
            var umfPosture = GroundTactics.DecidePosture(new GroundTacticsContext
            {
                OwnStrength = umfUnit.Attack, EnemyStrength = playerStrength,
                RiskTrait = 0.5, AggressionTrait = 0.5, FortificationMult = 1.0, AmmoFraction = 1.0,
                ReserveIntact = true, HasFallback = true, FallbackRegion = 1,
            });
            Assert.That(umfPosture.StanceFamily, Is.EqualTo(GroundTactics.Defensive),
                "the UMF brain reads the heavy counterattack and drops to Defensive (not pressing)");
            Assert.That(umfPosture.Intent, Is.EqualTo(GroundIntent.Retreat), "and withdraws toward its beachhead");
            Assert.That(umfPosture.MoveTargetRegion, Is.EqualTo(1));
            Assert.That(umfPosture.Reason, Does.Contain("withdrawal"), "the AI-tape explains the withdrawal (the brain visibly alive)");
            Log($"UMF brain reacts: own {umfUnit.Attack:0} vs player {playerStrength:0} -> {umfPosture.StanceFamily}/{umfPosture.Intent}: {umfPosture.Reason}");

            // 6) THE FIGHT — the player defense battalion defeats the landed UMF force (bounded salvo loop).
            var proc = new GroundForcesProcessor();
            for (int i = 0; i < 60 && forces.Units.Any(u => u.FactionOwnerID == umf && u.Health > 0); i++)
                proc.ProcessEntity(body, 3600);

            Assert.That(forces.Units.Any(u => u.FactionOwnerID == umf && u.Health > 0), Is.False,
                "the player defense battalion defeated the landed UMF force");
            Assert.That(forces.Units.Any(u => u.FactionOwnerID == player && u.Health > 0), Is.True,
                "the player's marines still hold Earth");
            Log($"defended Earth: {forces.Units.Count(u => u.FactionOwnerID == player && u.Health > 0)} player unit(s) survived; UMF force cleared.");
        }

        // ───────────────────────── INVADE — embark then land offensively on another body (both directions) ────────────

        [Test]
        [Description("The GENERIC player chain, INVADE direction (the developer's scenario shape): a fielded generic unit "
                   + "EMBARKS onto a transport (LoadTroopsOrder), the transport sails to another body, and the unit LANDS "
                   + "offensively there (LandTroopsOrder, region-index addressed per decision #5) — both directions on generic "
                   + "units, so the rails are proven green for the live-designed marine.")]
        public void PlayerChain_EmbarksAndLandsOffensively_OnAnotherBody()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();
            var earth = s.StartingBody;
            var when = s.StartingSystem.StarSysDateTime;

            // A second regioned body to invade (the "Mars" of the developer's scenario), and a transport orbiting Earth.
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var target = s.StartingSystem.GetAllEntitiesWithDataBlob<PlanetRegionsDB>()
                .FirstOrDefault(b => b.Id != earth.Id && b.GetDataBlob<PlanetRegionsDB>().Regions.Count > 0);
            Assert.That(target, Is.Not.Null, "the start system has a second regioned body to invade");

            // Field a generic unit on Earth and give it a transport with a troop bay in Earth orbit.
            var design = RegisterGenericTrooper(faction, "ef-invade-trooper", "Assault Trooper");
            var unit = GroundForces.RaiseUnit(earth, design, s.Faction.Id, 0, "Assault Squad");
            var earthForces = earth.GetDataBlob<GroundForcesDB>();

            var shipDesign = faction.ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(shipDesign, s.Faction, earth, "Assault Lander");
            ship.AddComponent(new ComponentInstance((ComponentDesign)faction.IndustryDesigns[TroopBay]));
            Assert.That(GroundTransport.BayCapacity(ship, GroundCarryClass.Personnel), Is.GreaterThan(0), "the transport has a personnel bay");

            // EMBARK — off the planet, onto the ship (direction 1).
            var load = LoadTroopsOrder.CreateCommand(ship, earth, unit.UnitId);
            Assert.That(load.IsValidCommand(s.Game), Is.True, "the load order is valid");
            load.Execute(when);
            Assert.That(load.IsFinished(), Is.True, "the embark completed");
            Assert.That(ship.GetDataBlob<GroundTransportDB>().LoadedUnits, Does.Contain(unit), "the unit is aboard the transport");
            Assert.That(earthForces.Units, Does.Not.Contain(unit), "and off the Earth roster");

            // SAIL — reposition the transport to the target body's orbit (the crossing the invasion makes).
            ship.SetDataBlob(new PositionDB(target));
            Assert.That(GroundTransport.ShipIsAtBody(ship, target), Is.True, "the transport is now over the target body");
            Assert.That(GroundTransport.HasOrbitalControl(ship, target), Is.True, "orbit uncontested — only our ship present");

            // LAND — onto the target body's region (direction 2, region-index addressed per decision #5).
            var land = LandTroopsOrder.CreateCommand(ship, target, unit.UnitId, regionIndex: 0);
            land.Execute(when);
            Assert.That(land.IsFinished(), Is.True, "the offensive landing completed");
            Assert.That(ship.GetDataBlob<GroundTransportDB>().LoadedUnits, Does.Not.Contain(unit), "the unit left the ship");

            var targetForces = target.GetDataBlob<GroundForcesDB>();
            Assert.That(targetForces.Units.Any(u => u.FactionOwnerID == s.Faction.Id && u.Name == "Assault Squad"), Is.True,
                "the generic unit is on the ground on the invaded body");
            var landed = targetForces.Units.First(u => u.FactionOwnerID == s.Faction.Id && u.Name == "Assault Squad");
            Assert.That(landed.RegionIndex, Is.EqualTo(0), "at the landed region");
            Assert.That(landed.Health, Is.GreaterThan(0), "with its health intact across the lift");
            Log($"invaded body #{target.Id}: embark off Earth -> land at region 0 (both directions, generic unit).");
        }
    }
}
