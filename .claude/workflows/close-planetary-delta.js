export const meta = {
  name: 'close-planetary-delta',
  description: 'Close the planetary FUNCTIONAL / ACCESSIBLE / OBSERVABLE delta — the slice-by-slice execution of docs/ground/PLANETARY-FUNCTIONAL-PLAN-2026-07-27.md. Observability first (the ground battle log), then reachability, then doctrine steering, then the tick + rate-fire, then the designer chain, then depth. ONE slice per invocation; the session commits and gates CI (~33 min) between slices.',
  whenToUse: 'Invoke with args {slice:"S0"|"S1"|"S1b"|"S2"|"S3"|"S4"|"S5"|"S6"|"D0"|"D2"|"D3a"|"D3b"|"S8"|"S9"|"S10"} (or {slice:["S1","S2"]} for file-disjoint slices) from the branch that owns the work. Requires docs/ground/PLANETARY-FUNCTIONAL-PLAN-2026-07-27.md + docs/DOCS-AUDIT-2026-07-27.md present. S12 (what capture transfers) is BLOCKED on a developer ruling and refuses to run. The developer must have answered the plan\'s Q2/Q3/Q4/Q5 before S3 and S8.',
  phases: [
    { title: 'Design' },
    { title: 'Implement' },
    { title: 'Verify' },
    { title: 'Docs' },
    { title: 'Synthesize' },
  ],
}

// ============================================================================
// CLOSE THE PLANETARY DELTA — self-contained for ANY executor session/model.
// Ground truth: docs/ground/PLANETARY-FUNCTIONAL-PLAN-2026-07-27.md (the plan +
// the delta ledger) and docs/DOCS-AUDIT-2026-07-27.md (the file:line evidence,
// incl. which findings are VERIFIED [V] vs inherited-unverified [A24] vs [?]).
// Every prompt cites repo-relative paths only.
// ============================================================================

const PLAN = 'docs/ground/PLANETARY-FUNCTIONAL-PLAN-2026-07-27.md'
const EVID = 'docs/DOCS-AUDIT-2026-07-27.md'
const RULINGS = 'docs/ground/GROUND-GAMEPLAY-DECISIONS-2026-07-24.md'
const BOARD = 'docs/ground/GROUND-SURFACE-MAP-DESIGN.md'

const COMMON = `
You are one implementation slice of CLOSE THE PLANETARY DELTA. Before ANYTHING else read, in order:
  1. the repo root CLAUDE.md — the six-step pre-flight, the Landmine Index (L1-L11), the Prime Directive
     (map every connection first), the Visibility Gate ("can we see enough?"), and the communication rules
     (the developer is a US Navy nuclear-trained machinist's mate: plain English, define jargon, lead with
     the point, mechanical/shipboard analogies).
  2. ${PLAN} — your slice's row: its gauge, its reachability click-path, its observability criterion.
  3. ${EVID} — the file:line evidence. HEED THE EVIDENCE MARKERS: [V] was verified first-hand,
     [A24] is inherited from the 2026-07-24 audit and NOT re-verified, [?] is unverified. If your slice
     depends on an [A24] or [?] fact, VERIFY IT YOURSELF FIRST by reading the source, and say in your
     report what you found — a wrong inherited assumption is how a slice ships broken.
  4. ${RULINGS} — the 27 rulings. They OVERRIDE every other doc, including this script. If any instruction
     here contradicts a ruling, the RULING wins and you say so in your report.
  5. CONVENTIONS.md, plus the subsystem CLAUDE.md for every folder you touch.

HARD RULES (these are the ones that get broken):
- NO .NET SDK in this container: you CANNOT compile or run tests. Verify every type / member / namespace /
  arity / overload by grep+Read of the REAL source BEFORE you use it. A wrong member reds the CI shards for
  ~33 minutes. There is no "I'll let CI tell me."
- CI IS ~33 MINUTES, not 13. The 'rest' shard is the critical path and .github/workflows/ci.yml's
  complement filter puts EVERY NEW FIXTURE in it by default. If your new fixture advances the game clock or
  builds a scenario, say in your report whether it needs its own shard, and give the exact yml shape — do
  not silently make the slowest shard slower.
- Do NOT git commit, push, or switch branches. Leave edits in the working tree; the session lands each
  slice and gates CI.
- ONE hotloop processor per DataBlob (landmine L9): new ground steps go INSIDE GroundForcesProcessor, never
  in a new processor beside it.
- The JSON atb binder is EXACT-ARITY (GroundCombat gotcha 6): a new ctor dial means updating EVERY template
  that binds that atb IN THE SAME CHANGE, or every one fails to bind. Six-point registration (root gotcha
  #10) for any new buildable; BaseModIntegrityTests is the sensor; JSON drift crashes players, not tests.
- Renaming or moving any *DB breaks saves (TypeNameHandling.Objects). Prefer APPENDING fields to an
  existing blob. If you must add a blob, say so loudly and describe the migration.
- Determinism is locked: fast-forward must equal watch. No RNG in a resolver without a seeded,
  order-independent stream.
- Doctrine reciprocal trap: space ToughnessMult and ground DamageTakenMult are reciprocals — author exactly
  ONE per catalog entry. CombatDoctrine/UnifiedDoctrineTests guard it; keep them green.
- BYTE-IDENTITY: every behaviour change ships behind a default-OFF flag (off in CI, on for menu games), or
  is provably inert absent new data. State WHICH in your report and WHY.
- Every new gameplay number gets a "// FLAGGED balance value" comment. Never choose one silently.
- NUnit 3, and a UNIQUE new fixture filename. [Timeout] on anything that advances the clock.
- CI cannot RUN the client. Client-side work is compile-checked only — add a row to
  docs/CLIENT-TEST-CHECKLIST.md for the developer's local runtime pass, and never claim a client change
  "works".
- Update the subsystem CLAUDE.md for code you changed. Do NOT edit docs/DOCS-INDEX.md,
  docs/TESTING-TRACKER.md or docs/SYSTEM-CONNECTION-MAP.md yourself — the Docs agent of this slice owns
  those, so report the rows you need flipped instead.
- THREE-STATE HONESTY: never write "built" for code nothing calls. Use built-and-gauged /
  built-but-runtime-unverified / built-but-INERT, and never let "code exists" stand in for "a player can
  reach it."
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

const CRITIC = {
  type: 'object', additionalProperties: false,
  required: ['gaps', 'next_slice_additions', 'notes'],
  properties: {
    gaps: { type: 'array', items: { type: 'string' }, description: 'what is MISSING: a wall not covered, a ruling not landed, a gauge not written' },
    next_slice_additions: { type: 'array', items: { type: 'string' } },
    notes: { type: 'string' },
  },
}

// ============================================================================
// SLICES — one per row of the plan's §4. Keyed so a session runs exactly one.
//
// MAPPING TO THE PLAN (keep these in step if either changes):
//   plan S0..S6, S8, S9, S10  -> the same keys here.
//   plan S1b (registry bug)    -> S1b here (a live bug; NOT blocked on ruling #21).
//   plan S7 (doctrine)        -> split here into D0 / D2 / D3a / D3b, matching the
//                                doctrine build plan's own names in
//                                GROUND-GAMEPLAY-DECISIONS-2026-07-24.md, because
//                                they are three separate pushes with three gauges.
//   plan S11 (depth)          -> DELIBERATELY NOT ENCODED YET. It is a bag of many
//                                small independent slices (people-cost, ammo, hazard
//                                counters, CasualtyTier, employment/power, tile
//                                bonuses, M4 terrain, naming, client fog, the grave
//                                rung, hex-deposits-as-truth). They are gated behind
//                                milestone M1 (the live cradle-to-grave sitting), so
//                                authoring them now would be guessing at prompts for
//                                work whose ground truth has not been observed yet.
//                                Add them here once M1 has run.
//   plan S12 (#21 capture)    -> present but BLOCKED; it only produces a decision aid.
// ============================================================================

const SLICES = {

  S0: { title: 'Settle the surface scale with a MEASUREMENT (the gauge, not an argument)', size: 'cheap-wire', prompt: `
