export const meta = {
  name: 'earthfall-campaign',
  description: 'Operation Earthfall v2 — parallel-lane fix/build campaign: freeze, AI coherence, sealift, ground campaign + battalions + infra combat, faction development, player marine chain, 2D resolver S0-S2. One phase per invocation; the session commits slices and gates CI between them.',
  whenToUse: 'Invoke with args {phase:"P0"|"P3"|"P4"|"G1".."G4"|"C1".."C5"|"D1".."D3"|"T0".."T3"|"PW"|"P8"} from the lane branch that owns that phase (see docs/earthfall/CAMPAIGN-PLAN.md §3). Requires docs/earthfall/ (plan + findings) present in the repo.',
  phases: [
    { title: 'Design' },
    { title: 'Implement' },
    { title: 'Verify' },
    { title: 'Synthesize' },
  ],
}

// ============================================================================
// OPERATION EARTHFALL v2 — self-contained for ANY executor session/model.
// Ground truth: docs/earthfall/CAMPAIGN-PLAN.md (the brief) + docs/earthfall/findings/
// (ten verified file:line ledgers). Prompts below cite repo-relative paths only.
// ============================================================================

const F = 'docs/earthfall/findings'

const COMMON = `
You are one implementation slice of OPERATION EARTHFALL. Before ANYTHING else, read
docs/earthfall/CAMPAIGN-PLAN.md — sections 0 (findings headlines), 1 (glossary: DataBlob,
Atb exact-arity binder, six-point registration, hotloop L4/L9, byte-identity, save-safety,
CI shards), and 2 (standing rules). Then read the findings ledger(s) your slice names.
HARD RULES (repeated because they are the ones that get broken):
- NO .NET SDK in this container: you CANNOT compile. Verify every type/member/namespace/
  arity by grep/Read of the real source BEFORE using it. A wrong member reds 4 CI shards
  for ~30 minutes. The repo root is the current working directory's git root.
- Do NOT git commit or push. Leave edits in the working tree; the session lands them
  slice-by-slice and gates CI.
- STAY INSIDE YOUR LANE'S FILE FENCE (CAMPAIGN-PLAN.md §3). An out-of-fence need becomes
  a written REQUEST in docs/earthfall/LANE-<X>-NOTES.md, never an edit.
- Do NOT edit docs/DOCS-INDEX.md / TESTING-TRACKER.md / SYSTEM-CONNECTION-MAP.md — append
  your pending rows to your lane notes file instead (campaign amendment, PLAN §2.4).
- Update the subsystem CLAUDE.md rows for code you changed (inside your fence), match
  CONVENTIONS.md idioms, NUnit-3 tests with UNIQUE new fixture filenames.
- PARALLEL SAFETY: your slice may run CONCURRENTLY with file-disjoint sibling slices in
  the same working tree (the phase's batches). Touch ONLY files your slice names or
  creates; never run repo-wide reformatting. If your subsystem-CLAUDE.md target could
  collide with a parallel sibling's, put the row text in your lane notes instead and say
  so in your report.
- Every new gameplay number gets a "// FLAGGED balance value" comment.
- State explicitly in your report which byte-identity claim your change makes:
  (a) default-off flag, or (b) provably inert absent new data — and why.
`

const VERDICT = {
  type: 'object', additionalProperties: false,
  required: ['approve', 'blockers', 'notes'],
  properties: {
    approve: { type: 'boolean' },
    blockers: { type: 'array', items: { type: 'string' } },
    notes: { type: 'string' },
  },
}

async function runSlice(slice) {
  log(`slice: ${slice.key} — ${slice.title}`)
  const impl = await agent(
    COMMON + `\nYOUR SLICE (${slice.key}): ${slice.title}\n\n` + slice.prompt +
    `\n\nRETURN: files changed/created + what each change does; tests added + what they assert; the byte-identity claim; every FLAGGED number; every developer decision or cross-lane request you wrote to the lane notes.`,
    { label: `impl:${slice.key}`, phase: 'Implement' })
  if (impl == null) { log(`slice ${slice.key}: implementer died — SKIPPED`); return { key: slice.key, status: 'skipped' } }

  const verdict = await agent(
    COMMON + `\nYou are an ADVERSARIAL verifier for slice ${slice.key} ("${slice.title}").
Read the working tree diff (git diff + git status) and the implementer report below. Try to REFUTE:
- COMPILE: does every referenced member exist with that exact name/arity/namespace? usings present? test file included in the test project by convention?
- FENCE: any file outside the lane fence (CAMPAIGN-PLAN.md §3)? Any dashboard file edited?
- BYTE-IDENTITY: is the stated claim actually true? default flags actually default-off? existing fixtures unaffected?
- LANDMINES: L4 (throw in a hotloop), L9 (second hotloop on a blob), atb exact-arity + six-point JSON registration completeness, [JsonProperty]+deep-copy for save-carried fields, enum append-only ordering, stale docs left behind.
- TESTS: do they gauge the claimed behavior (not tautologies)? [Timeout] on anything that advances the clock?
IMPLEMENTER REPORT:\n${impl}\n
Default to approve=false when uncertain; every blocker concrete (file:line).`,
    { label: `verify:${slice.key}`, phase: 'Verify', schema: VERDICT })

  if (verdict && !verdict.approve && verdict.blockers.length > 0) {
    log(`slice ${slice.key}: ${verdict.blockers.length} blocker(s) — repair pass`)
    await agent(
      COMMON + `\nREPAIR pass for slice ${slice.key} ("${slice.title}"). Fix each blocker below in the working tree (verify APIs by reading source; smallest correct change):\n- ` +
      verdict.blockers.join('\n- ') + `\n\nORIGINAL REPORT:\n${impl}\n\nRETURN: what you fixed for each blocker.`,
      { label: `repair:${slice.key}`, phase: 'Verify' })
  }
  return { key: slice.key, status: 'done', approved: verdict ? verdict.approve : null, blockers: verdict ? verdict.blockers : [] }
}

// ============================================================================
// PHASES — keyed for the lane structure in CAMPAIGN-PLAN.md §3/§4.
// ============================================================================

