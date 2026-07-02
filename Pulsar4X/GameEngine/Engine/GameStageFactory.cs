using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Pulsar4X.Colonies;
using Pulsar4X.Factions;
using Pulsar4X.Galaxy;
using Pulsar4X.Names;

namespace Pulsar4X.Engine
{
    /// <summary>The staged game-states the test rig / DevTools can jump a game to.</summary>
    public enum GameStage { NewGame = 0, Early = 1, Mid = 2, Late = 3 }

    /// <summary>
    /// "Age the galaxy" — layer a running game up to a later stage so the LATE-triggering systems (multi-colony
    /// economy, diplomacy, war, rebellion) can be SEEN without playing for hours. This is task #39, and it is the
    /// test rig for the whole political cluster: a fresh New Game shows none of it (no rivals, no war, nothing
    /// collapsing), so today the only way to exercise morale→legitimacy→rebellion or the diplomacy readout is a
    /// unit test — you can't watch it. A staged state fixes that.
    ///
    /// GENERATED, not a save file — on purpose. Our save format churns every session (`TypeNameHandling` bakes
    /// C# type names into the JSON, so every DataBlob we add/rename breaks old saves). This factory rides the
    /// CURRENT factories, so it never rots when a blob is added and it runs in CI. Save-file fixtures wait until
    /// the DataBlob set stabilises (post-MVP). Same reason we push logic into the CI-tested engine, not the
    /// CI-blind client — it lives here so BOTH the CI tests and the DevTools "Age the galaxy" button call it.
    ///
    /// The transforms are CUMULATIVE (Late applies Early+Mid+Late) and CONVERGENT (each checks current state and
    /// only adds what's missing), so it's safe to call repeatedly — the DevTools button can step Early→Mid→Late.
    /// Defensive: a transform whose preconditions aren't met is skipped, and the whole thing never throws (both
    /// callers run it live). Returns a short human summary of what it did.
    ///
    /// Deliberately does NOT create rival COLONIES or advance the clock: a rival is a *contacted faction* (enough
    /// for the diplomacy readout, drift, and war), and populations arrive as added colonies — this keeps the
    /// factory fast, deterministic, and clear of the colony-ownership blast radius (see the secession note in
    /// docs/TESTING-TRACKER.md).
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
                if (stage >= GameStage.Early) ApplyEarly(playerFaction, log);
                if (stage >= GameStage.Mid) ApplyMid(game, playerFaction, log);
                if (stage >= GameStage.Late) ApplyLate(game, playerFaction, log);
            }
            catch (Exception ex)
            {
                log.Append($"[error: {ex.Message}]");
            }
            return log.ToString();
        }

        /// <summary>EARLY — a second (frontier) colony so the economy is multi-world.</summary>
        private static void ApplyEarly(Entity playerFaction, StringBuilder log)
        {
            var fi = playerFaction.GetDataBlob<FactionInfoDB>();
            if (fi.Colonies.Count >= 2) { log.Append("[colonies ✓] "); return; }

            var home = fi.Colonies.FirstOrDefault();
            var species = fi.Species.FirstOrDefault();
            if (home == null || species == null) { log.Append("[no home/species] "); return; }

            var homeBody = home.GetDataBlob<ColonyInfoDB>().PlanetEntity;
            var system = homeBody?.Manager as StarSystem;
            if (system == null) { log.Append("[no home system] "); return; }

            var colonized = new HashSet<int>(fi.Colonies
                .Select(c => c.GetDataBlob<ColonyInfoDB>().PlanetEntity?.Id ?? -1));

            Entity target = null;
            foreach (var bodyDB in system.GetAllDataBlobsOfType<SystemBodyInfoDB>())
            {
                var body = bodyDB.OwningEntity;
                if (body == null || body.Id == (homeBody?.Id ?? -1) || colonized.Contains(body.Id)) continue;
                if (!body.HasDataBlob<NameDB>() || !body.HasDataBlob<MassVolumeDB>()) continue;
                target = body;
                break;
            }
            if (target == null) { log.Append("[no spare body] "); return; }

            ColonyFactory.CreateColony(playerFaction, species, target, 25_000_000);
            log.Append($"[+frontier colony on {target.GetDataBlob<NameDB>().GetName(playerFaction.Id)}] ");
        }

        /// <summary>MID — two rival factions the player has MET, with varied relations and a treaty.</summary>
        private static void ApplyMid(Game game, Entity playerFaction, StringBuilder log)
        {
            if (!playerFaction.TryGetDataBlob<DiplomacyDB>(out var pDip)) { log.Append("[no diplomacyDB] "); return; }
            var when = game.TimePulse.GameGlobalDateTime;

            // A cooperative neighbour (Friendly + a trade deal) and a hostile one — so the diplomacy readout has
            // both ends of the track and the reactive drift + IFF have real targets.
            if (pDip.Relationships.Count < 1)
                AddRival(game, playerFaction, "Vega Combine", "VEG", +40, TreatyType.TradeAgreement, when, log);
            if (pDip.Relationships.Count < 2)
                AddRival(game, playerFaction, "Crimson Hegemony", "CRH", -40, null, when, log);
            if (pDip.Relationships.Count >= 2 && log.Length > 0 && log[log.Length - 1] != ' ')
                log.Append("[rivals ✓] ");
        }

        private static void AddRival(Game game, Entity playerFaction, string name, string abbr,
            int scoreDelta, TreatyType? treaty, DateTime when, StringBuilder log)
        {
            var rival = FactionFactory.CreateBasicFaction(game, name, abbr, 50_000_000);
            if (!rival.TryGetDataBlob<DiplomacyDB>(out var rDip) ||
                !playerFaction.TryGetDataBlob<DiplomacyDB>(out var pDip))
                return;

            // A relationship is stored per side (they can disagree); stage both to the same standing.
            var pView = pDip.GetOrCreateRelationship(rival.Id);
            pView.AdjustScore(scoreDelta); pView.LastContact = when;
            var rView = rDip.GetOrCreateRelationship(playerFaction.Id);
            rView.AdjustScore(scoreDelta); rView.LastContact = when;

            // A friendly neighbour signs a deal (the score is already above the treaty's trust threshold).
            if (treaty.HasValue)
                Treaties.Propose(rival, playerFaction, treaty.Value, when);

            log.Append($"[+rival {name} ({scoreDelta:+0;-0}){(treaty.HasValue ? " +treaty" : "")}] ");
        }

        /// <summary>LATE — an active war with the most hostile rival + a frontier colony in open rebellion.</summary>
        private static void ApplyLate(Game game, Entity playerFaction, StringBuilder log)
        {
            var when = game.TimePulse.GameGlobalDateTime;

            // WAR: declare on the lowest-standing rival (the hostile one), if not already at war.
            if (playerFaction.TryGetDataBlob<DiplomacyDB>(out var pDip) && !pDip.IsAtWarWithAnyone())
            {
                var enemyRel = pDip.Relationships.Values.OrderBy(r => r.RelationScore).FirstOrDefault();
                if (enemyRel != null && game.Factions.TryGetValue(enemyRel.OtherFactionId, out var enemy))
                {
                    Diplomacy.DeclareWar(playerFaction, enemy, CasusBelli.ConfrontRival, when);
                    log.Append("[WAR declared] ");
                }
            }

            // REBELLION: push a non-home colony into the collapse band and open the reaction window. (Uses the
            // real LegitimacyProcessor transition, so it's a genuine rebellion the readout + quell path see.)
            var fi = playerFaction.GetDataBlob<FactionInfoDB>();
            var frontier = fi.Colonies.Skip(1).FirstOrDefault();
            if (frontier != null
                && frontier.TryGetDataBlob<LegitimacyDB>(out var leg)
                && frontier.TryGetDataBlob<RebellionDB>(out var reb)
                && !reb.IsRebelling)
            {
                if (frontier.TryGetDataBlob<ColonyMoraleDB>(out var mor))
                    mor.Morale = 10.0;                    // a miserable frontier
                leg.Legitimacy = 10.0;                    // below CollapseThreshold (20)
                LegitimacyProcessor.UpdateRebellion(reb, leg.Legitimacy, when);
                log.Append("[frontier REBELLING] ");
            }
        }
    }
}