Three docs disagree about surface scale and the disagreement is diagnosed, not open: ~37 km is
"coarse pitch / 13" and is WHAT THE RESOLVER ACTUALLY MEASURES; ~47 km is an area-equivalent no code uses.
Read first: GameEngine/GroundCombat/GroundMiniHex.cs (MiniPitchKm = coarsePitchKm / (2*cityRadius+1)),
GameEngine/Galaxy/CityGridFactory.cs (CityPatchRadius = 6), GameEngine/GroundCombat/GroundRangeTools.cs
(HexPitchKm(region) = sqrt(2*(Area_km2/Hexes.Count)/sqrt(3)) — PER REGION, PER BODY, not a constant), and
the consumers at GroundForcesProcessor.cs (miniPitchKm assignments).
BUILD a small NUnit readout fixture that generates a game and PRINTS, for Earth / Mars / Luna (whichever
bodies the standard start actually has — verify, don't assume): region name, Area_km2, hex count, the real
HexPitchKm, and the derived MiniPitchKm. Write it to TestResults/surface-scale-readout.txt like the other
readout fixtures do, and cat-ing is already handled by ci.yml's readout step.
ASSERT ONLY STRUCTURAL TRUTHS — never a balance number: mini pitch == coarse pitch / 13 exactly; both > 0
for a generated body; and the pitch differs between two bodies of different size (that IS the design's
"a hex means a different real distance on each body").
Then REPORT the measured numbers in your return value so the session can quote the measurement into
${BOARD} (Layer 4 ~line 276 and Layer 5 ~lines 293-324) and docs/AUTO-RESOLVER-GROUND-TRUTH-2026-07-29.md §12.
Do NOT edit those docs yourself — report the numbers; the Docs agent lands them.
Byte-identity: a new read-only test fixture. Trivially inert.` },

  S1: { title: 'THE GROUND BATTLE LOG — stop the interrupt from lying (ruling #24)', size: 'cheap-wire', prompt: `
THE KEYSTONE SLICE. Verified facts you are acting on:
 - GameEngine/GroundCombat/GroundForcesProcessor.cs:329-330 ALREADY halts the player's clock on a new
   ground battle (RequestCombatHalt) — and the entire 1,078-line file emits ZERO log lines, ZERO events,
   ZERO battle records. Line :334 then deletes the dead (forces.Units.RemoveAll(u => u.Health <= 0)).
   So the clock stops, the UI opens a SPACE report, and that report is empty. The gauge LIES today.
 - The pattern to copy is already in the repo: GameEngine/Combat/CombatEngagement.cs:352 (static
   NarrateToLog), :452-455 (CombatLog(msg) -> Console.WriteLine("[Combat] " + msg)), and :458+
   (RecordBattleEvent) feeding GameEngine/Combat/BattleLog.cs. The CLIENT turns narration on at
   Pulsar4X.Client/PulsarMainWindow.cs:77.
BUILD:
 (a) A narration helper on the ground side gated on a new default-OFF static (mirror NarrateToLog; the
     client flips it on beside the existing one). Narrate: battle joined (who, where, how many each side),
     each resolve round (losses per side), a region owner flip, disengage/wipe, and — this is the point —
     THE HALT ITSELF, so a stopped clock explains why it stopped.
 (b) A STRUCTURED record per event so the fight survives after it ends. DESIGN FORK, decide it explicitly
     and justify in your report: BattleEvent's fields are ship-shaped (BattleLog.cs:29-41 — FleetId,
     FleetName, ShipsLost, ShipsLeft). RECOMMENDED: add a domain discriminator to BattleEvent and REUSE
     BattleLog, because the client's existing Battle Report already reads it, so ground fights appear in
     the report with no new UI. If you instead add a parallel ground log, you must say what reads it.
     Whichever you choose, keep the capture UNCONDITIONAL (not gated on the narration flag) exactly as
     BattleLog.cs:66-68 explains.
 (c) TWO cautions to handle, not discover later: BattleLog is explicitly RUNTIME-ONLY / NOT SAVED
     (BattleLog.cs:66-68) — do not silently make it save state; and MaxEvents = 250 (BattleLog.cs:71) is
     sized for fleets, so a large ground battle can evict space history. If you raise it, mark it
     "// FLAGGED balance value" and say why.
