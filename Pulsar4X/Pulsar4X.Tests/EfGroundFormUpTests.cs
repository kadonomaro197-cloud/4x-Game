using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Components;
using Pulsar4X.Galaxy;      // PlanetRegionsDB, SystemBodyInfoDB
using Pulsar4X.Ships;       // ShipFactory (the transport harness)
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Operation Earthfall G2.1 — FORM-UP (AI formation parity, R2 gap 3: "the AI NEVER forms"). Gauges the new
    /// <see cref="GroundAssembly.FormUpLoose"/> (sweep loose units → battalions, per the faction's authored garrison
    /// size, deterministic, multiple battalions over cap), its two wired sites (garrison RAISE + invasion LANDING,
    /// gated OFF by default → byte-identical), and the manager micro-helpers the client needs
    /// (<see cref="GroundForces.RenameFormation"/>, <see cref="GroundFormationTools.AllFormationsFor"/>,
    /// <see cref="GroundSensors.RadarReachHexes"/>). Engine-only → runs in CI.
    /// </summary>
    [TestFixture]
    public class EfGroundFormUpTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[formup] " + m);

        private static GroundUnitDesign Infantry() => new GroundUnitDesign
        {
            UniqueID = "efg-infantry", Name = "Infantry", UnitType = GroundUnitType.Infantry,
            Attack = 100, Defense = 10, HitPoints = 500,
        };

        private const string TroopBay = "default-design-troop-bay";

        /// <summary>A ship orbiting the body, owned by <paramref name="factionId"/>, with a troop bay installed —
        /// the exact idiom from <c>GroundTransportTests.TransportAt</c>.</summary>
        private static Entity TransportAt(TestScenario s, Entity body, int factionId)
        {
            var shipDesign = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(shipDesign, s.Faction, body, "Trooper");
            ship.FactionOwnerID = factionId;
            var bayDesign = (ComponentDesign)s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns[TroopBay];
            ship.AddComponent(new ComponentInstance(bayDesign));
            return ship;
        }

        // ───────────────────────── FormUpLoose ─────────────────────────

        [Test]
        [Description("G2.1: FormUpLoose sweeps a raised HOME GARRISON into ONE battalion (cap = the faction's authored garrison size), every unit joins it with a leader set, and a second call is a no-op (no loose units left).")]
        public void FormUpLoose_SweepsRaisedGarrison_IntoOneBattalion()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;

            int expected = GroundStartGarrison.Composition.Sum(c => c.count);   // engine default = 6
            int raised = GroundStartGarrison.RaiseForFactionColonies(s.Game, s.Faction);
            Assert.That(raised, Is.EqualTo(expected), "the whole garrison composition is raised");

            var forces = body.GetDataBlob<GroundForcesDB>();
            var formed = GroundAssembly.FormUpLoose(body, s.Faction.Id);

            Assert.That(formed.Count, Is.EqualTo(1), "the garrison forms into one battalion (fits under the cap)");
            Assert.That(GroundFormationTools.MemberCount(forces, formed[0]), Is.EqualTo(expected),
                "every garrison unit joined the battalion");
            Assert.That(forces.Units.Where(u => u.FactionOwnerID == s.Faction.Id).All(u => u.FormationId == formed[0].FormationId),
                Is.True, "no unit left loose");
            Assert.That(formed[0].LeaderUnitId, Is.GreaterThanOrEqualTo(0), "the battalion has a leader (first unit in = flagship)");

            var again = GroundAssembly.FormUpLoose(body, s.Faction.Id);
            Assert.That(again.Count, Is.EqualTo(0), "nothing loose left → a second sweep forms nothing");
            Log($"garrison of {expected} formed into {formed.Count} battalion(s)");
        }

        [Test]
        [Description("G2.1: over the size cap, FormUpLoose splits the loose units into MULTIPLE numbered battalions in deterministic UnitId order (13 units, cap 6 → 6+6+1), and every unit ends formed.")]
        public void FormUpLoose_OverCap_SplitsIntoNumberedBattalions()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            Assert.That(s.Faction.GetDataBlob<FactionInfoDB>().GarrisonComposition.Count, Is.EqualTo(0),
                "the harness faction authors no garrison mix → the cap is the engine default");

            int cap = GroundStartGarrison.Composition.Sum(c => c.count);   // 6
            int n = cap * 2 + 1;                                           // 13
            for (int i = 0; i < n; i++)
                GroundForces.RaiseUnit(body, Infantry(), s.Faction.Id, 0);

            var forces = body.GetDataBlob<GroundForcesDB>();
            var formed = GroundAssembly.FormUpLoose(body, s.Faction.Id, "1st Legion");

            Assert.That(formed.Count, Is.EqualTo(3), "13 loose / cap 6 → 3 battalions");
            Assert.That(GroundFormationTools.MemberCount(forces, formed[0]), Is.EqualTo(cap));
            Assert.That(GroundFormationTools.MemberCount(forces, formed[1]), Is.EqualTo(cap));
            Assert.That(GroundFormationTools.MemberCount(forces, formed[2]), Is.EqualTo(1));
            Assert.That(formed[0].Name, Is.EqualTo("1st Legion 1"), "multiple battalions are numbered off the base name");
            Assert.That(formed[2].Name, Is.EqualTo("1st Legion 3"));
            Assert.That(forces.Units.Where(u => u.FactionOwnerID == s.Faction.Id).All(u => u.FormationId >= 1),
                Is.True, "every unit ended in a battalion");
            Log($"{n} units, cap {cap} → {formed.Count} battalions ({string.Join("/", formed.Select(f => GroundFormationTools.MemberCount(forces, f)))})");
        }

        [Test]
        [Description("G2.1 byte-identity: the LANDING form-up is gated — with GroundAssembly.AutoFormUp OFF a landed invader stays LOOSE (byte-identical); with it ON the invader is swept into a battalion (the AI fields formations).")]
        public void Landing_FormsTheInvaderUp_OnlyWhenAutoFormUpOn()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;

            bool prev = GroundAssembly.AutoFormUp;
            try
            {
                // OFF (default) — land → still loose
                GroundAssembly.AutoFormUp = false;
                var u1 = GroundForces.RaiseUnit(body, Infantry(), s.Faction.Id, 0);
                var forces = body.GetDataBlob<GroundForcesDB>();
                var ship1 = TransportAt(s, body, s.Faction.Id);
                Assert.That(GroundTransport.TryLoadUnit(ship1, body, u1), Is.True, "loads");
                Assert.That(GroundTransport.TryLandUnit(ship1, body, u1, 1), Is.True, "lands");
                Assert.That(u1.FormationId, Is.LessThan(0), "flag OFF → the landed invader stays loose (byte-identical)");

                // ON — land → formed
                GroundAssembly.AutoFormUp = true;
                var u2 = GroundForces.RaiseUnit(body, Infantry(), s.Faction.Id, 2);
                var ship2 = TransportAt(s, body, s.Faction.Id);
                Assert.That(GroundTransport.TryLoadUnit(ship2, body, u2), Is.True, "loads");
                Assert.That(GroundTransport.TryLandUnit(ship2, body, u2, 3), Is.True, "lands");
                Assert.That(u2.FormationId, Is.GreaterThanOrEqualTo(1), "flag ON → the landed invader is formed up");

                var formation = forces.Formations.First(f => f.FormationId == u2.FormationId);
                Assert.That(GroundFormationTools.MembersOf(forces, formation), Does.Contain(u2),
                    "the invader is a member of the battalion it was swept into");
                Log($"landing form-up: OFF→loose, ON→battalion {u2.FormationId}");
            }
            finally { GroundAssembly.AutoFormUp = prev; }
        }

        // ───────────────────────── manager micro-helpers ─────────────────────────

        [Test]
        [Description("G2.1: RenameFormation sets a formation's player-facing name (the DATA-object rename the client needs — R1 gap 2) and refuses a null formation / a blank name, keeping the old name.")]
        public void RenameFormation_SetsTheName_RejectsBlankAndNull()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var f = GroundForces.CreateFormation(body, s.Faction.Id, "Old Name");

            Assert.That(GroundForces.RenameFormation(f, "1st Armoured"), Is.True);
            Assert.That(f.Name, Is.EqualTo("1st Armoured"));
            Assert.That(GroundForces.RenameFormation(f, "   "), Is.False, "blank name refused");
            Assert.That(GroundForces.RenameFormation(null, "x"), Is.False, "null formation refused");
            Assert.That(f.Name, Is.EqualTo("1st Armoured"), "the name is unchanged after a rejected rename");
        }

        [Test]
        [Description("G2.1: AllFormationsFor enumerates a faction's formations across EVERY body in the game (the cross-body registry the Force window needs — R1 gap 1), each paired with its body, and filters out other factions.")]
        public void AllFormationsFor_EnumeratesAcrossBodies_FactionFiltered()
        {
            var s = TestScenario.CreateWithColony();
            int before = GroundFormationTools.AllFormationsFor(s.Game, s.Faction.Id).Count;

            var bodies = s.StartingSystem.GetAllEntitiesWithDataBlob<SystemBodyInfoDB>();
            var otherBody = bodies.First(b => b.Id != s.StartingBody.Id);

            GroundForces.CreateFormation(s.StartingBody, s.Faction.Id, "Alpha");
            GroundForces.CreateFormation(otherBody, s.Faction.Id, "Bravo");
            GroundForces.CreateFormation(s.StartingBody, s.Faction.Id + 4242, "Enemy");   // a rival's — must be excluded

            var mine = GroundFormationTools.AllFormationsFor(s.Game, s.Faction.Id);
            Assert.That(mine.Count, Is.EqualTo(before + 2), "both my formations enumerated; the rival's excluded");
            Assert.That(mine.Select(p => p.body.Id).Distinct().Count(), Is.EqualTo(2), "across two distinct bodies");
            Assert.That(mine.Any(p => p.formation.Name == "Alpha"), Is.True);
            Assert.That(mine.Any(p => p.formation.Name == "Bravo"), Is.True);
            Assert.That(mine.All(p => p.formation.FactionOwnerID == s.Faction.Id), Is.True, "faction-filtered");
        }

        [Test]
        [Description("G2.1: RadarReachHexes reads a unit's best mounted radar range translated to hexes on this body (Range_km / hex pitch); a unit with no radar — and a null body/unit — reads 0.")]
        public void RadarReachHexes_ReadsBestRadar_ZeroWithoutOne()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();
            var body = s.StartingBody;
            ComponentDesign Part(string id) => (ComponentDesign)faction.IndustryDesigns[id];

            // A radar component + a unit that carries it (the GroundSensorsTests idiom — the ability rides the component).
            var radar = new ComponentDesign { UniqueID = "efg-radar", Name = "Radar" };
            radar.AttributesByType[typeof(GroundSensorAtb)] = new GroundSensorAtb(500_000);   // 500,000 km reach
            faction.IndustryDesigns["efg-radar"] = radar;
            var scoutDesign = GroundUnitAssembly.RegisterAssembledDesign(
                faction, "efg-radar-scout", "Radar Scout",
                Part("default-design-human-frame"),
                new List<(ComponentDesign, int)> { (radar, 1) });
            var scout = GroundForces.RaiseUnit(body, scoutDesign, s.Faction.Id, 0);
            Assert.That(scout.BackingEntityId, Is.GreaterThanOrEqualTo(0), "the radar scout has a component-backing entity");

            var regions = body.GetDataBlob<PlanetRegionsDB>();
            double pitch = GroundRangeTools.HexPitchKm(regions.Regions[scout.RegionIndex]);
            Assert.That(pitch, Is.GreaterThan(0), "the starting body's region has real hex geometry");

            double reach = GroundSensors.RadarReachHexes(body, scout);
            Assert.That(reach, Is.EqualTo(500_000.0 / pitch).Within(1e-6), "reach = radar km / hex pitch km");
            Log($"radar 500,000 km / pitch {pitch:0.0} km = {reach:0.0} hexes");

            // No radar → 0; null guards → 0.
            var plain = GroundForces.RaiseUnit(body, Infantry(), s.Faction.Id, 0);
            Assert.That(GroundSensors.RadarReachHexes(body, plain), Is.EqualTo(0), "no radar mounted → 0");
            Assert.That(GroundSensors.RadarReachHexes(body, null), Is.EqualTo(0));
            Assert.That(GroundSensors.RadarReachHexes(null, scout), Is.EqualTo(0));
        }
    }
}