const PHASES = {

  // ------------------------------------------------------------------ CORE --
  // P0 batches: all five slices are file-disjoint (two NEW test files / ci.yml / the
  // Factions recorder files / the Engine concurrency files — different CLAUDE.mds too).
  P0: { lane: 'CORE', title: 'VISIBILITY & REPRO (pre-fork)', batches: [[0, 1, 2, 3, 4]], slices: [
    { key: 'P0.1', title: 'Campaign-clock CI repro fixture', prompt: `
Read ${F}/A1-freeze.md (MISSING section) + Pulsar4X/Pulsar4X.Tests/NPCActingSensorTests.cs + CombatFleetTreeSafetyTests (StandingHostilePair test).
Build Pulsar4X/Pulsar4X.Tests/CampaignClockReadoutTests.cs: DevTestStartFactory.CreateDevTest (uef-devtest+umf+kithrin), open the four NPCDecisionProcessor gates AND CombatEngagement.InterruptTimeOnNewEngagement/RequireDetectionToEngage/RequireWeaponRangeToEngage in try/finally restores, then drive the REAL master clock (game.TimePulse, 3600s steps) for a bounded window (default 40 game-days as a const). Per game-day record wall-ms, MasterTimePulse.FineStepCount delta, CombatEngagement.TickCount delta, SensorScan.ScanCount delta, NPCDecisionProcessor.TickErrorCount/LastTickError, ManagerSubPulse.GlobalCurrentProcess. Assert: FineStepCount FLAT while no fleets are within CombatEngagement.EngagementRange_m; wall-ms/day under a generous bound; zero tick errors. [Timeout] mandatory. Write the whole readout to TestResults/campaign-clock-readout.txt (create dir). Do NOT use TestScenario.AdvanceTime (forces single-thread); drive TimePulse directly.` },
    { key: 'P0.2', title: 'Per-faction self-sufficiency readout', prompt: `
Read ${F}/A6-faction-development.md (Q2 + gauge plan) + Pulsar4X/Pulsar4X.Tests/EconomyReadoutTests.cs (pattern).
Build FactionSelfSufficiencyReadoutTests.cs: DevTest sandbox, advance ~120 game-days ([Timeout]), then per faction iterate FactionInfoDB.Colonies AND .Stations; per host print food supply vs demand (ColonySustenanceDB), power, mining rate + deposit depletion, industry line job statuses, cargo mass delta; per faction Ledger totals by TransactionCategory + balance start->end. Assert structural truths only (ran for all hosts; ledger non-empty) — no balance numbers. Write TestResults/self-sufficiency-readout.txt. This fixture would have caught the Kithrin drain on day one.` },
    { key: 'P0.3', title: 'ci.yml readout-cat step', prompt: `
Read .github/workflows/ci.yml fully. Add ONE step to the test job after "Build & run tests", with if: always(), that cats TestResults/*-readout.txt into the job log (guard for none existing). Minimal diff; do not touch shard filters. NOTE: ci.yml is frozen after this phase (campaign rule).` },
    { key: 'P0.4', title: 'Honest station-aware tape + stale-doc fixes', prompt: `
Read ${F}/A6-faction-development.md (Q4). AIDecisionRecorder.cs:46 records FactionRollup.ColonyCount (Colonies only) — the Kithrin tape read "colonies 0" while owning Titan station. Add a stations count to the record + PlanReadout.DecisionLine (save-safe: [JsonProperty], appended fields). Fix the stale "monthly Tick" comment (AIDecisionRecorder.cs:7) and the Factions/CLAUDE.md "RunFrequency 30d" claim (the processor is DAILY, NPCDecisionProcessor.cs:30-31). Extend an existing recorder test.` },
    { key: 'P0.5', title: 'Latent concurrency fixes (engine)', prompt: `
Read ${F}/A1-freeze.md (H2/H3 + fix seams). Two real latent bugs, fix both:
(a) GameEngine/Engine/DataStructures/SafeDictionary.cs is a plain Monitor that raises ItemAdded/ItemRemoved/OnChange handlers WHILE HOLDING _lock (:95-128) — change to copy-then-notify (capture handler+args under lock, invoke after release). FIRST grep every subscriber of those events and confirm none depends on under-lock semantics; list each subscriber + verdict.
(b) GameEngine/Engine/Entities/EntityManager.cs:552-561 GetAllEntitiesWithDataBlob enumerates the LIVE _entities.Values outside the lock — add a ValuesSnapshot (List copy under lock, the GetEnumerator pattern at SafeDictionary.cs:158-166) and use it; audit other live .Values/.Keys enumerations and fix same-exposure sites (list each + verdict). Perf note: runs 720x/game-hour/system — keep the copy a cheap List ctor.
Also fix the stale ReaderWriterLockSlim claims in GameEngine/CLAUDE.md. Add a focused re-entrancy unit test for (a).` },
  ]},

  // P3 batches: P3.1 (FactionFactory + umf.json) and P3.2 (Colonies/Legitimacy*) are
  // disjoint — parallel. P3.3 (ObjectiveTransition + Factions/CLAUDE.md, which P3.1 also
  // rows) runs alone; P3.4 builds ON P3.3's release logic — last.
  P3: { lane: 'CORE', title: 'STRATEGIC COHERENCE', batches: [[0, 1], [2], [3]], slices: [
    { key: 'P3.1', title: 'UMF government node + Ceres factory (data)', prompt: `
Read ${F}/A3-objective-flip.md (fix seam 1) + ${F}/A6-faction-development.md (Ceres factory x0, umf.json:511-513).
(a) FIRST verify whether FactionFactory.LoadFromJson parses a "government" node (grep). If not, add a parser mirroring the "personality" node pattern (byte-identical when absent). Then author the node in GameData/basemod/ScenarioFiles/umf.json: Militarism High + fitting Authority — per GovernmentDB.cs:86 WarMoraleFactor High=+0.5 so the war legitimacy term becomes +10 instead of the all-Mid default's -5 (a war-winning militarist takes pride).
(b) Add ONE factory to Ceres in umf.json (it can mine+refine but cannot manufacture — the DEV lane's data request; umf.json is CORE-owned).
Tests: UMF loads Militarism High; a militarist-at-war legitimacy war term is positive; Ceres hosts a manufacturing-capable line (BaseModIntegrity-style scenario check).` },
    { key: 'P3.2', title: 'Rebellion debounce + fresh-morale legitimacy', prompt: `
Read ${F}/A3-objective-flip.md (root-cause chain + seams 2-3). Two engine fixes in GameEngine/Colonies/:
(a) LegitimacyProcessor.UpdateRebellion (:97-112) starts a rebellion on ONE IsCollapsing sample — require 2 consecutive monthly collapsing reads (persist the counter save-safely: [JsonProperty] + deep-copy on LegitimacyDB or RebellionDB).
(b) Kill the one-cycle stale-morale echo (legit printed 15 a month AFTER morale recovered): verify ManagerSubPulse ordering between same-frequency hotloops FIRST; then either order legitimacy after population on the shared 30d boundary, or have RecalcLegitimacy read the SAME pure morale computation PopulationProcessor uses this cycle, or smooth legit rate-of-change. Choose the least invasive VERIFIED option; justify.
Tests: a one-month transient morale dip does NOT produce a legit cliff next month and does NOT start a rebellion; a sustained collapse still does both.` },
    { key: 'P3.3', title: 'Hysteresis break-glass', prompt: `
Read ${F}/A3-objective-flip.md (hysteresis section) + GameEngine/Factions/ObjectiveTransition.cs. Three coordinated changes:
(a) crisis objectives (committed at Survive tier) get shorter CommitFor than 180d (suggest 60d — FLAGGED);
(b) release the commit early when the TRIGGERING condition clears (record why committed — rebellion/legit-crisis — and re-check in ShouldReplan);
(c) persistent-contradiction release: N consecutive cycles proposing a HIGHER tier than committed unlocks a replan (suggest 14 daily reads — FLAGGED).
CRITICAL BUG TO FIX WHILE THERE: the commit is stamped at Tier=Survive (enum 0, the floor, NPCDecisionProcessor.cs:341) so the existing proposedTier<currentTier break-glass is mathematically unreachable — your (b)/(c) must not share that flaw. Non-crisis (Conquer/Expand ambition-scaled) behavior unchanged. Save-safe state. Tests: the log's exact scenario (1-month rebellion -> quell -> daily Conquer reads) returns to Conquer within days; a sustained crisis still holds Defend.` },
    { key: 'P3.4', title: 'Operation continuity — never orphan an invasion', prompt: `
Read ${F}/A3-objective-flip.md (§D commitment gap + seam 5). Minimal continuity in CORE-owned resolvers:
(a) protect a WINNING in-flight conquest from TRANSIENT internal wobble: strike fleet en route (ConquerResolver.FleetIsMoving) + atWarAndWinning + the Survive read caused only by internal factors (not losing the war, homeland not invaded) => the Conquer commit survives (wire through P3.3's release logic, not around it);
(b) a GENUINE flip to Defend actively RECALLS in-flight offensive fleets: DefendResolver issues a return MoveToSystemBodyOrder to the home colony body (verify the order signature in source) instead of only re-posturing;
(c) leave arrival-under-Conquer behavior as-is (the bombard rung lands in PW).
Tests drive resolvers directly (no sim advance): transient-wobble keeps Conquer; genuine-Defend emits the recall order for the moving fleet.` },
  ]},

  // P4 batches: P4.1 (ConquerResolver) / P4.2 (Ships + Industry) / P4.3 (umf.json data +
  // its own test) are file-disjoint — parallel. P4.4 (the e2e gauge exercising all three)
  // runs after.
  P4: { lane: 'CORE', title: 'SEALIFT', batches: [[0, 1, 2], [3]], slices: [
    { key: 'P4.1', title: 'Rung-2 already-queued guard', prompt: `
Read ${F}/A4-sealift.md (cause 1). ConquerResolver Rung 2 (ConquerResolver.cs:241-271) queues a transport on EVERY free line (gate is only !FactionOwnsTransport) — the log shows 4 redundant heavy transports strangling Mars. Add "and none already queued": scan the faction's production lines for an existing job of the transport design (find the real job-read API in Industry/ by source). Test: resolver twice on a free-line colony — first queues, second falls through to the next rung.` },
    { key: 'P4.2', title: 'Charge + fuel BUILT hulls', prompt: `
Read ${F}/A4-sealift.md (cause 3) + GameEngine/Combat/CLAUDE.md spawn-parity rule. ShipFactory.ChargeReactors/FillFuelTanks are called by the scenario loader (FactionFactory.cs ~:398) and CombatSandbox but NEVER on the industry build path. Wire both at ShipDesign.OnConstructionComplete (:116 CreateShip branch) AND LaunchComplexProcessor.TryLaunchShip (:117 after positioning). DEVELOPER DECISION to honor: "a production ship earns its charge over game-time" was deliberate for the player — implement behind a static policy flag (default: ON for NPC-owned, OFF for player-owned; both FLAGGED) and write the decision to the lane notes. Extend NpcFleetReadyToSailTests with a built-ship sibling assertion.` },
    { key: 'P4.3', title: 'Launch fuel + pad tonnage (data + audit)', prompt: `
Read ${F}/A4-sealift.md (cause 2). Mars builds through a launch complex gated on colony fuel (LaunchComplexProcessor.cs:111-170) — umf.json Mars cargo has NO fuel. (a) Identify the exact fuel id TryDeductFuel deducts (source + GameData) and add starting stock to Mars in umf.json; audit every UMF colony + (write a REQUEST to DEV lane for kithrin.json) for the same trap. (b) Compute the Lander Troop Transport's summed mass from GameData component masses vs the pad MaxTonnage 100,000 kg (:58); if over, raise the authored pad tonnage or author a lighter transport design — justify, FLAG numbers. Test: scenario check that every colony owning a launch complex stocks its launch fuel.` },
    { key: 'P4.4', title: 'End-to-end sealift gauge', prompt: `
Read ${F}/A4-sealift.md (CI gap — ConquerResolverTests hand-place transports, bypassing every link that died). Build a fixture driving BUILD -> LAUNCH -> LOAD -> SAIL emission through the REAL paths on DevTest Mars: queue via the resolver, advance the real sim bounded ([Timeout]) until the transport exists as an owned in-system entity, assert the LOAD rung finds it and SAIL can emit (charged per P4.2). If too slow, drive IndustryProcessor/LaunchComplexProcessor ticks through their real entry points — never hand-built entities.` },
  ]},

  // ---------------------------------------------------------------- GROUND --
  G1: { lane: 'GROUND', title: 'BEACHHEAD — combat engineer + colony-free build', slices: [
    { key: 'G1.1', title: 'GroundConstructorAtb + surface parts haulage', prompt: `
Read ${F}/A5-ground-campaign.md (Step 3, the big gap) + ${F}/R3-docs-directives.md (FOB reconciliation note) + GameEngine/GroundCombat/CLAUDE.md LOCKED PRINCIPLES.
(a) New GroundConstructorAtb — a ground-mountable constructor component (build-rate dial), full six-point base-mod registration (installations.json template + componentDesigns.json + earth.json ComponentDesigns/StartingItems; exact-arity atb args). A ground chassis carrying it = a combat engineer (CONVENTIONS §6 — component, never a unit type).
(b) Surface parts haulage: pick the least-invasive VERIFIED option after reading both candidates — extend GroundTransportDB with a parts manifest, or a per-region surface parts pool on GroundForcesDB — landing crated ComponentInstance parts onto the surface. Save-safe ([JsonProperty] + deep-copy). Tests for both halves (atb binds from JSON — the gotcha-10 sensor; parts land and are readable).` },
    { key: 'G1.2', title: 'Colony-free on-site ground build + FOB role', prompt: `
Continue from G1.1. Read ${F}/R4-infrastructure-combat.md (data model — Region/GroundHex InstallationIds, roll-up invariant) + ${F}/R3-docs-directives.md (FOB = staging-anchor role framing).
A landed unit whose design mounts GroundConstructorAtb + available surface parts BUILDS a footprint building into ITS friendly-held region: consume the parts, tick build-time inside GroundForcesProcessor's existing tick (L9 — a step like GroundUpkeep, no new hotloop), then place via the SAME primitives GroundBuildings/CityBuilder use (GroundHex.InstallationIds + tile roll-up + Region.InstallationIds) — NOTE: with no colony there is no ComponentInstancesDB host; solve ownership the verified least-invasive way (e.g. a minimal invader outpost record or attach to the invader's nearest own colony store) and DOCUMENT the choice + alternatives in the lane notes as a developer decision. The placed bunker fortifies (GroundDefenseAtb path) and marks the region a resupply point (consumed in G2). Byte-identity: inert until an engineer unit exists and lands. Tests: engineer + parts on held region builds over ticks; no parts / enemy ground => no build; fortification readable after.` },
  ]},

  G2: { lane: 'GROUND', title: 'BATTALION ENGINE — form-up, TACTICAL BRAIN, sustainment', slices: [
    { key: 'G2.1', title: 'FormUpLoose — AI formation parity', prompt: `
Read ${F}/R2-player-unit-chain.md (gap 3: the AI NEVER forms — CreateFormation's only non-test caller is the player UI) + GameEngine/GroundCombat/GroundForcesDB.cs formation API.
Build GroundAssembly.FormUpLoose(body, factionId, name=null): sweep the faction's formation-less units on a body into a battalion (CreateFormation + AssignUnit; sensible size cap from FactionInfoDB.GarrisonComposition — FLAGGED; multiple battalions if over cap; deterministic order). Pure engine helper — callable by garrison-raise, the landing path, and the CORE lane's resolver (they wire it in PW; write that request to lane notes). Call it yourself at the two GROUND-owned sites: GroundStartGarrison raise and GroundTransport.TryLandUnit landings (verify both by source). Also: RenameFormation(formation, name) + GroundFormationTools.AllFormationsFor(game, factionId) + GroundSensors.RadarReachHexes(body, unit) — the manager micro-helpers CLIENT needs (R1 findings). Tests: raise -> battalions formed per composition; landing -> invader formed; rename works; AllFormationsFor enumerates across bodies.` },
    { key: 'G2.2', title: 'THE GROUND TACTICAL BRAIN — GroundThreat + GroundTactics + the wire (per the locked design)', prompt: `
Read docs/earthfall/GROUND-TACTICAL-AI-DESIGN.md IN FULL — it IS this slice's design (the decision model §2, the dependency web §3, the build spec §4) and it answers the developer's question "is the AI smart enough to know when to be defensive vs offensive." Implement its build spec exactly, in order:
(a) G2.2a — GroundThreat (NEW file): the FOG-HONEST enemy-strength read, own battalion strength (GroundFormationTools.FormationStrength) vs DETECTED enemy strength in the battalion's region + adjacent regions via the per-faction ground fog reveal API (verify by source; an UNDETECTED enemy counts ZERO — the space undetected-clears rule). This is the open fog "slice 5" promoted to a requirement; PW also consumes it for the landing score.
(b) G2.2b — GroundTactics.DecidePosture(ctx) (NEW file): pure + deterministic -> { Stance, Roe, Intent(Advance/Hold/PullBack/Retreat), Reason }. Inputs + rules exactly per the design doc §2 table (odds via the CombatRisk.RequiredStrengthRatio curve — READ-ONLY cross-fence reference, never edit Factions/; attacker-vs-homeland-defender role; orbital support; fortification + terrain; reserve; ammo fraction; fallback destination = nearest friendly region / G1 beachhead anchor; blind => cautious; dry ammo => never Offensive; cornered => dig in, no suicide moves). Posture hysteresis WITH a built-in break-glass (§2 last para — never a time-lock without a release). All thresholds FLAGGED.
(c) G2.2c — the wire in GroundForcesProcessor's existing tick (L9 — a step, NO new hotloop) behind NEW static flag EnableGroundTacticalAI (default OFF; CORE flips it in PW): per AI-owned battalion apply Stance (TrySetStance, respect its cooldown), ROE (SetEngagementStance), Intent via the order queue (Advance = QueueFormationOrder MoveRegion toward the nearest adjacent enemy-held region, verify the PlanetRegionsDB adjacency read by source, deterministic id tiebreaks, stop when all held; PullBack/Retreat = MoveRegion toward the chosen friendly region/beachhead). ORDER OWNERSHIP (design §3.5): GroundOrder gains a save-safe issuer marker ([JsonProperty] + deep-copy) so the brain replaces only ITS OWN orders — a PLAYER order queue ALWAYS overrides. Record each decision's Reason (no decision without its explain; CLIENT shows own battalions' Reason — note the request).
Tests — the design doc §4 acceptance gauges verbatim: outnumbered fortified defender => Defensive+Hold, does NOT advance; attacker at 2:1 => Offensive+Close+Advance and takes a small multi-region world end-to-end; losing 1:4 with friendly ground behind => Retreat toward it, cornered => digs in; parity => Balanced+StandOff; bold faction commits at odds a cautious one refuses (the curve bites); blind => cautious; player order suppresses the brain; flag-off byte-identical.` },
    { key: 'G2.3', title: 'Ammo drain + resupply + upkeep values', prompt: `
Read ${F}/A5-ground-campaign.md (Steps 4/6). Three wires:
(a) ResolveRegionCombat calls GroundAmmo.Consume per firing unit with an ammo-mode weapon (read GroundAmmo.cs deferred-drain notes first; per-salvo kg FLAGGED); dry => that weapon silent via existing IsDry semantics.
(b) Resupply caller in the ground tick: a unit on friendly-held ground with a G1 beachhead/depot structure or a home-colony region auto-resupplies via GroundForces.ResupplyUnit (currently caller-less).
(c) Set GroundUnitDesign.UpkeepCredits at GroundUnitAssembly.ToGroundUnitDesign + GroundStartGarrison (mass-scaled suggestion, FLAGGED) so the standing-army biller finally bills.
Keep GroundAmmoTests/GroundUpkeepTests green (extend, don't rewrite). Tests for each wire.` },
  ]},

  G3: { lane: 'GROUND', title: 'INFRASTRUCTURE COMBAT — destroy/capture orders', slices: [
    { key: 'G3.1', title: 'Order types + processor cases', prompt: `
Read ${F}/R4-infrastructure-combat.md IN FULL — it is the design. Implement exactly:
(a) APPEND GroundOrderType.DestroyInfrastructure=5, CaptureInfrastructure=6 (GroundForcesDB.cs:245; ordinal-stable, never reorder); reuse GroundOrder.TargetQ/TargetR/TargetRegion; factory statics + Describe cases.
(b) ProcessFormationOrders cases (GroundForcesProcessor.cs:498 switch, kick-off-once/done/pop pattern, never wedge): DESTROY — gate on the resolver's own range check (HexDist <= unit.Range, per-region coords; footprints default to region (0,0)), then staged drain via GroundBuildings.BombardHex with Attack-scaled strength (scaling constant FLAGGED; staged-drain default per the ledger); done when the hex has no footprint buildings or budget exhausted. CAPTURE — gate on range/hold, set hex.OwnerFactionID = formation faction; INSTANT capture v1 (contested-timer = developer decision, note it).
(c) First hex-owner consumers so the field stops being inert: captured-hex buildings stop counting toward the DEFENDER's fortification (GroundFortification reads — verify path) and the capture is visible in a readout. The produce-for-captor economy transfer = developer decision (Aurora "intact installations produce for the conqueror"); write the 6 open decisions from the ledger to lane notes.
Tests: in-range destroy removes buildings through the real removal path (economy + tile roll-up intact — assert the CityBuilder invariant); out-of-range order does not fire and pops cleanly; capture flips hex owner + fortification effect.` },
  ]},

  G4: { lane: 'GROUND', title: 'SEALED COMPONENT — the last marine blocker', slices: [
    { key: 'G4.1', title: 'Environmental-seal component + assembler wire', prompt: `
Read ${F}/R2-player-unit-chain.md (gap 1) + GameEngine/GroundCombat/CLAUDE.md (PlanetEnvironmentFactory row: "the buildable SEALED COMPONENT ... is the next slice") + GroundUnitAssembly.ToGroundUnitDesign.
(a) New GroundSealAtb (or extend GroundAugmentAtb ONLY if verified cleaner — justify): dials for sealing fraction vs Vacuum + ToxicAtmosphere (0..1 each, or one Sealing dial covering both — pick + justify; mass scales with sealing, FLAGGED). Six-point base-mod registration ("sealed-systems" template).
(b) Wire EnvironmentalResistance at GroundUnitAssembly.ToGroundUnitDesign: best mounted seal writes {Vacuum: x, ToxicAtmosphere: x} into the design's EnvironmentalResistance so RaiseUnit snapshots it (the existing E4 counter path).
(c) Expose assembler readout data engine-side (training multiplier + power/ammo supply-vs-demand numbers already computed in GroundUnitAssemblyResult — verify what CLIENT needs per ${F}/R2 flags; add public getters if missing).
(d) TALENT SCARCITY PARITY ("you can't mass-produce Space Marines"): the ship-side elite stamp (UnitCaliberAtb) draws from the scarce talent pool via ManpowerTools (verify HasTalentToBuild/CommitTalent by source), but the ground Training Cadre draws nothing — building a cadre-equipped ground unit should COMMIT talent the same way (draw size FLAGGED; a colony out of talent cannot field another veteran battalion; release on unit death mirrors the ship path if one exists — verify). Byte-conscious: units without a cadre draw nothing.
Tests: JSON->atb binding (gotcha-10 sensor); an assembled sealed unit survives the vacuum world an unsealed twin bleeds on (extend the existing GroundForcesTests vacuum gauges); cadre build commits talent and an exhausted pool blocks the next veteran.` },
  ]},

  // ---------------------------------------------------------------- CLIENT --
  C1: { lane: 'CLIENT', title: 'FREEZE FIX (client)', slices: [
    { key: 'C1.1', title: 'Blip cull + render breadcrumbs + watchdog', prompt: `
Read ${F}/A1-freeze.md (H1 + fix seams) + Pulsar4X.Client/CLAUDE.md gotcha #15 (orbit-icon cull precedent).
(a) Finite-coordinate + on-screen-size cull for contact blips (SensorContactIcon / SystemMapRendering.UpdateContactBlips / SafeDraw path) mirroring the OrbitEllipseIcon cull, so a degenerate blip coordinate can never reach SDL/ImGui natively.
(b) Per-window "last render stage" breadcrumb (static string set before each window draw, included in [HANG] watchdog output + SessionLog flush) so a native hang names its stage.
(c) Modestly lower the hang-watchdog threshold if cheap.
CLIENT-only: CI compiles it; add precise runtime-check entries to docs/CLIENT-TEST-CHECKLIST.md (play past 2050-04-22 with a fleet mid-transit; no freeze; breadcrumb visible). Update Pulsar4X.Client/CLAUDE.md.` },
  ]},

  C2: { lane: 'CLIENT', title: 'SENSOR TRUTH', slices: [
    { key: 'C2.1', title: 'Honest ring + held-vs-fresh + coverage tests', prompt: `
Read ${F}/A2-ghost-contacts.md IN FULL.
(a) The colony detection ring (SystemMapRendering.cs:874-878) under-draws (sized vs ONE reference target): make a detected contact never render outside the drawn reach (size vs the actual contact class or draw a min/max band).
(b) Split the [DETECT] readout (SessionLog.cs:352): held vs fresh (LastDetection within ~2 scan intervals). Do NOT change the hostile-gate gameplay; add the recency policy behind a default-off static + note the developer decision.
(c) Close the engine coverage gaps with NEW test files: the Sensors->Memory lost-track flip and ContactStaleSeconds age-out actually firing (drive SensorScan with a target moved out of range).
(d) Write docs/combat/HOMEWORLD-SENSOR-HORIZON-MEMO.md (the 200 Gm design call: keep+honest-readouts [default] / signature-curve / EMCON-gate, each with the file:line dial + gameplay consequence; NO value change). Lane notes: DOCS-INDEX row pending.` },
  ]},

  C3: { lane: 'CLIENT', title: 'FORCE MANAGEMENT — battalions beside fleets', slices: [
    { key: 'C3.1', title: 'The combined manager window', prompt: `
Read ${F}/R1-manager-and-rings.md IN FULL (anatomy + gaps + precedent) + ${F}/R2-player-unit-chain.md (§4/§5 the working per-body panel).
In FleetWindow (KEEP the class name — retitle the window "Force Management"; developer may rename later, list the R1 candidates in lane notes):
(a) A "Battalions" section/tab: enumerate ALL the player's ground formations across bodies — _uiState.StarSystemStates -> each system GetAllEntitiesWithDataBlob<GroundForcesDB>() -> GroundFormationTools.FormationsFor (pure client, the R1-verified pattern; use AllFormationsFor if GROUND's helper is merged, else client-side).
(b) Columns: name, body, region (LeaderRegion), strength/health (FormationStrength/FormationHealth), reach (FormationReachHexes), stance/ROE. Filters: system / body / has-orders.
(c) Selection -> order surface reusing PlanetViewWindow.DrawFormationPanel idioms: march to region (OrderFormationMove), queue waypoints (QueueFormationOrder), stance (TrySetStance), ROE (SetEngagementStance) — all direct calls, verified signatures.
(d) Selecting a battalion can jump the PlanetViewWindow to that body (verify the open-window-for-entity mechanic in GlobalUIState).
Rename button deferred to PW (needs GROUND's RenameFormation). CLIENT-TEST-CHECKLIST entries; Pulsar4X.Client/CLAUDE.md row.` },
  ]},

  C4: { lane: 'CLIENT', title: 'PLANETARY RANGE OVERLAY + assembler readout rows', slices: [
    { key: 'C4.1', title: 'Hex-highlight range overlay + assembler rows', prompt: `
Read ${F}/R1-manager-and-rings.md (B3/B4 — exact insertion + precedents) + ${F}/R2-player-unit-chain.md (flags).
(a) In PlanetViewWindow.DrawGlobalHexWindow: a highlight pass after the terrain loop (~:344), before unit tokens (~:361) — for the selected unit/formation tint hexes within weapon reach (GroundUnit.Range / FormationReachHexes; red) and radar reach (GroundSensorAtb.Range_km ÷ GroundRangeTools.HexPitchKm, the GroundSensors.cs:42 conversion — use RadarReachHexes if merged; green). Hex distance via Colonies.HexCoordinate + the file's WrapDelta; drawList.AddNgon/AddNgonFilled low-alpha (the selection-ring/deposit-overlay idioms). Toggle mirroring GlobalUIState.ShowAllRangeRings. Fog-honest (respect the survey gates the window already uses).
(b) Entity Assembler readout: add the missing rows — Training multiplier, and ALWAYS-ON power supply-vs-demand + ammo capacity rows (today only violation text) — reading GroundUnitAssembly.Compute results (verify member names; if engine getters are missing, note the request for GROUND G4c).
CLIENT-TEST-CHECKLIST entries for both.` },
  ]},

  C5: { lane: 'CLIENT', title: 'INVADE-FROM-ORBIT UI (MVP Stage 4)', slices: [
    { key: 'C5.1', title: 'Embark + land buttons', prompt: `
Read ${F}/R2-player-unit-chain.md (§6 — LoadTroopsOrder/LandTroopsOrder have ZERO client surface; MVP calls this "the missing control panel", the one true remaining v1 gap).
Add the player path: from the Force Management window (and/or the orbiting ship's context) — a troop-bay ship at a friendly garrisoned body lists loadable units => LoadTroopsOrder.CreateCommand via OrderHandler (the FleetWindow Issue-Orders idiom, R1 §A1); a loaded transport holding orbit at a target body offers "Land troops" with a REGION picker => LandTroopsOrder.CreateCommand (order carries RegionIndex — verified). Show bay capacity vs unit size. Verify both order signatures in source (LoadTroopsOrder.cs:35 CreateCommand(ship, body, unitId)). Infra Destroy/Capture buttons are NOT in this slice (PW — needs GROUND's enum). CLIENT-TEST-CHECKLIST: the full player invasion click-path (embark Earth marines -> land on Mars).` },
  ]},

  // ------------------------------------------------------------------- DEV --
  D1: { lane: 'DEV', title: 'KITHRIN SURVEY CHAIN', slices: [
    { key: 'D1.1', title: 'Survey emission + surveyor + fallback build', prompt: `
Read ${F}/A6-faction-development.md (Q3/Q5 — dead at three rungs). Fix all three:
(a) DATA: add a kithrin-ship-sable (registered surveyor, shipDesigns.json:964-981) to the Titan Hive Guard fleet in kithrin.json.
(b) ExpandResolver's survey leg (ExpandResolver.cs:73-74, currently Execute=null): emit a real GeoSurveyOrder for the nearest unsurveyed colonizeable body when the faction owns an idle survey-capable ship (verify GeoSurveyAbilityDB read + GeoSurveyOrder.CreateCommand signature :84).
(c) Fallback rung: no surveyor owned => queue ONE surveyor build on a free line (the P4.1 already-queued guard pattern — implement independently here; CORE owns ConquerResolver, you own ExpandResolver).
Tests: resolver-driven — Kithrin with a Sable emit the survey order; drive GeoSurveyProcessor to completion => next cycle emits CreateColonyOrder (the FOUND rung :59-69 already works). The task-#35 gauge.` },
  ]},

  D2: { lane: 'DEV', title: 'STATION INCOME + CONSOLIDATE AGENCY', slices: [
    { key: 'D2.1', title: 'End the structural bankruptcy', prompt: `
Read ${F}/A6-faction-development.md (Q4 — station bills ~6,880/mo, income ZERO; no station-tax processor exists).
(a) Minimal honest station income: a populated station with industry generates monthly tax/trade income — either extend StationUpkeepProcessor into a net-operating pass or a new processor keyed to a blob no hotloop owns (L9 check FIRST). Rate from the existing taxRate model; Kithrin have no strain node so default modestly (FLAGGED). The player owns stations too — state the balance impact, keep coefficients conservative.
(b) ConsolidateResolver skips stations (requires ColonyEconomyDB, :48): give it a station-legal step (read StationEconomyDB, or fall through to the host-agnostic GrowEconomy build rungs) so Consolidate is never a guaranteed no-op for a station faction.
Tests: a populated industrial station's faction balance no longer decays monotonically (ledger shows the income category); a station-only faction under Consolidate emits a real action.` },
  ]},

  D3: { lane: 'DEV', title: 'COLONIZATION GAUGE', slices: [
    { key: 'D3.1', title: 'AI expand end-to-end', prompt: `
Building on D1/D2: a CI gauge that drives the WHOLE Kithrin expand arc through real paths on the DevTest sandbox (bounded, [Timeout]): survey order emitted -> survey completes -> colony founded (CreateColonyOrder -> ColonyFactory) -> the new colony appears in FactionInfoDB.Colonies and pays tax next cycle. Milestone-named assertion messages. [Ignore] any milestone that cannot pass yet with the reason (never a red test).` },
  ]},

  // ------------------------------------------------------------------ TWOD --
  T0: { lane: 'TWOD', title: '2D JOINTS MEMO', slices: [
    { key: 'T0.1', title: 'Pin fire-allocation + theater cadence', prompt: `
Read docs/combat/RESOLVER-2D-GROUP-PLANE-DESIGN.md §11. Write docs/combat/RESOLVER-2D-JOINTS.md pinning BOTH joints with concrete math + pseudocode: (1) CONSERVED FIRE-ALLOCATION — one DPS pool per group split target-weighted across in-range enemies (weights, rounding, deterministic tiebreaks; total dealt == total owned; 3-way FFA worked example); (2) COMBINED-THEATER CADENCE — BattleTheater owns stepping a ground plane at 5s during a combined battle (the GroundForcesProcessor handoff/return contract; fixed-quantum fast-forward==watch proof). These GATE later slices S5/S6. Lane notes: DOCS-INDEX row pending.` },
  ]},
  T1: { lane: 'TWOD', title: '2D S0 — GroupPlane pure', slices: [
    { key: 'T1.1', title: 'GroupPlane.cs + tests', prompt: `
Per docs/combat/RESOLVER-2D-GROUP-PLANE-DESIGN.md §13 S0: GameEngine/Combat/GroupPlane.cs pure static — seed a battle-local frame from 3D positions (deterministic basis: lowest-entity-id rule, degenerate fallback), role offset from (bearingDeg, alongStandoff, perpSpread) vs a deterministic enemyDir (id tiebreak), pair distance. NOTHING calls it. GroupPlaneTests: frame determinism; 2-side collapse to 1-D (second axis ~0); offset trig; joiner projected with the STORED frame; tiebreak stability. Byte-identical by construction.` },
  ]},
  T2: { lane: 'TWOD', title: '2D S1 — anchors behind flag', slices: [
    { key: 'T2.1', title: 'FleetCombatStateDB anchors + 2D closing', prompt: `
Per §13 S1: add Anchor/Frame/GroupPositions to FleetCombatStateDB ([JsonProperty] + Clone), seed from real positions at engagement start (copy frame to joiners), generalize AdvanceClosing to move anchors along enemyDir in 2D — ALL behind new static EnableGroupPlane (default false). Flag-off degrades EXACTLY to scalar Separation_m (verify by reading every flag-off path; the existing closing/combat fixtures are the tripwire). Tests: flag-on seeding matches GroupPlane math; flag-off state identical.` },
  ]},
  T3: { lane: 'TWOD', title: '2D S2 — 2D range gate', slices: [
    { key: 'T3.1', title: 'Pair distance feeds the gate', prompt: `
Per §13 S2: flag-on, WithinWeaponRange/SeparationOf compute the 2D group-pair distance (per-sub-fleet gaps real); kernel stays 1-D (receives scalar d); flag-off byte-identical. Tests: a long-range group fires a short-range group that cannot answer (directed gate) on the plane; flag-off fixtures unaffected. Do NOT start S3+ (later campaign).` },
  ]},

  // ------------------------------------------------------- POST-MERGE CORE --
  // PW batches: PW.1 (Factions resolver files) and PW.2 (client windows + notes sweep)
  // are file-disjoint — parallel.
  PW: { lane: 'CORE', title: 'WIRING — resolver rungs onto merged capabilities', batches: [[0, 1]], slices: [
    { key: 'PW.1', title: 'Conquer rungs: bombard, landing score, beachhead, campaign kickoff', prompt: `
Prerequisite: GROUND lane merged. Read ${F}/A5-ground-campaign.md (minimal-build list) + ${F}/R3-docs-directives.md (easiest-landing score spec) + docs/earthfall/GROUND-TACTICAL-AI-DESIGN.md §3.8 (the strategy->tactics seam) + the merged GroundCombat surface (grep the new helpers: FormUpLoose, GroundThreat, GroundTactics, EnableGroundTacticalAI, the beachhead build step, the infra order types).
In ConquerResolver (CORE-owned): (a) BOMBARD rung between STRIKE and LAND — orbiting fleet + defending garrison outguns the landing force (read via GroundThreat, fog-honest) => emit the bombardment through the real order/fire path (verify what exists; if the fleet resolve cannot target colonies, fire the bounded OnTakingDamage entry through an ORDER, the GroundBombardmentTests shape). ONGOING FIRE SUPPORT: the rung may RE-FIRE on later cycles while the defending garrison still outguns the landed force — ApplyGroundBombardment is defender-only (verified friendly-fire guard), so own landed troops are safe; cadence + cap FLAGGED; (b) LANDING-REGION choice replacing the hardcoded 0 (ConquerResolver.cs:62) using the easiest-landing score with GroundThreat as its garrison-strength input (fog-honest; deterministic tiebreak); (c) BEACHHEAD rung — after a region is held, land engineer + parts, invoke the G1 build; (d) BRAIN kickoff — flip EnableGroundTacticalAI for menu/DevTest games (beside the other AI gates) + ensure FormUpLoose runs on landings the resolver drives; (e) INFRA tasking rung — queue DestroyInfrastructure against fortifying enemy buildings in reach, gated on the battalion's brain posture being Offensive (the design's stance-as-gate rule; FLAGGED aggression policy).
Resolver-driven tests for each rung (the ConquerResolverTests idiom), including re-fire (strong garrison persists => second bombardment emitted; softened => the rung stops).` },
    { key: 'PW.2', title: 'Client cross-lane buttons + request resolution', prompt: `
Prerequisites: GROUND + CLIENT merged. (a) Force Management: battalion RENAME button via GroundForces.RenameFormation; infra "Destroy/Capture infrastructure" order buttons (the G3 order types) in the battalion order surface + PlanetViewWindow city-tile inspect spot (R4 hooks :640/:1074). (b) Sweep every lane-notes REQUEST (docs/earthfall/LANE-*-NOTES.md) and resolve or escalate each to the developer list. CLIENT-TEST-CHECKLIST entries.` },
  ]},

  P8: { lane: 'CORE', title: 'EARTHFALL ACCEPTANCE', slices: [
    { key: 'P8.1', title: 'The campaign gauge + the Space-Marine gauge', prompt: `
Two capstone fixtures (bounded, [Timeout], readout files, milestone-named assertions, [Ignore] unpassable milestones with reasons — never red):
(a) OperationEarthfallTests: the WHOLE AI campaign through real paths on DevTest — Conquer held under transient wobble (P3) -> mass+sail -> ONE transport builds, launches fueled+charged (P4) -> bombard rung vs the strong garrison (incl. a between-waves RE-FIRE while it stays strong) -> land at the SCORED region -> FormUpLoose battalion -> THE TACTICAL BRAIN picks Offensive and advances region-to-region (G2) -> beachhead built by the engineer (G1) -> infra destroyed/captured where the brain's posture gates it (G3/PW) -> a SECOND WAVE loads+lands -> planet capture flips the colony; meanwhile Kithrin survey+found (D1-D3) and no faction balance decays structurally (D2). POSTURE ASSERTIONS (the brain visibly alive): when the defenders outnumber the landed force past the Risk bar, the invader battalion reads it and goes Defensive / withdraws toward its beachhead — assert the Stance/Roe/Intent + Reason, not just survival.
(b) SpaceMarineDefenseTests: the PLAYER chain — assemble a marine design in-engine exactly as the UI does (RegisterAssembledDesign: human-frame + power-armor + sealed-systems (G4) + training cadre + rifle + plating + ward), build+field via the real industry path, FormUp into a battalion, then a landed UMF invasion force attacks Earth: the marine battalion (stance/ROE set) defeats it — AND assert the UMF brain REACTS to the counterattack (outmatched => Defensive/withdraw, the exact moment that answers the developer's question); then embark the marines (LoadTroopsOrder) and land them on Mars (LandTroopsOrder) — the developer's declared scenario, both directions.
(c) MID-CAMPAIGN SAVE/LOAD: at a mid-invasion point (troops landed, battalion formed, brain posture set, beachhead placed, an infra hex captured, a brain-issued order queued), Game.Save -> Game.Load (the SaveLoadDesignRoundTripTests pattern) and assert the campaign state survives intact: formations + membership, the order queue INCLUDING the issuer marker, hex OwnerFactionID flips, beachhead building + surface parts, the rebellion-debounce counter + commit-reason state (P3), and (if TWOD merged) the battle-frame anchors — then advance a tick and assert the campaign CONTINUES (the brain re-decides without error).
Write TestResults/earthfall-readout.txt with the full campaign tape.` },
    { key: 'P8.2', title: 'Dashboard sync + final report', prompt: `
Sweep ALL docs/earthfall/LANE-*-NOTES.md pending rows into the real dashboards in ONE slice: DOCS-INDEX rows (new docs incl. the memos + this campaign folder + GROUND-TACTICAL-AI-DESIGN), TESTING-TRACKER rows (every new gauge), SYSTEM-CONNECTION-MAP (new wires: bombard rung, beachhead, TACTICAL BRAIN, infra combat, station income, survey chain, embark UI), subsystem CLAUDE.md rows any implementer missed (audit git log of all lanes), stale-claim retirements. THEN write TWO docs: (1) docs/ground/EARTHFALL-CAMPAIGN-OPS.md — the developer's "EACH INDIVIDUAL STEP AND CLICK" deliverable: the complete invasion flow as BUILT, from BOTH seats — the AI's rung sequence (mass -> sail -> bombard+re-fire -> scored landing -> form up -> brain posture -> advance -> beachhead -> infra -> second wave -> capture, with file anchors) and the PLAYER's actual windows/buttons for the same acts (design marine -> assemble -> build -> form battalion -> stance/ROE -> move/queue -> embark -> land -> destroy/capture infra), plus the support loop (ammo, resupply, upkeep, fire support) and the honest LIMITS list — plain English, shipboard analogies, per the root CLAUDE.md communication rules; (2) docs/earthfall/CAMPAIGN-REPORT.md: what shipped per lane, every developer decision awaiting an answer, deferred items (incl. orbital-strike-at-a-specific-hex, explicitly deferred by the developer), and the CLIENT-TEST-CHECKLIST items awaiting the developer's local runtime pass.` },
  ]},
}

