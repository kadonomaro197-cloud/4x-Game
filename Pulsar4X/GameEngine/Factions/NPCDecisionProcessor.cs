using System;
using Pulsar4X.DataStructures;
using Pulsar4X.Engine;
using Pulsar4X.Industry;
using Pulsar4X.Interfaces;
using Pulsar4X.People;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// Monthly decision loop for AI-controlled factions (the Organism brain, docs/AI-BRAIN-BUILD-TRACKER.md).
    /// Each cycle it runs reactive-diplomacy drift and settles a strategic objective: read the needs-ladder
    /// (<see cref="NeedsLadder"/>) → pick an objective from tier × doctrine × <see cref="PersonalityDB"/>
    /// (<see cref="ObjectiveSelector"/>) → commit it through the hysteresis engine (<see cref="ObjectiveTransition"/>)
    /// → store it on the faction's <see cref="StrategicObjectiveDB"/>.
    ///
    /// Faction entities live in the GlobalManager, which <see cref="Engine.MasterTimePulse"/> DOES iterate (keystone
    /// fixed 2026-06-30), so this fires on its schedule (proven by FactionEconomyTests). The decision loop is live;
    /// turning the stored objective into actual ORDERS (build / expand / attack) is the follow-on (Phase 2.4c+), so
    /// the brain currently DECIDES but does not yet ACT.
    /// </summary>
    public class NPCDecisionProcessor : IHotloopProcessor
    {
        public TimeSpan RunFrequency => TimeSpan.FromDays(30);
        public TimeSpan FirstRunOffset => TimeSpan.FromDays(5);
        public Type GetParameterType => typeof(FactionInfoDB);

        /// <summary>
        /// Liveness gauge: total faction entities processed across all ProcessManager calls. Climbs only once
        /// MasterTimePulse actually iterates the GlobalManager (where faction entities live) — before the
        /// keystone fix this stayed 0 forever (the processor was registered but never reached any faction).
        /// Read by tests to prove faction-level processors fire. Not serialized; resets each process start.
        /// </summary>
        public static int TickCount;

        /// <summary>
        /// Phase-2.4c gate: when true, the Tick doesn't just DECIDE — it ACTS, emitting real orders for the settled
        /// objective (the first behaviour-changing step). Defaults <b>false</b> so every existing test is
        /// byte-identical (the brain decides but stays hands-off); the client turns it on. Mirrors the combat/economy
        /// flag pattern (`RequireDetectionToEngage`, `TradeIncomeProcessor.EnablePayout`).
        /// </summary>
        public static bool EnableOrderEmission = false;

        /// <summary>
        /// Phase-3.3 gate (docs/AI-BRAIN-BUILD-TRACKER.md — the Ecosystem): when true, the Tick lets an NPC PROPOSE
        /// treaties to its neighbours (a real behaviour change — a signed pact), turning the built-but-uncalled
        /// <see cref="Treaties.Propose"/> into live diplomacy. Defaults <b>false</b> so every existing test is
        /// byte-identical. A SIBLING of <see cref="EnableOrderEmission"/> (not the same flag) so combat/economy order
        /// tests and diplomacy tests don't couple — either can be flipped alone.
        /// </summary>
        public static bool EnableDiplomaticProposals = false;

        /// <summary>
        /// Phase-3.1 gate (docs/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md — the Information Ledger): when true, the monthly
        /// Tick POPULATES each NPC's persistent <see cref="InformationLedgerDB"/> — Confirming the Military facet (and
        /// recording the current <see cref="ThreatAssessment.DetectedStrengthOf"/> sample) for every rival it currently
        /// detects, and decaying the rest to Stale. That's what turns the inert ledger shell into a populated, decaying,
        /// trend-readable record (<see cref="ThreatAssessment.IsRising"/>). Defaults <b>false</b> so every existing test
        /// is byte-identical (with it off the attached ledger stays empty and nothing reads a trend). A SIBLING of
        /// <see cref="EnableOrderEmission"/> / <see cref="EnableDiplomaticProposals"/> so it can be flipped alone.
        /// </summary>
        public static bool EnableIntelLedger = false;

        /// <summary>Phase-3.1 tunable: a Confirmed intel record not refreshed within this span decays to Stale (you
        /// can't know a rival forever). One game-year by default; provisional / live-tunable.</summary>
        public static TimeSpan IntelStaleAfter = TimeSpan.FromDays(365);

        /// <summary>
        /// Espionage E5 gate (docs/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md §G — the ALWAYS-ON MIRROR): when true, an NPC
        /// with spy capacity (a built <see cref="IntelDirectorateDB"/>) and an idle operative runs covert ops against
        /// its rivals — INCLUDING the player. This is what makes counter-intelligence a standing decision rather than a
        /// one-way toy: neglect your counter-intel rating and enemy agents raise their picture of you (and, in E6, steal
        /// and sabotage). Tuned LOW and gated on hostility (a friendly neighbour isn't spied on). Defaults <b>false</b> so
        /// every existing test is byte-identical (no NPC tasks an op); a SIBLING of the other espionage/AI gates so it
        /// can be flipped alone.
        /// </summary>
        public static bool EnableEspionageMirror = false;

        /// <summary>Phase-3.4c tunable: the "shed the obligation" temptation a defensive pact exerts once the shared
        /// threat is gone, measured against a faction's Honour (<see cref="Treaties.WouldKeepFaith"/>). At 0.5 a
        /// Neutral-Honour (or personality-less) faction sits exactly on the keep-faith line, so it does NOT break —
        /// only a below-neutral-Honour faction cracks the coalition. Provisional; live-tunable.</summary>
        public const double PactAbandonTemptation = 0.5;

        /// <summary>Phase-4 gate: when true, each cycle checks for a galaxy CRISIS (a faction that has ascended) and
        /// forms the NPC coalition against it (declare war, reuse Phase-3.4). <b>LIVE by default as of the Phase-4 finish
        /// (2026-07-12)</b> — the ascension is now reachable in a real game (the <c>tech-ascension</c> "Transcendence"
        /// research grants <c>capability-ascension</c>), so the crisis actually triggers. Still byte-identical across the
        /// test suite: nothing there researches the new tech, so no faction holds the capability and the coalition call
        /// is a no-op (returns 0). Galaxy-level + idempotent, so running it per-NPC-tick is safe; kept as a flag so a
        /// scenario/test can disable it.</summary>
        public static bool EnableGalaxyCrisis = true;

        private Game _game;

        public void Init(Game game)
        {
            _game = game;
        }

        public void ProcessEntity(Entity entity, int deltaSeconds)
        {
            if (!entity.TryGetDataBlob<FactionInfoDB>(out var factionInfoDB))
                return;
            if (!factionInfoDB.IsNPC)
                return;

            Tick(entity, factionInfoDB);
        }

        public int ProcessManager(EntityManager manager, int deltaSeconds)
        {
            int count = 0;
            foreach (var entity in manager.GetAllEntitiesWithDataBlob<FactionInfoDB>())
            {
                ProcessEntity(entity, deltaSeconds);
                count++;
            }
            TickCount += count; // liveness gauge — proves the GlobalManager is now being iterated
            return count;
        }

        /// <summary>
        /// Core NPC decision step. Evaluates doctrine weights and selects the
        /// highest-priority goal for this faction this cycle.
        /// </summary>
        private static void Tick(Entity factionEntity, FactionInfoDB factionInfoDB)
        {
            // Reactive diplomacy (docs/DIPLOMACY-DESIGN.md "Are we good?"): a faction's feelings DRIFT each cycle
            // based on what it can read of its neighbours, turning the previously-dead ReactiveDiplomacy table into
            // a live loop. Runs before the doctrine step so it happens every cycle regardless of doctrine weights.
            RunDiplomaticDrift(factionEntity);

            // Phase-3.1: keep the persistent Information Ledger LIVE — Confirm+sample the rivals we currently detect,
            // decay the rest. A real state write, so it's behind its own default-off gate (byte-identical until opted
            // in). Runs alongside drift (every cycle, independent of doctrine) so the trend record is continuous.
            if (EnableIntelLedger)
                UpdateInformationLedger(factionEntity);

            // The Organism decision (docs/AI-BRAIN-BUILD-TRACKER.md, Movement II Phase 2): read the needs-ladder, pick
            // an objective from tier × doctrine × personality, and commit it through the hysteresis engine. 2.4b
            // settles + STORES the objective (the DECISION).
            UpdateStrategicObjective(factionEntity, factionInfoDB);

            // Reactive POLITICS (Phase 3.3): seek a pact with a qualifying neighbour. A real behaviour change (a
            // signed treaty), so it's behind its own default-off gate — byte-identical until a client/test opts in.
            if (EnableDiplomaticProposals)
                RunTreatyPolicy(factionEntity);

            // The espionage MIRROR (E5): NPCs spy on their rivals — including the player — so counter-intel is a
            // standing decision, not optional. Gated (default off) → byte-identical until opted in.
            if (EnableEspionageMirror)
                RunEspionageMirror(factionEntity, factionInfoDB);

            // Phase-4.2b: the galaxy crisis — if a faction has ASCENDED, the NPCs unite against it (declare war).
            // Gated (default off) so byte-identical; galaxy-level + idempotent, so a per-NPC-tick call is safe.
            if (EnableGalaxyCrisis)
            {
                var game = factionEntity.Manager?.Game;
                if (game != null)
                    GalaxyCrisis.FormCoalitionAgainstAscendant(game, game.TimePulse.GameGlobalDateTime);
            }

            // ACT on it (Phase 2.4c) — but only behind the default-off gate, so the plan-only path stays
            // byte-identical until a client/test opts in.
            if (EnableOrderEmission)
                EmitOrders(factionEntity, factionInfoDB);
        }

        /// <summary>
        /// Phase-2.4c: turn the faction's settled <see cref="StrategicObjectiveDB"/> into real orders. This slice
        /// wires the first, safest objective — <see cref="StrategicObjective.GrowEconomy"/> → queue an industry job on
        /// a colony (the same lever a player pulls). The other objectives (Defend/Consolidate/Expand/Conquer) are the
        /// follow-on slices (2.4d+); they no-op here. Defensive/no-throw (runs in a hotloop). Internal for the gauge.
        /// </summary>
        internal static void EmitOrders(Entity factionEntity, FactionInfoDB factionInfo)
        {
            if (!factionEntity.TryGetDataBlob<StrategicObjectiveDB>(out var objective)) return;

            // Phase-2.8 P0-b: the means-ends PLANNER. Look up the resolver for the settled objective, snapshot the
            // faction's state, and let the resolver name the ONE step that advances the nearest unmet prerequisite;
            // the processor runs it. Objectives with no registered resolver no-op (Expand/Conquer/Defend land later).
            if (!ObjectiveResolvers.TryGet(objective.Objective, out var resolver)) return;

            FactionState state = FactionState.Snapshot(factionEntity);
            if (state == null) return;                       // defensive (hotloop)

            PlannerAction action = resolver.Resolve(state, objective);
            objective.LastActionKind = action.Kind;          // Visibility Gate: record what the planner decided…
            objective.LastActionDetail = action.Detail;      // …before acting, so a stuck NPC is never silent
            action.Execute?.Invoke();                        // the ONE step (the only side effect)
        }

        /// <summary>
        /// SUPERSEDED by <see cref="GrowEconomyResolver"/> (Phase-2.8 P0-b) — <c>EmitOrders</c> no longer calls this;
        /// the resolver's Rung C does the same selection AND routes the build through <c>AutoAddSubJobs</c> (which this
        /// bare version lacked — it's blind to its own inputs). Retained only as the direct-mechanic gauge in
        /// <c>NPCOrderEmissionTests</c>; delete with that test in a later cleanup slice.
        ///
        /// GrowEconomy's action: queue ONE buildable design onto the first free production line on the colony that can
        /// make it (repeat on). Returns true if a job was queued. No-op (false) on a colony with no industry or
        /// nothing buildable/free.
        /// </summary>
        internal static bool TryQueueEconomyJob(Entity colony, FactionInfoDB factionInfo)
        {
            if (colony == null || !colony.TryGetDataBlob<IndustryAbilityDB>(out var industry)) return false;

            foreach (var designKvp in factionInfo.IndustryDesigns)
            {
                IConstructableDesign design = designKvp.Value;
                if (design == null) continue;

                foreach (var lineKvp in industry.ProductionLines)
                {
                    if (!lineKvp.Value.IndustryTypeRates.ContainsKey(design.IndustryTypeID)) continue;
                    if (lineKvp.Value.Jobs.Count > 0) continue;   // that line is busy — try another design/line

                    var job = new IndustryJob(factionInfo, designKvp.Key);
                    job.InitialiseJob(1, true);                   // repeat: keep the line producing
                    IndustryTools.AddJob(colony, lineKvp.Key, job);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Phase-2.4b: settle this faction's <see cref="StrategicObjectiveDB"/> for the cycle. Assess the needs-tier
        /// (<see cref="NeedsLadder"/>), select the concrete objective (<see cref="ObjectiveSelector"/> over doctrine +
        /// personality), and commit it through the transition engine (<see cref="ObjectiveTransition"/>) so the plan
        /// doesn't thrash. The blob is created on first run. Uses GAME time (not wall-clock) so it stays deterministic.
        /// Defensive: a faction with no manager/game is skipped. Internal for the CI gauge.
        /// </summary>
        internal static void UpdateStrategicObjective(Entity factionEntity, FactionInfoDB factionInfoDB)
        {
            var game = factionEntity.Manager?.Game;
            if (game == null) return;
            DateTime now = game.TimePulse.GameGlobalDateTime;

            if (!factionEntity.TryGetDataBlob<StrategicObjectiveDB>(out var objective))
            {
                objective = new StrategicObjectiveDB();
                factionEntity.SetDataBlob(objective);
            }

            NeedTier tier = NeedsLadder.AssessTier(factionEntity);
            factionEntity.TryGetDataBlob<PersonalityDB>(out var personality);   // null → neutral in the selector
            // Phase-5.2 decision-log: take the choice AND the reason tracing it to the driving input.
            var (chosen, reason) = ObjectiveSelector.SelectWithReason(tier, factionInfoDB.Doctrine, personality);

            // Target selection (which rival to Conquer) is the 2.4c refinement; keep -1 (none) for now.
            // Phase-2.5: the commit DWELL scales with Ambition — a high-Ambition faction renews an expansion push
            // (Expand/Conquer) on a SHORTER cadence, a low-Ambition one dwells longer. Neutral/absent personality and
            // every non-expansion objective return the fixed DefaultCommitFor, so this stays byte-identical today.
            TimeSpan commitFor = ObjectiveTransition.CommitFor(chosen, personality);
            ObjectiveTransition.Advance(objective, tier, chosen, -1, now, commitFor);

            // Record WHY: if the transition committed the fresh choice, that's the reason; if hysteresis HELD a prior
            // objective (the brain didn't thrash), say so and note what this cycle actually read (still traceable).
            objective.DecisionReason = objective.Objective == chosen
                ? reason
                : $"holding {objective.Objective} (hysteresis until {objective.CommittedUntil:u}); this cycle read: {reason}";
        }

        /// <summary>
        /// Reactive-diplomacy DRIFT: for each faction this one has met (a relationship row exists), nudge the
        /// relationship score by the effect of what's OBSERVABLE from existing state — a militarist neighbour
        /// sours the mood; a standing treaty warms it (kept faith). The magnitudes come straight from the locked
        /// <see cref="ReactiveDiplomacy.RelationDelta"/> table (no invented numbers); the monthly cadence is
        /// <see cref="RunFrequency"/>. Conservative on purpose — drift only, no auto-proposed treaties (that's a
        /// policy call, and border-proximity reactions need a territory model neither of which exists yet).
        ///
        /// Start-safe: a relationship row only exists after first contact, so a single-faction New Game has no
        /// rows and this is inert. War is skipped (its own latched track). Internal for the CI gauge.
        /// </summary>
        internal static void RunDiplomaticDrift(Entity factionEntity)
        {
            if (factionEntity == null || !factionEntity.TryGetDataBlob<FactionInfoDB>(out var dummy)) return;
            if (!factionEntity.TryGetDataBlob<DiplomacyDB>(out var dipDB)) return;
            var game = factionEntity.Manager?.Game;
            if (game == null) return;

            foreach (var rel in dipDB.Relationships.Values)
            {
                if (rel.AtWar) continue; // war is its own latched track; drift doesn't apply while shooting

                int delta = 0;

                // A militarist neighbour sours the mood (their hawks are loud).
                if (game.Factions.TryGetValue(rel.OtherFactionId, out var otherFaction)
                    && otherFaction.TryGetDataBlob<GovernmentDB>(out var otherGov)
                    && otherGov.Militarism == GovNotch.High)
                    delta += ReactiveDiplomacy.RelationDelta(ExternalStimulus.TheirMilitaristsRose);

                // Kept faith: while a treaty stands, trust slowly accrues (deals build on themselves).
                if (rel.NonAggressionPact || rel.TradeAgreement || rel.LogisticsAccess
                    || rel.MilitaryAccess || rel.DefensivePact)
                    delta += ReactiveDiplomacy.RelationDelta(ExternalStimulus.YouHonoredTreaties);

                if (delta != 0)
                    rel.AdjustScore(delta);
            }
        }

        /// <summary>
        /// Phase-3.1 (docs/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md — the Information Ledger): drive this faction's
        /// persistent <see cref="InformationLedgerDB"/> for the cycle. For every OTHER faction (skip self + the neutral
        /// catch-all) it reads the fog-limited <see cref="ThreatAssessment.DetectedStrengthOf"/>: a rival it currently
        /// SEES gets its Military facet Confirmed with that strength recorded as a fresh sample (so the last-vs-prior
        /// pair — the <see cref="ThreatAssessment.IsRising"/> trend — accumulates); a rival it can't see is left to age.
        /// Then <see cref="InformationLedgerDB.DecayStale"/> drops any Confirmed record it hasn't refreshed within
        /// <see cref="IntelStaleAfter"/> to Stale (a just-refreshed record is safe: now − now = 0). Uses GAME time (not
        /// wall-clock) so it stays deterministic. Gated by <see cref="EnableIntelLedger"/>, so default-off is
        /// byte-identical (the ledger stays empty). Defensive/no-throw — runs in the monthly hotloop. Internal for the gauge.
        /// </summary>
        internal static void UpdateInformationLedger(Entity factionEntity)
        {
            if (factionEntity == null || !factionEntity.TryGetDataBlob<InformationLedgerDB>(out var ledger)) return;
            var game = factionEntity.Manager?.Game;
            if (game == null) return;
            DateTime now = game.TimePulse.GameGlobalDateTime;

            foreach (var kvp in game.Factions)
            {
                int rivalId = kvp.Key;
                if (rivalId == factionEntity.Id || rivalId == Game.NeutralFactionId) continue;

                double detected = ThreatAssessment.DetectedStrengthOf(factionEntity, rivalId);
                if (detected > 0)                                           // we can SEE them → confirm + sample
                    ledger.Confirm(rivalId, IntelFacet.Military, now, detected);
                // else: leave the record to age; DecayStale (below) will drop it if it goes cold.
            }

            ledger.DecayStale(now, IntelStaleAfter);
        }

        /// <summary>
        /// Espionage E5 (docs/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md §G — the always-on mirror): an NPC runs a covert op
        /// against the rival it likes LEAST. The move that makes the spy game two-sided — an NPC with a built
        /// <see cref="IntelDirectorateDB"/> (capacity) and an idle <see cref="CommanderTypes.Intelligence"/> operative
        /// tasks a low-risk <see cref="CovertAction.GatherIntel"/> op on its most-hostile met rival (score at or below
        /// <see cref="RelationshipState.HostileThreshold"/>; war reads as fully hostile), which is the PLAYER whenever
        /// the player is that rival. Tuned LOW: the natural rate-limit is the op's ~90-day duration (an agent stays busy)
        /// plus the monthly cadence — one agent can only run one op at a time, so "scaling with hostility" here means the
        /// hostility GATE (a friendly neighbour is never targeted). Gated by <see cref="EnableEspionageMirror"/> →
        /// default-off is byte-identical. Steal/sabotage escalation for higher hostility is E6. Defensive/no-throw
        /// (monthly hotloop). Internal for the CI gauge.
        /// </summary>
        internal static void RunEspionageMirror(Entity factionEntity, FactionInfoDB factionInfo)
        {
            var game = factionEntity.Manager?.Game;
            if (game == null) return;
            if (!factionEntity.TryGetDataBlob<DiplomacyDB>(out var dip)) return;

            // Spy capacity: at least one built directorate with op capacity. No directorate → no ops (the E1 gear gate).
            bool hasCapacity = false;
            foreach (var colony in factionInfo.Colonies)
                if (colony.TryGetDataBlob<IntelDirectorateDB>(out var directorate) && directorate.OpCapacity > 0)
                {
                    hasCapacity = true;
                    break;
                }
            if (!hasCapacity) return;

            // An idle operative to task (one not already busy on an op).
            Entity agent = Entity.InvalidEntity;
            foreach (var commander in factionInfo.Commanders)
                if (commander.TryGetDataBlob<CommanderDB>(out var cdb) && cdb.Type == CommanderTypes.Intelligence
                    && !commander.HasDataBlob<CovertOpDB>())
                {
                    agent = commander;
                    break;
                }
            if (!agent.IsValid) return;

            // Target the MOST-hostile met rival, and only if it's actually hostile (the "scales with hostility" gate —
            // a friendly/neutral neighbour is left alone). War reads as the floor score.
            int targetId = -1;
            int worst = RelationshipState.HostileThreshold; // must be at or below this to be worth a covert op
            foreach (var rel in dip.Relationships.Values)
            {
                if (rel.OtherFactionId == factionEntity.Id || rel.OtherFactionId == Game.NeutralFactionId) continue;
                int score = rel.AtWar ? RelationshipState.MinScore : rel.RelationScore;
                if (score <= worst)
                {
                    worst = score;
                    targetId = rel.OtherFactionId;
                }
            }
            if (targetId < 0) return; // no hostile rival → the mirror stays quiet

            // Run the safe baseline: gather intel on their military. TaskAgent handles the guards + scheduling.
            Espionage.TaskAgent(agent, targetId, CovertAction.GatherIntel, IntelFacet.Military, game.TimePulse.GameGlobalDateTime);
        }

        /// <summary>
        /// Phase-3.3 (docs/AI-BRAIN-BUILD-TRACKER.md — the Ecosystem): the NPC treaty POLICY — the first step of the
        /// living galaxy. Each monthly cycle, for the first met, not-at-war rival whose standing already clears a
        /// non-aggression pact's trust bar, the faction PROPOSES one — turning the built-but-uncalled
        /// <see cref="Treaties.Propose"/> into live behaviour (an NPC that actively seeks détente, not just drifts).
        /// Modeled on <see cref="RunDiplomaticDrift"/>; gated by <see cref="EnableDiplomaticProposals"/> (a real
        /// behaviour change), so default-off is byte-identical. One proposal per cycle (least-commitment, the resolver
        /// cadence). Defensive/no-throw — runs in the monthly hotloop. Internal for the CI gauge.
        ///
        /// v1 proposes only <see cref="TreatyType.NonAggression"/> — its bar (Hostile −25) is reachable by drift,
        /// where a <see cref="TreatyType.DefensivePact"/> needs Allied 75 and rides 3.2's shared-threat read. The pact
        /// auto-resolves through <see cref="Treaties.WouldAccept"/>; a "the player answers the offer" inbox is a later
        /// refinement (for now an NPC↔NPC détente is the payoff).
        /// </summary>
        internal static void RunTreatyPolicy(Entity factionEntity)
        {
            if (factionEntity == null || !factionEntity.TryGetDataBlob<FactionInfoDB>(out _)) return;
            if (!factionEntity.TryGetDataBlob<DiplomacyDB>(out var dipDB)) return;
            var game = factionEntity.Manager?.Game;
            if (game == null) return;

            // Pass 0 (Phase-3.4b — HONOUR the pact: JOIN a DefensivePact ally's war). A defensive pact isn't just
            // "we don't shoot each other" — it's "your enemies are my enemies." Each cycle, if a faction we hold a
            // DefensivePact with is AtWar with some faction X (and we aren't already), we DECLARE WAR on X (casus
            // belli AllyDefense — justified, we were dragged in). This is the move that turns two pacts into one real
            // COALITION: the shared threat that 3.3b allied us against now faces BOTH of us. It composes straight into
            // 3.4a — the instant we declare, combat reads us as hostile to X regardless of anything we'd signed with
            // it. Runs BEFORE the pact-forming passes (an existing obligation outranks a new offer). One war-join per
            // cycle (least-commitment). Breaking the pact once the threat is gone is Phase-3.4c.
            foreach (var rel in dipDB.Relationships.Values)
            {
                if (!rel.DefensivePact) continue;                          // only a defensive-pact ally pulls us in
                if (rel.OtherFactionId == factionEntity.Id || rel.OtherFactionId == Game.NeutralFactionId) continue;
                if (!game.Factions.TryGetValue(rel.OtherFactionId, out var ally)) continue;
                if (!ally.TryGetDataBlob<DiplomacyDB>(out var allyDip)) continue;

                foreach (var allyRel in allyDip.Relationships.Values)
                {
                    if (!allyRel.AtWar) continue;                          // find who the ally is fighting
                    int enemyId = allyRel.OtherFactionId;
                    if (enemyId == factionEntity.Id || enemyId == Game.NeutralFactionId) continue; // not the ally-vs-us case
                    if (enemyId == rel.OtherFactionId) continue;           // guard (an ally isn't at war with itself)
                    if (dipDB.HasMet(enemyId) && dipDB.GetRelationship(enemyId).AtWar) continue;   // already in this war
                    if (!game.Factions.TryGetValue(enemyId, out var enemy)) continue;

                    if (Diplomacy.DeclareWar(factionEntity, enemy, CasusBelli.AllyDefense, game.TimePulse.GameGlobalDateTime))
                        return;   // one war-join per cycle
                }
            }

            // Pass 1 (Phase-3.4 seed — ally against a SHARED THREAT): if we fear a rival most (the strongest we can
            // SEE that out-muscles us, 3.2), seek a DEFENSIVE PACT with a TRUSTED neighbour who isn't that threat.
            // Deliberately rare: it needs both a feared rival AND an Allied-trust (75) partner, so it fires only when a
            // real alliance-against-a-common-enemy is on the table — the diplomatic seed a coalition (3.4) grows from.
            int threatId = ThreatAssessment.GreatestThreatTo(factionEntity).rivalId;

            // Pass C (Phase-3.4c — the CRACK: a coalition betrays once the shared threat DIES). A defensive pact
            // formed against a common enemy loses its reason to exist the moment that enemy is neutralized. When we
            // no longer fear anyone (GreatestThreatTo names nobody → threatId == -1), a LOW-HONOUR faction sheds the
            // obligation — it BREAKS the pact for the freedom of not being entangled; a HIGH-HONOUR faction keeps
            // faith regardless (Treaties.WouldKeepFaith: honour vs the abandon temptation). Breaking the pact re-arms
            // the two former allies (3.4a's peace-suppression is gone the instant the flag drops), so betrayal has
            // teeth. Gated on "no current threat," so it is mutually exclusive with Pass 1 (which needs a threat) —
            // the alliance forms vs a riser and cracks when it dies, exactly the 3.4 acceptance line. One move/cycle.
            if (threatId == -1)
            {
                factionEntity.TryGetDataBlob<PersonalityDB>(out var personality);   // null → Neutral honour (keeps faith)
                if (!Treaties.WouldKeepFaith(personality, PactAbandonTemptation))
                {
                    foreach (var rel in dipDB.Relationships.Values)
                    {
                        if (!rel.DefensivePact) continue;                          // only a still-standing pact can crack
                        if (rel.OtherFactionId == factionEntity.Id || rel.OtherFactionId == Game.NeutralFactionId) continue;
                        if (!game.Factions.TryGetValue(rel.OtherFactionId, out var former)) continue;
                        if (Diplomacy.BreakTreaty(factionEntity, former, TreatyType.DefensivePact, game.TimePulse.GameGlobalDateTime))
                            return;   // one treaty move per cycle
                    }
                }
            }

            if (threatId != -1)
            {
                foreach (var rel in dipDB.Relationships.Values)
                {
                    if (rel.AtWar || rel.DefensivePact) continue;
                    if (rel.OtherFactionId == factionEntity.Id || rel.OtherFactionId == Game.NeutralFactionId) continue;
                    if (rel.OtherFactionId == threatId) continue;                       // don't ally WITH the one we fear
                    if (!Treaties.WouldAccept(rel, TreatyType.DefensivePact)) continue; // needs Allied trust (75)
                    if (!game.Factions.TryGetValue(rel.OtherFactionId, out var ally)) continue;
                    if (Treaties.Propose(factionEntity, ally, TreatyType.DefensivePact, game.TimePulse.GameGlobalDateTime))
                        return;   // one treaty move per cycle
                }
            }

            // Pass 2 (Phase-3.3 — plain détente): otherwise seek a NonAggression pact with any qualifying neighbour.
            foreach (var rel in dipDB.Relationships.Values)
            {
                if (rel.AtWar) continue;                                    // no ordinary treaty while shooting
                if (rel.NonAggressionPact) continue;                       // already signed — don't re-warm every cycle
                if (rel.OtherFactionId == factionEntity.Id) continue;      // never propose to self
                if (rel.OtherFactionId == Game.NeutralFactionId) continue; // skip the neutral catch-all

                // Cheap pre-check to avoid spamming refusals (the real accept decision re-checks the TARGET's view
                // inside Propose; this reads our own view of them as an approximation).
                if (!Treaties.WouldAccept(rel, TreatyType.NonAggression)) continue;

                if (!game.Factions.TryGetValue(rel.OtherFactionId, out var target)) continue;

                // The act: propose the pact. Propose signs BOTH ledgers + warms both scores on acceptance.
                if (Treaties.Propose(factionEntity, target, TreatyType.NonAggression, game.TimePulse.GameGlobalDateTime))
                    return;   // one treaty move per cycle
            }
        }
    }
}
