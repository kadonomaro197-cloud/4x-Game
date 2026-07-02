using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Pulsar4X.Colonies;
using Pulsar4X.Factions;
using Pulsar4X.Galaxy;
using Pulsar4X.Names;
using Pulsar4X.People;

namespace Pulsar4X.Engine
{
    /// <summary>The staged game-states the test rig / DevTools can jump a game to.</summary>
    public enum GameStage { NewGame = 0, Early = 1, Mid = 2, Late = 3 }

    /// <summary>
    /// "Age the galaxy" — layer a running game up to a later stage so the LATE-triggering systems (multi-colony
    /// economy, diplomacy, war, rebellion) can be SEEN without playing for hours. This is task #39, the test rig
    /// for the whole political cluster: a fresh New Game shows none of it (no rivals, no war, nothing collapsing),
    /// so today the only way to exercise morale→legitimacy→rebellion or the diplomacy readout is a unit test —
    /// you can't watch it. A staged state fixes that, at a real fidelity: the galaxy at a stage is a proper set
    /// of empires — the player with SEVERAL colonies, rival empires that each have their OWN species + colonies,
    /// a web of relations, treaties, a war, and a rebellion.
    ///
    /// GENERATED, not a save file — on purpose. Our save format churns every session (`TypeNameHandling` bakes
    /// C# type names into the JSON, so every DataBlob we add/rename breaks old saves). This rides the CURRENT
    /// factories (ColonyFactory / FactionFactory / SpeciesFactory / Diplomacy), so it never rots and runs in CI.
    /// It lives in the ENGINE so BOTH the CI tests and the DevTools "Age the galaxy" button call it.
    ///
    /// Transforms are CUMULATIVE (Late applies Early+Mid+Late) and CONVERGENT (each ensures a target — colony
    /// counts, rival standing — and only adds/adjusts the shortfall), so it's safe to call repeatedly: the
    /// DevTools button can step Early→Mid→Late and a rival grows from 1 colony to 2, neutral to allied, etc.
    /// Defensive: every step is guarded and the whole thing never throws (both callers run it live). It builds a
    /// SNAPSHOT (no clock advance) — fresh colonies read neutral morale until the sim runs; advance time to let
    /// morale/legitimacy compute (the rebellion is forced into the collapse band so it shows immediately).
    ///
    /// NOTE — this is the opposite of the secession blast radius: creating a colony a rival owns FROM BIRTH is a
    /// clean `ColonyFactory` call; only TRANSFERRING an existing colony's owner is the hard problem. So rivals
    /// here are real empires with their own colonies.
    /// </summary>
    public static class GameStageFactory
    {
        public static string AgeTo(Game game, Entity playerFaction, GameStage stage)
        {
            var log = new StringBuilder($"Age galaxy → {stage}: ");
            if (game == null || playerFaction == null || !playerFaction.TryGetDataBlob<FactionInfoDB>(out _))
                return log.Append("[no game/player faction]").ToString();

            try
            {
                if (stage >= GameStage.Early) BuildStage(game, playerFaction, GameStage.Early, log);
                if (stage >= GameStage.Mid) BuildStage(game, playerFaction, GameStage.Mid, log);
                if (stage >= GameStage.Late) BuildStage(game, playerFaction, GameStage.Late, log);
            }
            catch (Exception ex)
            {
                log.Append($"[error: {ex.Message}]");
            }
            return log.ToString();
        }

        private static void BuildStage(Game game, Entity player, GameStage stage, StringBuilder log)
        {
            var when = game.TimePulse.GameGlobalDateTime;
            switch (stage)
            {
                case GameStage.Early: // expanding + first contact
                    EnsurePlayerColonies(game, player, target: 3, pop: 50_000_000, log);
                    EnsureRival(game, player, "Vega Combine", "VEG", score: +30, colonies: 1, pop: 40_000_000, treaty: null, when, log);
                    break;

                case GameStage.Mid: // established, multipolar
                    EnsurePlayerColonies(game, player, target: 4, pop: 200_000_000, log);
                    EnsureRival(game, player, "Vega Combine", "VEG", score: +45, colonies: 2, pop: 150_000_000, treaty: TreatyType.TradeAgreement, when, log);
                    EnsureRival(game, player, "Crimson Hegemony", "CRH", score: -40, colonies: 2, pop: 150_000_000, treaty: null, when, log);
                    break;

                case GameStage.Late: // mature — alliances matter, a war is on, a colony revolts
                    EnsurePlayerColonies(game, player, target: 5, pop: 800_000_000, log);
                    EnsureRival(game, player, "Terran League", "TRL", score: +65, colonies: 1, pop: 300_000_000, treaty: TreatyType.DefensivePact, when, log);
                    DeclareWarOnHostile(game, player, when, log);
                    RebelAColony(player, when, log);
                    break;
            }
        }