GAUGE (new fixture, unique filename): stand up two hostile forces on one body, run the resolver, and assert
(1) at least one record per phase (join / loss / flip / disengage), (2) records name the right formations
and the right loss counts, (3) with the narration flag OFF the engine is byte-identical to before (no
Console output, existing ground fixtures unaffected).
REACHABILITY criterion to state in your report: Main menu -> DevTest (or a menu game with a garrison) ->
advance the clock until a ground fight -> the fight narrates into game_logs/.
Byte-identity claim: default-OFF narration flag; the structured capture is additive.
Add a docs/CLIENT-TEST-CHECKLIST.md row for "ground fight narrates + appears in the Battle Report".` },

  S1b: { title: 'FIX THE COLONY REGISTRY — a live bug: the AI can target its own planet', size: 'cheap-wire', prompt: `
A REAL BUG, not a doc problem. Verify it, then fix it.
FactionInfoDB.Colonies (FactionInfoDB.cs:62) is the registry the ENTIRE faction/AI layer reads. It is only
ever ADDED to (Colonies/ColonyFactory.cs:104 and :226) — there is NO removal path anywhere in the solution —
and colony capture never touches it (GroundCombat/GroundForcesProcessor.cs:1073 flips only
colony.FactionOwnerID, reached from TryCapturePlanet at :1056).
THE FAILURE: faction A takes every region of B's world. B STILL LISTS IT, so B's DefendResolver keeps
defending it and FactionRollup.ColonyCount keeps counting it. A NEVER lists it, so A's FactionState
world-view never contains it. Then MilitaryTarget.EnemyColonies (MilitaryTarget.cs:106-129, esp. :120) builds
A's strike-target set by enumerating B's OWN Colonies list — and scores the planet A ALREADY OWNS as a target
to invade.
BEFORE CHANGING ANYTHING, read every reader and say what each assumes, because several treat membership as
ownership: Factions/FactionState.cs:50, NPCDecisionProcessor.cs:478, ConsolidateResolver.cs:55 and :109,
DefendResolver.cs:155 and :171, FactionRollup.cs:39/:45/:111, Espionage.cs:61, MilitaryTarget.cs:120,
GroundCombat/GroundStartGarrison.cs:43.
BUILD: at the capture site, remove the colony from the old owner's registry and add it to the new owner's.
Mind that FactionInfoDB.Colonies is save-carried — respect [JsonProperty]/deep-copy conventions, and check
whether any index or count is cached elsewhere.
GAUGE: capture a world, then assert (a) it LEAVES the loser's Colonies, (b) it JOINS the captor's, (c)
FactionRollup.ColonyCount moves on BOTH sides, and (d) MilitaryTarget no longer returns the captured world as
a target for its NEW owner — (d) is the assertion that actually encodes the bug, so do not omit it.
NOT BLOCKED on ruling #21: this is registry hygiene (the registry must reflect reality regardless of what the
developer decides transfers), NOT "what capture transfers". Do not add any other transfer behaviour here.
Byte-identity: this CHANGES behaviour deliberately (it fixes a bug). Say which existing fixtures assumed the
broken state, if any.` },

  D0: { title: 'THE DOCTRINE KEYSTONE — stop TrySetDoctrine dropping the fields on the floor', size: 'cheap-wire', prompt: `
DO THIS BEFORE D2/D3a/D3b. Without it every doctrine slice decorates a value that is discarded on assignment.
Verified: the 25-entry unified catalog is authored data that NOTHING READS. CombatDoctrine's reader
(GameEngine/Combat/CombatDoctrine.cs — see :28, :58, :82) has ZERO non-test engine callers for 9 of its 10
functions, and the ONE live path — FleetDoctrine.TrySetDoctrine (GameEngine/Combat/FleetDoctrine.cs:54-64) —
copies the raw blueprint and SILENTLY DROPS EngagementPosture, TargetPriority, RetreatCasualtyThreshold,
BreakAwaySeconds and Pursues. That is why rulings #15/#18/#19/#20 all currently resolve into a JSON file with
no consumer.
BUILD: route FleetDoctrine.cs:58-64 through CombatDoctrine.Effective*/ParsePosture so an assigned doctrine
actually carries every field it was authored with. Keep the reciprocal rule intact — space ToughnessMult and
ground DamageTakenMult are reciprocals and exactly ONE is authored per entry; CombatDoctrine's guard and
UnifiedDoctrineTests exist for this, keep them green.
GAUGE: assign a doctrine to a fleet AND to a ground formation, read it back, and assert all five
previously-dropped fields survive with the authored values — the test that would have caught this in D1.
Also assert the reciprocal still derives correctly after the change.
Byte-identity: nothing yet READS the newly-carried fields (D3a/D3b do that), so carrying them is inert by
construction. State that claim explicitly and prove it by naming the (zero) consumers.` },

  S2: { title: 'The five ground behaviour flags into the SAVE (ruling #27a)', size: 'medium', prompt: `
CORRECTED PREMISE — verify it yourself, then build on it: the five flags are set on the NORMAL menu New
Game path (Pulsar4X.Client/Interface/Menus/NewGameMenu.cs:562-581) as well as the DevTest path (:979-986).
So it is NOT true that only DevTest turns them on. The real defect is that LOADING A SAVE never sets them,
so one save plays differently depending on whether a New Game happened earlier in the same process — and
CI therefore tests a configuration no player runs.
The five: GroundForcesProcessor.EnableGroundTacticalAI (:62), EnableGroundRoleManeuver (:72),
EnableMiniHexCombat (:85), EnableInitialEngagementSpread (:100), and GroundAssembly.AutoFormUp.
BUILD: carry the five in SAVED game settings — New Game writes them, LOAD reads them and sets the statics.
FIRST find the right home: grep for an existing saved game-settings/options blob and APPEND fields to it.
Only if none exists may you add a blob, and then you must state the save-migration consequence
(TypeNameHandling.Objects embeds type names — a new *DB is a new type name in every future save).
Keep the statics as the runtime read path so no hotloop call site changes.
GAUGE: create a game with a known non-default combination, save, load in a fresh state (statics reset to
default first, to prove the load is what sets them), and assert all five match. Plus: the existing ground
fixtures still pass with the flags at their CI defaults.
Byte-identity: default values unchanged; only the LOAD path gains behaviour.` },

  S3: { title: 'Promote the existing scenario start (ruling #27b + audit P1)', size: 'cheap-wire',
        needsRuling: 'Q2 in the plan — does the developer want the DevTest button renamed/promoted into a supported Scenario/Skirmish start, or a separate authored scenario built?', prompt: `
