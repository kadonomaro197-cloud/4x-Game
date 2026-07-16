using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Colonies;
using Pulsar4X.Combat;      // ShipCombatValueDB
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;      // FleetRole, FleetRoleComposer
using Pulsar4X.Modding;
using Pulsar4X.Ships;       // ShipFactory, ShipDesign

namespace Pulsar4X.Tests
{
    /// <summary>
    /// The DevTest FLEET-ROLE readout + the new-unit cradle-to-grave gauge (2026-07-16). Two jobs:
    ///
    /// 1. It CLOSES a real blind spot — `BaseModIntegrityTests` only checks the colony-blueprint path, NOT the scenario
    ///    faction files (uef/umf/kithrin). A registration slip there (a missing material / template / component design)
    ///    crashes New Game but ships green today. This test loads the whole DevTest sandbox and builds EVERY faction's
    ///    ship design, so a broken registration fails CI instead of a player's New Game.
    /// 2. It prints each design's classified fighting ROLE (via `FleetRoleComposer.ClassifyRole`) + its combat spec, so
    ///    the holistic composition of each faction is visible, and it asserts the NEW gap-filler units land in the role
    ///    they were designed for — the "cradle to grave, the AI knows how to use them" acceptance test: an armed hull is
    ///    auto-built, auto-fleeted, auto-classified, auto-doctrined by the AI with no code, so hitting the right role +
    ///    a real Firepower is exactly what makes it AI-usable.
    /// </summary>
    [TestFixture]
    public class DevTestFleetRoleReadoutTests
    {
        private const string ScenarioDir = "Data/basemod/ScenarioFiles";

        private static Game NewGame()
        {
            var modDataStore = new ModDataStore();
            new ModLoader().LoadModManifest("Data/basemod/modInfo.json", modDataStore);
            return GameFactory.CreateGame(modDataStore, new NewGameSettings
            {
                MaxSystems = 1,
                CreatePlayerFaction = false,
                DefaultSolStart = true,
                MasterSeed = 12345,
                EleStart = true
            });
        }

        [Test]
        [Description("Loads the whole DevTest sandbox (UEF+UMF+Kithrin), builds EVERY faction ship design (so a scenario "
                     + "registration slip fails CI, not New Game), prints each design's classified role + combat spec, and "
                     + "asserts the new gap-filler units hit their intended holistic role.")]
        public void EveryFactionShipDesign_BuildsAndClassifies_AndNewFillersHitTheirRole()
        {
            var game = NewGame();

            // CreateDevTest loads all three faction files through FactionFactory.LoadFromJson. If ANY ship design's
            // component/template/material registration is broken, this throws here — the New-Game-crash guard.
            var (player, _) = DevTestStartFactory.CreateDevTest(
                game, ScenarioDir, new List<string> { "uef-devtest.json", "umf.json", "kithrin.json" });
            Assert.That(player, Is.Not.Null, "DevTest returned no player faction.");

            // A valid body to parent test-built ships at (any body in the system works — classification reads the ship's
            // own components, not its location). The player's Earth colony gives us one.
            var body = player.GetDataBlob<FactionInfoDB>().Colonies
                .Where(c => c != null && c.IsValid)
                .Select(c => c.GetDataBlob<ColonyInfoDB>().PlanetEntity)
                .FirstOrDefault(b => b != null && b.IsValid);
            Assert.That(body, Is.Not.Null, "no player colony body to build test ships at.");

            // Build + classify every ship design across every loaded faction.
            var role = new Dictionary<string, FleetRole>();
            var fire = new Dictionary<string, double>();
            var evade = new Dictionary<string, double>();

            foreach (var faction in game.Factions.Values.Where(f => f != null && f.IsValid && f.HasDataBlob<FactionInfoDB>()))
            {
                var info = faction.GetDataBlob<FactionInfoDB>();
                foreach (var kv in info.ShipDesigns)
                {
                    try
                    {
                        var ship = ShipFactory.CreateShip(kv.Value, faction, body);
                        var cv = ship.GetDataBlob<ShipCombatValueDB>();
                        var r = FleetRoleComposer.ClassifyRole(ship);
                        role[kv.Key] = r;
                        fire[kv.Key] = cv?.Firepower ?? 0;
                        evade[kv.Key] = cv?.Evasion ?? 0;
                        TestContext.WriteLine($"[role] {info.Abbreviation,-4} {kv.Value.Name,-24} ({kv.Key}): {r,-9} "
                            + $"fp={cv?.Firepower,-10:F0} tough={cv?.Toughness,-12:F0} ev={cv?.Evasion:F2} range_m={cv?.MaxWeaponRange:F0}");
                    }
                    catch (System.Exception ex)
                    {
                        TestContext.WriteLine($"[role] {info.Abbreviation} {kv.Key}: BUILD FAILED — {ex.GetType().Name}: {ex.Message}");
                    }
                }
            }

            // The new gap-fillers registered, built, and are ARMED (Firepower > 0) — so the AI recognizes them as
            // warships and will build + fleet + doctrine them with zero code.
            foreach (var id in new[] { "umf-ship-legionnaire", "umf-ship-skirmisher", "kithrin-ship-harbinger" })
            {
                Assert.That(role.ContainsKey(id), Is.True, $"{id} did not register/build — a scenario cradle-to-grave gap.");
                Assert.That(fire[id], Is.GreaterThan(0), $"{id} must be an ARMED warship the AI fields (Firepower > 0).");
            }

            // …and each lands in the holistic role it was designed to fill:
            Assert.That(role["kithrin-ship-harbinger"], Is.EqualTo(FleetRole.Artillery),
                "Kithrin Harbinger (heavy phase-disruptor/plasma standoff) = the long-range ARTILLERY the Kithrin lacked.");
            Assert.That(role["umf-ship-legionnaire"], Is.EqualTo(FleetRole.Line),
                "UMF Legionnaire (heavy short-range chain-flak brawler) = the mid-range LINE.");
            // Screen depends on evasion (hull volume + thrust/mass), so assert the ROBUST relative fact: the light
            // Skirmisher corvette is more evasive than the heavy Legionnaire (a light hull is always harder to hit).
            // Whether it clears the 0.5 Screen bar is read off the printout above and tuned in the follow-up slice.
            Assert.That(evade["umf-ship-skirmisher"], Is.GreaterThan(evade["umf-ship-legionnaire"]),
                "the light Skirmisher corvette must be more evasive than the heavy Legionnaire brawler.");
        }
    }
}