// ============================================================================
// DISPATCH
// ============================================================================

const phaseKey = (args && args.phase) ? String(args.phase).toUpperCase() : null
if (!phaseKey || !PHASES[phaseKey]) {
  return { error: `Pass args.phase — one of: ${Object.keys(PHASES).join(', ')}. See docs/earthfall/CAMPAIGN-PLAN.md §3 for which lane/branch owns each phase.` }
}
const ph = PHASES[phaseKey]

phase('Design')
log(`EARTHFALL ${phaseKey} [lane ${ph.lane}] — ${ph.title} (${ph.slices.length} slice(s))`)
log('Operator: verify you are on the lane branch that owns this phase (CAMPAIGN-PLAN.md §3); commit slice-by-slice; gate CI green between phases.')

phase('Implement')
// PARALLEL BATCHES: a phase may declare `batches` — arrays of slice INDICES whose complete
// file sets (code + tests + the CLAUDE.md each updates) are DISJOINT by construction; those
// slices run CONCURRENTLY in the same working tree. Batches run sequentially. A phase
// without `batches` runs fully sequentially (the safe default).
const results = []
const groups = ph.batches ? ph.batches.map(ix => ix.map(i => ph.slices[i])) : ph.slices.map(s => [s])
for (const group of groups) {
  if (group.length === 1) { results.push(await runSlice(group[0])); continue }
  log('parallel batch: ' + group.map(s => s.key).join(' + '))
  const batchResults = await parallel(group.map(s => () => runSlice(s)))
  results.push(...batchResults.filter(Boolean))
}

phase('Synthesize')
const brief = await agent(
  COMMON + `\nYou are the ${phaseKey} phase reporter (lane ${ph.lane}). Read git status + git diff of the working tree and the slice results below. Write the operator's landing brief: recommended COMMIT SEQUENCE (one commit per slice, dependency order, one-line messages), engine vs client-only classification per commit, every DEVELOPER DECISION + cross-lane REQUEST raised (confirm each is in the lane notes file), and any cross-slice conflict visible in the diff. SLICE RESULTS: ${JSON.stringify(results)}`,
  { label: 'landing-brief', phase: 'Synthesize' })

return { phase: phaseKey, lane: ph.lane, title: ph.title, slices: results, landingBrief: brief }