DO NOT START until the developer has answered plan Q2.
VERIFIED: the scenario start already exists and is already reachable — Pulsar4X.Client/Interface/Menus/
MainMenuItems.cs:51 renders an UNGATED "DevTest" button calling NewGameMenu.DevTestGame(), and
NewGameMenu.cs:895-911 + :979-986 stand up the player plus two developed NPC factions (UMF at war with
Earth, Kithrin) with all five ground behaviour flags ON, via the same DevTestStartFactory.CreateDevTest
orchestrator CI already drives. This is how ruling #27b (a stock New Game raises NOTHING) and the audit's
P1 (a takeable target without DevTools) reconcile WITHOUT new machinery.
BUILD (if the developer chose promotion): rename the button and the menu copy to a first-class
Scenario / Skirmish start, with one short sentence of in-menu text saying what it sets up. Keep the stock
New Game path untouched — it must still raise no garrison and place no enemy (#27b).
GAUGE: a BaseModIntegrity-style assertion that the scenario start yields at least one HOSTILE garrison and
a takeable target, so it cannot silently empty out later (this is the guard the audit's P1 asked for).
Byte-identity: naming/UI only, plus a new assertion. No engine behaviour change.
Add a CLIENT-TEST-CHECKLIST row (the button boots and the rivals exist).` },

  S4: { title: 'Unstick the designer door — ground designs listable and reopenable (audit BREAK 3)', size: 'cheap-wire', prompt: `
VERIFIED: Pulsar4X.Client/Interface/Windows/ShipDesignWindow.cs:166 builds the picker from
_factionInfoDB.ShipDesigns only; :223 documents in a comment that "ground designs live in IndustryDesigns,
never selected here"; :592 registers a saved ground design via GroundUnitAssembly.RegisterAssembledDesign
into IndustryDesigns. Net effect: a player can SAVE a ground design and can NEVER REOPEN it. The ground
panels are additionally gated behind having a pre-existing SHIP design (check the gates around :166/:178/
:332 and quote what you actually find — the exact lines may have moved).
BUILD: list ground designs from IndustryDesigns alongside ShipDesigns in the picker (clearly distinguished
so a ground design cannot be loaded into the ship-hull editor path by accident — read :223's warning and
respect the reason behind it), and let the ground panels render without a pre-existing ship design.
Client-only; keep the two design stores as they are (do NOT migrate ground designs into ShipDesigns — that
would change the industry rails).
GAUGE: CI can only compile the client, so add an ENGINE-level assertion that a design registered by
RegisterAssembledDesign is retrievable by the same lookup the picker uses — the data contract behind the
button, which is the part CI can actually gauge.
Add CLIENT-TEST-CHECKLIST rows: Entity Assembler -> Ground -> design -> save -> REOPEN it; and the ground
panels appear with no ship design in existence.
Byte-identity: client-only + one new engine assertion.` },

  S5: { title: 'ONE build queue with destinations — and kill the free path (rulings #10 + #9 + #6 + #11)', size: 'large', prompt: `
THE PAIRING RULE, DO NOT BREAK IT: ruling #9 (delete the free "Build here" path) and ruling #10 (one queue)
MUST land in the same slice, because the free path is TODAY THE ONLY producer of a building that actually
fortifies. Ship #9 alone and ground defence silently breaks. Verify that claim yourself before you cut
anything: find the free-build handler, find the costed tile queue (GroundCombat/GroundBuildQueueDB.cs,
GroundBuildQueueProcessor.cs, GroundBuild.cs), and find what GroundFortification.cs actually READS. Report
the three findings before designing.
BUILD:
 (a) ONE RTS-style queue for units AND buildings, every entry carrying its DESTINATION, with visible
     progress. Ruling #10's own note: most of this exists — Pulsar's IndustryJob queue already does batch /
     repeat / priority / cancel / auto-install. The work is routing ground through it properly, adding the
     destination field, and surfacing progress. Do NOT build a second queue system.
 (b) The costed path must write whatever region/hex list GroundFortification reads, so a built building
     fortifies (this is what closes the #9 gap).
 (c) Muster/rally as a BUILDING SETTING (ruling #6): a planetary coordinate for ground, an orbit for space.
     This replaces the region-0 hardcode — GroundUnitDesign.DefaultRegionIndex is declared ~:126 and READ
     at muster ~:163-165 but never set non-zero by any path. VERIFY that before relying on it.
 (d) "Occupies a tile" and "is a war-map objective" become ONE attribute (ruling #11), not two.
 (e) Close the "building built after game start is located nowhere" hole: locate installations at
     PRODUCTION COMPLETION, idempotently (the audit's P6 hook).
GAUGE: queue one unit and one building with explicit destinations; assert both arrive at the NAMED place
(not region 0), the building fortifies its region (the thing #9 would otherwise break), progress is
readable, and NOTHING can be created for free any more.
Six-point registration + BaseModIntegrityTests must stay green with ZERO skipped entries.
Byte-identity: this one CHANGES BEHAVIOUR by design (a removed free path). Gate the new queue behind a
default-OFF flag if the existing fixtures depend on the old paths, and say exactly which fixtures you had
to touch and why.` },

  S6: { title: 'Movement rework — the two-layer address, and make the token actually move (rulings #14/#16/#17)', size: 'medium', prompt: `
Ruling #14 DELETES "march to region" entirely — and it removes a BROKEN path, not a working one: every
region-march button, every queued region move and every AI move sets the region INDEX without restamping
the GLOBAL POSITION, so the token never moves on the map. VERIFY that first: find every issuer, and for
each show the index write and the ABSENT global-position write, with file:line. The position fields are on
GroundForcesDB/GroundUnit: HexQ/HexR (per-region, ~:152/154), GlobalQ/GlobalR (~:171/173), MiniQ/MiniR
(~:182/184) plus the sub-tile MiniOffX_km/MiniOffY_km.
BUILD: (a) the two-layer coordinate (17,09)(22,47) — regional hex + mini hex as ONE address, including a
formatter for display; (b) mini-hex movement as the SAME verb at a finer layer (ruling #16); (c) restamp
the global position on every move so the token moves; (d) the march readout showing ALL FOUR (destination,
distance remaining, ETA, current speed). Speed_kmh is already real AND already read by the resolver's
closing step (GroundForcesProcessor.cs:855) — reuse it, do not add a second speed.
Keep all changes inside the ONE ground hotloop (landmine L9).
GAUGE: issue a move and assert the GLOBAL position changes (the bug that would otherwise survive); assert
the four readout numbers are non-zero and mutually consistent (distance/speed ≈ ETA); assert a mini-hex
move lands on the intended mini tile.
Byte-identity: deleting the region-march verb changes behaviour — say which fixtures used it, and keep the
old path behind a flag for one slice if any fixture depends on it.` },

  D2: { title: 'Doctrine: ground reads the UNIFIED catalog; retire groundStances.json', size: 'medium', prompt: `
D1 (unified catalog shape + pure reader + reciprocal guard, 97ecd5b) and D1b (the 25-entry role catalog +
the client domain filter, b218acf) are BUILT and CI-GREEN. Today the entire ground doctrine surface is
still three JSON entries in GameData/basemod/.../groundStances.json (Balanced / Offensive Push / Dig In —
attack multiplier, damage-taken multiplier, cooldown) read via ModDataStore.GroundStances.
BUILD: ground reads the unified catalog instead; retire groundStances.json. Keep the ID/name mapping so
existing saves and scenario JSON that name a stance still resolve — say exactly how you handle a save that
references an old stance id.
LANDMINE: space ToughnessMult and ground DamageTakenMult are RECIPROCALS. Author exactly one per entry;
CombatDoctrine/UnifiedDoctrineTests guard this — keep them green.
GAUGE: every stance id previously in groundStances.json resolves to a catalog entry with equivalent
numbers (so this slice is provably behaviour-preserving), and a ground formation can be assigned any
ground-selectable catalog entry.
Byte-identity: numbers preserved through the migration; prove it in the gauge.` },

  D3a: { title: 'Doctrine gets MOVEMENT authority — ClosingIntent (the developer\'s actual point)', size: 'medium', prompt: `
Verified state, which makes this FOLD + REDIRECT rather than new machinery: the in-engagement movement
machinery already runs inside the resolver on both sides — ground GroundForcesProcessor.
ApplyEngagementManeuvers (~:263) -> GroundRoleComposer.RoleMoveAway -> StepMiniToward; space
CombatEngagement.AdvanceClosing (~:785). RoleMoveAway already returns exactly the tri-state a closing
intent needs (false = close, true = kite, null = hold). Ground already has a separate intent concept,
GroundEngagementStance (Hold / Close / Stand-off), which gates WHETHER a unit auto-maneuvers while the
ROLE decides WHICH WAY. And doctrine has ZERO movement authority today: of the movement dials the catalog
carries only SpeedMult, and NOTHING READS IT — verify that with a grep before you start.
BUILD, in this order: (1) add ClosingIntent (Close / Hold / Standoff / Kite) to the doctrine blueprint plus
a CombatDoctrine.ParseClosingIntent, authored across the catalog; (2) make RoleMoveAway / AdvanceClosing
consult THE FORMATION'S DOCTRINE FIRST, falling back to role when the doctrine does not specify — this is
the line that transfers steering from "what I am" to "what I was ordered"; (3) fold ground's existing
GroundEngagementStance into ClosingIntent so there is ONE intent, not two; (4) make SpeedMult finally bite
on those maneuvers.
Only after this may "Standoff Barrage" be added to the catalog — it was deliberately held back from D1b
because without step (2) an artillery doctrine maneuvers identically to unassigned artillery and the name
would be pure decoration.
GAUGE: two identical forces differing ONLY in assigned doctrine move differently — and the difference is
NAMED in the S1 ground log (a kiter opens the range, an Anvil holds). Assert on positions/gaps, not vibes.
Byte-identity: default-OFF flag; with the flag off, role decides exactly as today.` },

  D3b: { title: 'Doctrine gets FIRE behaviour — targeting, retreat, break-away, pursuit, leaders', size: 'medium', prompt: `
BUILD (each behind its own default-OFF flag, byte-identical off):
 (a) TARGET PRIORITY per doctrine (ruling #18: the player chooses, gated by the battalion's doctrine),
     replacing the current spread-by-current-health allocation that shoots the healthiest and never
     finishes a cripple. Verify that allocation in source first and quote it.
 (b) A per-doctrine RETREAT THRESHOLD, replacing the flat 0.5 constant (find and quote it).
 (c) THE RETREAT VERB + the break-away timer (~1-2 h, "// FLAGGED balance value") + the ENGAGEMENT LOCK:
     ruling #15 says a fight is COMMITTED — it ends only when one side is wiped or one side calls RETREAT.
     Today there is no ground retreat verb and no lock; verify both. A doctrine change is a DIRECT call
     that deliberately BYPASSES the lock (the developer's rule) — the player's only other in-fight input is
     the retreat call.
 (d) PURSUIT as a doctrine the opponent chooses. ALL exit pricing (parting shot, delay, fighting
     withdrawal) lives in DOCTRINE ENTRIES, never hardcoded.
 (e) LEADER MODULATION: the substrate exists — CommanderDB carries a PersonalityDB, and
     OfficerCharacter.Blend/TenureWeight already feed the SPACE retreat decision. Ground has no equivalent
     read. Either wire the ground read (mirroring the space call site, which you must locate and quote) or
     RECORD THE DEFERRAL EXPLICITLY in your report and in the subsystem CLAUDE.md. Do not leave it silent —
     the developer named it ("doctrines are also affected by leaders").
GAUGE: a doctrine with an early retreat threshold breaks off sooner than a stubborn one; an Anvil never
retreats; a pursuing doctrine chases and a non-pursuing one does not; a commander's character shifts the
retreat point (or the deferral is recorded). Every one of these must be legible in the S1 log.` },

  S8: { title: 'Shorten the combat tick, then CALCULATE fire rate (ruling #23)', size: 'medium',
        needsRuling: 'Q3 (the tick VALUE), Q4 (mid-tick overkill semantics), Q5 (per-weapon rates) in the plan', prompt: `
DO NOT START until the developer has answered plan Q3, Q4 and Q5.
TWO FINDINGS RESHAPE THIS SLICE — read them before planning:
 (C2) THE SALVO POOL IS NOT deltaSeconds-SCALED: pool = m.Attack * SalvoScale, applied once per tick
      (GroundForcesProcessor.cs:487,491,520,543). So shortening the tick ALONE multiplies ground damage by
      the shortening factor (1 h -> 5 s = 720x). Ammo drain (AmmoPerSalvo_kg) and infrastructure bombardment
      scale the same way, while attrition, shield regen and the K3 closing step ARE properly tick-scaled — so
      the balance INVERTS. THEREFORE the tick change and the rate model MUST land in this ONE slice; do not
      split them, and AUDIT EVERY per-tick damage term for deltaSeconds scaling as part of the work.
 (C3) AN ACCEPTANCE SPEC ALREADY EXISTS AND IS UNIMPLEMENTED: Pulsar4X.Tests/Resolver2DJointsSpecTests.cs
      pins a 5 s ground quantum (:210), the 720-steps-per-hour divisibility invariant (:216), and
      fast-forward==watch at a fixed quantum (:232, :253) including a proof that a variable 1 h step
      diverges. Read it FIRST and build to it rather than inventing a cadence.
The problem, verified: GroundForcesProcessor.RunFrequency = TimeSpan.FromHours(1) (:29), and
GroundWeaponAtb carries a flat Attack applied in full once per hour (no rate, no reload — five fields:
Mass, Attack, Range, Mode, Range_m at GroundWeaponAtb.cs:33-45). Under a rate model, 10 damage/second
becomes 36,000 damage in one tick. The arithmetic is what the developer asked for; the TICK is what makes
it absurd. The developer RULED: shorten the tick. Space's CombatReactionStep fine-step is the precedent —
locate it and quote the real constant.
BUILD: (1) a fine step while a ground battle is LIVE, hourly otherwise (mirror the space pattern, do not
invent a second scheduler — and mind landmine L9: stay inside the one ground hotloop). (2) A per-weapon
RATE (damage/second) that the resolver INTEGRATES over the tick against available targets, resolving who
died and who lived, for ALL weapons. (3) The mid-tick overkill rule exactly as the developer confirmed in
Q4 (the recommendation was sequential down the doctrine's priority list — which ties this to ruling #18 and
therefore to D3b: build D3b FIRST if the answer is sequential).
THE C2 GUARD, which is the assertion that matters most here: the SAME fight run at TWO different tick
lengths must produce the SAME result. That is what catches an unscaled damage term. Write it first.
THE CALIBRATION-PROOF GAUGE: derive each weapon's rate from today's flat
Attack divided by the chosen tick, and assert a reference fight resolves IDENTICALLY to today. That proves
the refactor before any tuning happens. THEN a second case: raising one weapon's rate changes the outcome
in the expected direction.
Determinism: fast-forward must equal watch — assert the same fight from a save produces the same result.
Every rate and the tick value get "// FLAGGED balance value".
Byte-identity: default-OFF flag for the rate model; with it off, the hourly lump behaviour is unchanged.` },

  S9: { title: 'Close the bombardment joint — soften the beach for real (audit P3)', size: 'medium', prompt: `
The live fleet engine never fires on colonies: no BombardColonyOrder, no client button, no ConquerResolver
bombard rung — so "soften before you land" is dark for the AI and a Fire-Control workaround for the player,
and the invasion loop closes only because troops beat the garrison on numbers. VERIFY all of that, and find
what DOES exist: DamageProcessor.ApplyGroundBombardment (the already-wired damage path),
DamageProcessor.OnColonyDamage, BombardGlobalHex (check whether its only callers are tests), and the
one-shot orbital bombardment the Earthfall tests exercise.
BUILD: (a) a first-class REGION-TARGETED BombardColonyOrder routing into the existing
ApplyGroundBombardment path (hit the LANDING region's defenders, not the whole surface); (b) the client
button; (c) a ConquerResolver bombard rung ABOVE the LAND rung so the AI softens too.
NOTE the standing developer decision from Earthfall: a bombard RE-FIRE cadence between waves was TABLED.
Build the order and the rung; do NOT build or assert a re-fire cadence.
GAUGE: an order damages the TARGETED region's defenders and not another region's; the AI rung fires before
landing; the colony readout loses what it should. Calibrate nothing silently —
GroundBombardmentDamagePerStrength and the colony energy divisor are "// FLAGGED balance value".
Byte-identity: new order + default-OFF AI rung flag.` },

  S10: { title: 'The designer chain in the FORCED order #2 -> #4 -> #1, then #3', size: 'large', prompt: `
THE ORDER IS NOT NEGOTIABLE (ruling #1's own note): the three prebuilt templates are TODAY THE ONLY carrier
of penetration and per-shot energy, AND they are what GroundStartGarrison raises, AND they are referenced
from the base mod and several tests. So:
 STEP 1 (#2): penetration + per-shot energy become WEAPON properties on GroundWeaponAtb (currently five
   fields, :33-45). THE JSON ATB BINDER IS EXACT-ARITY — you must update EVERY ground-weapon template that
   binds this atb in the SAME change or all of them fail to bind. Enumerate those template files FIRST and
   list them in your report before editing.
 STEP 2 (#4): ground parts cost RESEARCH, scaling with complexity. Today most templates cost 0 and are
   start-unlocked — verify the real count and report it. Six-point registration applies: a design id in the
   colony blueprint's ComponentDesigns AND the template id in StartingItems are SEPARATE unlocks; conflating
   them is how "X was not found in the faction data store" ships.
 STEP 3 (#1): delete the three prebuilt templates — WITH a garrison-composition replacement so
   GroundStartGarrison still raises a garrison from designed units, and with every base-mod and test
   reference updated.
 STEP 4 (#3): an invalid design (over carry budget / unpowered / no magazine) is BLOCKED FROM SAVING.
   PIN THIS FIRST — the audit's status board says the carry/power/ammo gates bite in the ENGINE assembly
   path while the surveys found them unenforced at the SAVE step. For EACH of the three gates determine
   which of {computes a number, DISPLAYS a warning, BLOCKS assembly, BLOCKS the save} it does today, with
   file:line. That disagreement IS the size of this step; resolve it before building.
GAUGE: BaseModIntegrityTests green with ZERO skipped entries (the sensor for JSON drift, which crashes
players rather than tests); a garrison still raises after the prebuilts are gone; a weapon carries
penetration and per-shot energy on the ASSEMBLED path; an invalid design cannot be saved; research gates
what you can design.
Byte-identity: this changes data. State exactly which fixtures change and why, and keep the research costs
at zero-cost defaults in CI if that is what preserves existing fixtures.` },

  S12: { title: 'What capture transfers (ruling #21)', size: 'blocked', blocked: true, prompt: `
BLOCKED. Ruling #21 is explicitly OPEN and the developer said "still thinking". Do NOT decide it and do NOT
build it. If this slice is invoked, report that it is blocked, and instead produce the DECISION AID the plan
promises: read TryCapturePlanet and report, step by step with file:line, exactly what it does today (it is
believed to be a bare FactionOwnerID flip), then list every candidate thing that COULD transfer —
population, buildings on the hexes, mineral stockpiles, designs, research, surviving units, infrastructure
rating — and for each state whether the code today moves it, destroys it, or ignores it. That is what lets
the developer rule with facts in front of them.` },
}

// ============================================================================
// SLICE RUNNER — implement -> adversarial verify -> repair -> docs
// ============================================================================

async function runSlice(key) {
  const s = SLICES[key]
  log(`slice ${key} — ${s.title} [${s.size}]`)

  if (s.blocked) {
    const aid = await agent(
      COMMON + `\nYOUR SLICE (${key}): ${s.title}\n\n` + s.prompt,
      { label: `blocked:${key}`, phase: 'Implement' })
    return { key: key, status: 'blocked', decisionAid: aid }
  }

  if (s.needsRuling) {
    log(`⚠ ${key} depends on a developer ruling: ${s.needsRuling}`)
  }

  const impl = await agent(
    COMMON + `\nYOUR SLICE (${key}): ${s.title}\nSize estimate from the plan: ${s.size}\n\n` + s.prompt +
    `\n\nRETURN: (1) every file changed/created and what each change does; (2) tests added and what they` +
    ` ASSERT (not what they run); (3) your BYTE-IDENTITY claim and why it is true; (4) every "// FLAGGED` +
    ` balance value" you introduced; (5) every [A24]/[?] fact you had to verify yourself and what you` +
    ` actually found (say so plainly if an inherited claim was WRONG); (6) the rows you need the Docs agent` +
    ` to flip in DOCS-INDEX / TESTING-TRACKER / SYSTEM-CONNECTION-MAP / CLIENT-TEST-CHECKLIST; (7) whether` +
    ` your new fixture needs its own CI shard and the exact yml shape if so; (8) anything you could NOT do` +
    ` and why.`,
    { label: `impl:${key}`, phase: 'Implement' })

  if (impl == null) { log(`slice ${key}: implementer died — SKIPPED`); return { key: key, status: 'skipped' } }

  const verdict = await agent(
    COMMON + `\nYou are an ADVERSARIAL verifier for slice ${key} ("${s.title}"). Read the working-tree diff
(git diff + git status) and the implementer report below. Your job is to REFUTE, not to agree. Default to
approve=false when uncertain; make every blocker concrete with file:line.
 - COMPILE (no SDK here, so this is on you): does every referenced member exist with that exact name,
   arity and namespace? usings present? Is the new test file where the test project picks it up?
 - GAUGE HONESTY: do the tests assert the CLAIMED BEHAVIOUR, or are they tautologies that would pass with
   the feature removed? Try to name one change to the production code that would keep them green.
 - BYTE-IDENTITY: is the stated claim actually TRUE? Are new flags really default-off? Do existing ground
   fixtures still run the old path?
 - LANDMINES: L4 (a throw inside a hotloop), L9 (a second hotloop on the same blob), atb EXACT-ARITY
   completeness (every binding template updated?), six-point registration, [JsonProperty] + deep-copy for
   any save-carried field, enum append-only ordering, save/type-name risk, doctrine reciprocal (exactly ONE
   toughness encoding per entry), determinism (any unseeded RNG in a resolver?).
 - RULINGS: does the change contradict any of the 27 in ${RULINGS}? Ruling #21 must remain undecided.
 - THREE-STATE HONESTY: does the report or any doc/comment call something "built" that nothing calls, or
   "reachable" without a real click path?
 - LEFTOVERS: stale comments or docs the change just made false (this repo had 327 dead doc pointers —
   do not add more; run the residual grep from DOCS-INDEX known-debt 3(a) if the slice touched doc paths).
IMPLEMENTER REPORT:\n${impl}`,
    { label: `verify:${key}`, phase: 'Verify', schema: VERDICT })

  if (verdict && !verdict.approve && verdict.blockers && verdict.blockers.length > 0) {
    log(`slice ${key}: ${verdict.blockers.length} blocker(s) — repair pass`)
    await agent(
      COMMON + `\nREPAIR pass for slice ${key} ("${s.title}"). Fix each blocker below in the working tree.
Verify every API by READING the source; make the smallest correct change; do not widen the slice.
BLOCKERS:\n- ` + verdict.blockers.join('\n- ') +
      `\n\nORIGINAL REPORT:\n${impl}\n\nRETURN: what you changed for each blocker, and any blocker you` +
      ` believe is WRONG (say why, with file:line — the verifier can be mistaken).`,
      { label: `repair:${key}`, phase: 'Verify' })
  }

  const docs = await agent(
    COMMON + `\nYou are the DOCS agent for slice ${key} ("${s.title}"). You own the dashboards for this
slice — the implementer was told not to touch them.
Read the working-tree diff and the implementer report, then update, IN THIS WORKING TREE:
 - docs/DOCS-INDEX.md — flip/add the row for every doc this slice changed or made newly-true/newly-stale
   (this file owns DOC CURRENCY). Refresh the "As of" stamp if the pass was substantive.
 - docs/TESTING-TRACKER.md — a row per new gauge, with the seven fields that file uses (what · why ·
   most-efficient method · what-right-looks-like · most-likely-failure · mitigation · what-it-unblocks).
 - docs/SYSTEM-CONNECTION-MAP.md — any NEW system-to-system wire this slice created.
 - docs/CLIENT-TEST-CHECKLIST.md — a row for anything only the developer's local Windows runtime can check
   (all client behaviour qualifies; CI compiles the client but can never run it).
 - ${PLAN} — mark this slice's row with its real state, and correct any [A24]/[?] marker the implementer
   verified (say what it turned out to be).
RULES: use the THREE-STATE vocabulary — never write "built" for code nothing calls, and never "reachable"
without the literal click path. Plain English, define jargon on first use (the developer is a Navy
nuclear-trained machinist's mate, not a career programmer). Do not invent a status you did not read in the
diff. Keep provenance names of merged-away docs as PLAIN TEXT, never rewritten into a live doc's path — a
2026-07-13 link sweep did that once and destroyed the history.
IMPLEMENTER REPORT:\n${impl}
RETURN: every row you added or flipped, and anything you deliberately did not touch.`,
    { label: `docs:${key}`, phase: 'Docs' })

  return {
    key: key,
    status: 'done',
    approved: verdict ? verdict.approve : null,
    blockers: verdict ? verdict.blockers : [],
    docs: docs,
    report: impl,
  }
}

// ============================================================================
// DISPATCH — args may be {slice:"S1"}, {slice:["S1","S2"]}, a JSON string, or "S1"
// ============================================================================

let _a = args
if (typeof _a === 'string') {
  const t = _a.trim()
  try { _a = JSON.parse(t) } catch (e) { _a = { slice: t } }
}
if (Array.isArray(_a)) { _a = { slice: _a } }

let want = _a && _a.slice ? _a.slice : null
if (typeof want === 'string') { want = [want] }

const keys = Object.keys(SLICES)
if (!want || !want.length) {
  return { error: `Pass args.slice — one of: ${keys.join(', ')}. See ${PLAN} §4 for what each slice does, its gauge, and its dependencies. (received args of type ${typeof args})` }
}

const unknown = want.filter(function (k) { return !SLICES[k] })
if (unknown.length) {
  return { error: `Unknown slice(s): ${unknown.join(', ')}. Valid: ${keys.join(', ')}.` }
}

phase('Design')
log(`CLOSE PLANETARY DELTA — slice(s): ${want.join(', ')}`)
log(`Plan: ${PLAN} · Evidence: ${EVID} · Rulings: ${RULINGS}`)
log('Operator: ONE slice per push. Gate CI GREEN (~33 min, 6 test shards + build-client) before the next.')
for (const k of want) {
  if (SLICES[k].needsRuling) log(`⚠ ${k} needs a developer ruling first: ${SLICES[k].needsRuling}`)
}

phase('Implement')
const results = []
if (want.length === 1) {
  results.push(await runSlice(want[0]))
} else {
  log('NOTE: running multiple slices concurrently in one working tree — only do this when their file sets are DISJOINT.')
  const batch = await parallel(want.map(function (k) { return function () { return runSlice(k) } }))
  results.push(...batch.filter(Boolean))
}

phase('Synthesize')
const critic = await agent(
  COMMON + `\nYou are the COMPLETENESS CRITIC for this run (slices: ${want.join(', ')}). Read the
working-tree diff and the slice results below, then answer one question: WHAT IS MISSING?
 - A reachability wall in ${PLAN} §2 that this slice was supposed to close and did not.
 - A ruling in ${RULINGS} that this slice touched but did not actually land.
 - A gauge that was not written, or one that cannot fail.
 - An observability hole: did the change produce anything a player or the developer can SEE? If it is
   invisible, that is a gap, not a detail — the whole point of this plan's ordering.
 - A doc claim this slice just made false and nobody corrected.
 - Anything the plan's delta ledger should now flip, in any of the three columns.
Be specific and file:line where you can. What you find becomes the next slice's work list.
SLICE RESULTS: ${JSON.stringify(results.map(function (r) { return { key: r.key, status: r.status, approved: r.approved, blockers: r.blockers } }))}`,
  { label: 'completeness-critic', phase: 'Synthesize', schema: CRITIC })

const brief = await agent(
  COMMON + `\nYou are the landing reporter for slice(s) ${want.join(', ')}. Read git status + git diff and
the results below. Write the operator's landing brief IN PLAIN ENGLISH (the developer is a Navy
nuclear-trained machinist's mate — lead with what it does and why it matters, then how; define jargon;
shipboard/mechanical analogies land well):
 - the recommended COMMIT SEQUENCE (one commit per slice, dependency order, one-line messages);
 - engine vs client-only classification per commit (client-only = CI compiles it but can never run it, so
   it needs a local runtime pass);
 - every FLAGGED balance number introduced, and what it does;
 - every developer DECISION still needed (including any plan Q1-Q5 still unanswered);
 - what the developer should SEE if the change works, and where (which log tag, which window);
 - anything that is NOT done and why.
RESULTS: ${JSON.stringify(results)}
CRITIC: ${JSON.stringify(critic)}`,
  { label: 'landing-brief', phase: 'Synthesize' })

return { slices: results, critic: critic, landingBrief: brief }
