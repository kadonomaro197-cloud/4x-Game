using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Components;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// The missing "designs survive Save -> Load" sensor.
    ///
    /// A faction's designed things — its component designs (a laser, a reactor, a mine), its ship classes,
    /// its industry designs — are stored on the faction, not in the star system. <see cref="SaveLoadSmokeTests"/>
    /// only proves a colony-LESS universe round-trips without throwing, and <see cref="SaveLoadWithJobTests"/>
    /// only proves a queued production job round-trips without throwing. NEITHER checks that the DESIGNS
    /// themselves are still intact after a load. If a later engine change silently breaks design serialization —
    /// e.g. someone renames or drops a *Atb (a component design attribute type, the piece TypeNameHandling
    /// embeds by C# type name in the save file) — a player's save would load "successfully" but come back with
    /// missing or hollowed-out designs. That is a save-corrupting disaster no existing test would catch.
    ///
    /// This test builds a real faction+colony the way the live game does, saves, loads, finds the SAME faction
    /// in the reloaded game, and asserts its component designs came back with the same UniqueIDs, the same Names,
    /// and — the real sensor — the same set of *Atb attribute types per design. It asserts only STABLE identity
    /// fields (ids/names/counts/attribute-type keys), never derived stats, so it fails on a genuine
    /// serialization regression rather than on incidental recompute noise.
    ///
    /// Enabled (not [Ignore]'d): the full-colony Game.Save -> Game.Load path is already proven to round-trip by
    /// <see cref="SaveLoadWithJobTests"/> (same CreateWithColony harness, CI-green, no [Ignore]).
    /// </summary>
    [TestFixture]
    public class SaveLoadDesignRoundTripTests
    {
        [Test]
        [Description("A faction's component/ship designs must survive Game.Save -> Game.Load: same UniqueIDs, Names, and per-design *Atb attribute-type set.")]
        public void FactionDesigns_SurviveSaveLoadRoundTrip()
        {
            // 1) Build a real start (faction + colony WITH designs), the way the live game does.
            var s = TestScenario.CreateWithColony();
            var beforeFaction = s.Faction.GetDataBlob<FactionInfoDB>();

            // We locate the faction again after reload by its short code (Abbreviation lives on FactionInfoDB
            // and is set by FactionFactory.CreateBasicFaction to "TST" in the harness).
            string factionAbbr = beforeFaction.Abbreviation;
            Assert.That(factionAbbr, Is.Not.Null.And.Not.Empty,
                "Test faction has no Abbreviation to match on after reload.");

            // 2) Capture BEFORE. Key everything by the design's dictionary key (a stable string). For each design,
            //    record its UniqueID, its Name, and the SET of attribute-type full names — the *Atb C# types that
            //    TypeNameHandling embeds in the save. A serialization break is exactly a design coming back with a
            //    different (or empty) attribute-type set.
            var beforeComponents = new Dictionary<string, (string uniqueId, string name, HashSet<string> attrTypes)>();
            foreach (var kvp in beforeFaction.ComponentDesigns)
            {
                var design = kvp.Value;
                var attrTypes = design.AttributesByType.Keys.Select(t => t.FullName).ToHashSet();
                beforeComponents[kvp.Key] = (design.UniqueID, design.Name, attrTypes);
            }
            int beforeComponentCount = beforeComponents.Count;
            Assert.That(beforeComponentCount, Is.GreaterThanOrEqualTo(1),
                "The start colony faction should carry at least one ComponentDesign before save.");

            // Ship designs may legitimately be 0 on this start — capture the count but don't hard-require ships.
            int beforeShipCount = beforeFaction.ShipDesigns.Count;

            // 3) Save.
            string json = null;
            Assert.DoesNotThrow(() => json = Game.Save(s.Game), "Game.Save threw on a full-colony game.");
            Assert.That(json, Is.Not.Null.And.Not.Empty, "Game.Save produced no JSON.");

            // 4) Load.
            Game reloaded = null;
            Assert.DoesNotThrow(() => reloaded = Game.Load(json),
                "Game.Load threw on the JSON that Game.Save just produced.");
            Assert.That(reloaded, Is.Not.Null, "Game.Load returned null.");

            // 5) Find the SAME faction in the reloaded game (match by Abbreviation off its reloaded FactionInfoDB).
            FactionInfoDB afterFaction = null;
            foreach (var factionEntity in reloaded.Factions.Values)
            {
                if (!factionEntity.HasDataBlob<FactionInfoDB>())
                    continue;
                var info = factionEntity.GetDataBlob<FactionInfoDB>();
                if (string.Equals(info.Abbreviation, factionAbbr))
                {
                    afterFaction = info;
                    break;
                }
            }
            Assert.That(afterFaction, Is.Not.Null,
                $"Reloaded game has no faction with Abbreviation '{factionAbbr}'.");

            // 6) The component-design store must come back as the same identity set.
            //    (a) same count + same key set.
            Assert.That(afterFaction.ComponentDesigns.Count, Is.EqualTo(beforeComponentCount),
                "ComponentDesigns count changed across Save/Load.");
            var afterKeys = afterFaction.ComponentDesigns.Keys.ToHashSet();
            Assert.That(afterKeys.SetEquals(beforeComponents.Keys), Is.True,
                "The set of ComponentDesign keys changed across Save/Load.");

            //    (b) same UniqueID + Name, and — the real sensor — the same *Atb attribute-type set per design.
            foreach (var kvp in beforeComponents)
            {
                string key = kvp.Key;
                var before = kvp.Value;

                Assert.That(afterFaction.ComponentDesigns.ContainsKey(key), Is.True,
                    $"Design '{key}' is missing after reload.");
                var after = afterFaction.ComponentDesigns[key];

                Assert.That(after.UniqueID, Is.EqualTo(before.uniqueId),
                    $"UniqueID changed for design '{key}' across Save/Load.");
                Assert.That(after.Name, Is.EqualTo(before.name),
                    $"Name changed for design '{key}' across Save/Load.");

                var afterAttrTypes = after.AttributesByType.Keys.Select(t => t.FullName).ToHashSet();
                Assert.That(afterAttrTypes.SetEquals(before.attrTypes), Is.True,
                    $"The *Atb attribute-type set changed for design '{key}' across Save/Load " +
                    $"(before: [{string.Join(", ", before.attrTypes.OrderBy(x => x))}], " +
                    $"after: [{string.Join(", ", afterAttrTypes.OrderBy(x => x))}]). " +
                    "That is a dropped or renamed *Atb — a save-corrupting serialization regression.");
            }

            // 7) Ship designs: softer (the start may carry zero), but if any existed the same count must survive.
            Assert.That(afterFaction.ShipDesigns.Count, Is.EqualTo(beforeShipCount),
                "ShipDesigns count changed across Save/Load.");
        }
    }
}