        // --- player colonies -------------------------------------------------

        private static void EnsurePlayerColonies(Game game, Entity player, int target, long pop, StringBuilder log)
        {
            var fi = player.GetDataBlob<FactionInfoDB>();
            var species = fi.Species.FirstOrDefault();
            var home = fi.Colonies.FirstOrDefault();
            if (species == null || home == null) { log.Append("[no species/home] "); return; }

            var homeSystem = SystemOf(home);
            var order = SystemsPreferring(game, homeSystem, preferHome: true);

            while (fi.Colonies.Count < target)
            {
                var body = NextSpareBody(game, order);
                if (body == null) break;
                ColonyFactory.CreateColony(player, species, body, pop);
            }

            // Grow existing frontier colonies to this stage's population floor (the homeworld stays as-is).
            foreach (var c in fi.Colonies)
            {
                if (c.Id == home.Id) continue;
                GrowColony(c, species.Id, pop);
            }
            log.Append($"[player colonies: {fi.Colonies.Count}] ");
        }

        private static void GrowColony(Entity colony, int speciesId, long floor)
        {
            if (!colony.TryGetDataBlob<ColonyInfoDB>(out var ci)) return;
            if (!ci.Population.TryGetValue(speciesId, out var cur) || cur < floor)
                ci.Population[speciesId] = floor;
        }

        // --- rival empires ---------------------------------------------------

        private static void EnsureRival(Game game, Entity player, string name, string abbr,
            int score, int colonies, long pop, TreatyType? treaty, DateTime when, StringBuilder log)
        {
            var rival = FindFactionByName(game, name)
                        ?? FactionFactory.CreateBasicFaction(game, name, abbr, 50_000_000);
            if (!rival.TryGetDataBlob<FactionInfoDB>(out var rfi)) return;

            // Rival species (created once, in any loaded system — colony refs it globally by id).
            var rivalSpecies = rfi.Species.FirstOrDefault();
            if (rivalSpecies == null)
            {
                var speciesBp = game.StartingGameData?.Species.Values.FirstOrDefault();
                var sysForSpecies = SystemOf(player.GetDataBlob<FactionInfoDB>().Colonies.FirstOrDefault())
                                    ?? game.Systems.FirstOrDefault();
                if (speciesBp != null && sysForSpecies != null)
                {
                    rivalSpecies = SpeciesFactory.CreateFromBlueprint(sysForSpecies, speciesBp);
                    rivalSpecies.FactionOwnerID = rival.Id;
                    rfi.Species.Add(rivalSpecies);
                }
            }

            // Rival colonies — prefer systems OTHER than the player's home (a rival empire lives elsewhere;
            // falls back to the home system if that's all that's loaded, e.g. a Sol-only test).
            if (rivalSpecies != null)
            {
                var homeSystem = SystemOf(player.GetDataBlob<FactionInfoDB>().Colonies.FirstOrDefault());
                var order = SystemsPreferring(game, homeSystem, preferHome: false);
                while (rfi.Colonies.Count < colonies)
                {
                    var body = NextSpareBody(game, order);
                    if (body == null) break;
                    ColonyFactory.CreateColony(rival, rivalSpecies, body, pop);
                }
            }

            // Relations — converge BOTH sides' view to the target score (a treaty can warm it further).
            SetRelation(player, rival, score, when);
            SetRelation(rival, player, score, when);
            if (treaty.HasValue)
                Treaties.Propose(rival, player, treaty.Value, when);

            log.Append($"[{name}: {rfi.Colonies.Count} colonies, rel {score:+0;-0}{(treaty.HasValue ? " +treaty" : "")}] ");
        }

        private static void SetRelation(Entity from, Entity to, int targetScore, DateTime when)
        {
            if (!from.TryGetDataBlob<DiplomacyDB>(out var dip)) return;
            var rel = dip.GetOrCreateRelationship(to.Id);
            rel.AdjustScore(targetScore - rel.RelationScore); // converge to target (idempotent on re-age)
            rel.LastContact = when;
        }

        // --- war + rebellion -------------------------------------------------

        private static void DeclareWarOnHostile(Game game, Entity player, DateTime when, StringBuilder log)
        {
            if (!player.TryGetDataBlob<DiplomacyDB>(out var dip) || dip.IsAtWarWithAnyone()) return;
            var enemyRel = dip.Relationships.Values.Where(r => !r.AtWar)
                .OrderBy(r => r.RelationScore).FirstOrDefault();
            if (enemyRel != null && game.Factions.TryGetValue(enemyRel.OtherFactionId, out var enemy))
            {
                Diplomacy.DeclareWar(player, enemy, CasusBelli.ConfrontRival, when);
                log.Append("[WAR declared] ");
            }
        }

        private static void RebelAColony(Entity player, DateTime when, StringBuilder log)
        {
            var fi = player.GetDataBlob<FactionInfoDB>();
            var home = fi.Colonies.FirstOrDefault();
            var frontier = fi.Colonies.FirstOrDefault(c => c.Id != (home?.Id ?? -1)
                && c.TryGetDataBlob<RebellionDB>(out var r) && !r.IsRebelling);
            if (frontier == null) return;

            if (frontier.TryGetDataBlob<ColonyMoraleDB>(out var mor)) mor.Morale = 10.0; // a miserable frontier
            if (frontier.TryGetDataBlob<LegitimacyDB>(out var leg) && frontier.TryGetDataBlob<RebellionDB>(out var reb))
            {
                leg.Legitimacy = 10.0; // below CollapseThreshold (20)
                LegitimacyProcessor.UpdateRebellion(reb, leg.Legitimacy, when);
                log.Append("[frontier REBELLING] ");
            }
        }

        // --- helpers ---------------------------------------------------------

        private static StarSystem SystemOf(Entity colony)
            => colony != null && colony.TryGetDataBlob<ColonyInfoDB>(out var ci) && ci.PlanetEntity != null
                ? ci.PlanetEntity.Manager as StarSystem
                : null;

        /// <summary>Loaded systems in priority order — home first (for the player) or home last (for rivals).</summary>
        private static List<StarSystem> SystemsPreferring(Game game, StarSystem home, bool preferHome)
        {
            var others = game.Systems.Where(s => s != home).ToList();
            var ordered = new List<StarSystem>();
            if (preferHome && home != null) ordered.Add(home);
            ordered.AddRange(others);
            if (!preferHome && home != null) ordered.Add(home); // fall back to home only if nothing else has room
            return ordered;
        }

        /// <summary>The set of bodies already carrying a colony (any faction), so no two colonies share a body.</summary>
        private static HashSet<int> ColonizedBodyIds(Game game)
        {
            var set = new HashSet<int>();
            foreach (var faction in game.Factions.Values)
                if (faction.TryGetDataBlob<FactionInfoDB>(out var fi))
                    foreach (var c in fi.Colonies)
                        if (c.TryGetDataBlob<ColonyInfoDB>(out var ci) && ci.PlanetEntity != null)
                            set.Add(ci.PlanetEntity.Id);
            return set;
        }

        /// <summary>First colonizable body across the ordered systems that isn't already colonized.</summary>
        private static Entity NextSpareBody(Game game, List<StarSystem> systemsInOrder)
        {
            var colonized = ColonizedBodyIds(game);
            foreach (var system in systemsInOrder)
            {
                foreach (var bodyDB in system.GetAllDataBlobsOfType<SystemBodyInfoDB>())
                {
                    var body = bodyDB.OwningEntity;
                    if (body == null || colonized.Contains(body.Id)) continue;
                    if (!body.HasDataBlob<NameDB>() || !body.HasDataBlob<MassVolumeDB>()) continue;
                    return body;
                }
            }
            return null;
        }

        private static Entity FindFactionByName(Game game, string name)
        {
            foreach (var faction in game.Factions.Values)
                if (faction.TryGetDataBlob<NameDB>(out var nameDB) && nameDB.DefaultName == name)
                    return faction;
            return null;
        }
    }
}
