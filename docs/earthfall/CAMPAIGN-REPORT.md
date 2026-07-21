# Operation Earthfall — Campaign Report

**Status: COMPLETE. All five lanes merged into CORE, every slice CI-green, landed 2026-07-21.**

This is the close-out. It says what shipped in each lane, which developer decisions were applied, what got switched ON for real games, and what was deliberately deferred or is still waiting on your local Windows play-test. The step-by-step invasion flow (both seats) is its companion: `docs/ground/EARTHFALL-CAMPAIGN-OPS.md`. The decisions of record are `docs/earthfall/EARTHFALL-DECISIONS.md`. The plan that produced all this is `docs/earthfall/CAMPAIGN-PLAN.md`.

---

## 0. Why the campaign existed (one paragraph)

You played a real game (logs on `main` at `fe72043`). Five things went wrong, one thing was missing, several things were wanted. Ten research passes (`docs/earthfall/findings/`) produced verified, file-and-line ledgers — several of which overturned the obvious theory. Then five parallel branch-lanes with hard file fences fixed the failures and built the missing invasion machinery, each slice gated on CI-green before the next. This report is what came out the other end.

The five real-game failures, and their verdicts:
1. **The freeze at 2050-04-22** was a native-client render crash (contact blips lacked the finite-coordinate cull orbit icons have), **not** a sim crawl.
2. **"Saw the fleet before sensor range"** — detection was honest (a deliberate 200 Gm colony horizon); the *drawn ring* under-drew.
3. **The AI abandoned its own invasion** via a stale-morale echo → phantom rebellion → a 180-day Defend lock at the Survive floor, with nothing recalling the coasting fleet.
4. **The troop transport never existed** — a 4× redundant build queue strangled Mars, a finished hull sat on a fuel-less pad, and a launched one couldn't warp (built ships booted uncharged).
5. **The ground campaign was mostly missing** — no AI bombardment step, no colony-free building (so no beachhead), no AI troop movement after landing, ammo/resupply unwired.

Plus: the NPC factions didn't develop (Kithrin structurally bankrupt; survey chain unbuilt), the player's unit-creation chain had a couple of gaps, and the 2D group-plane resolver hadn't started.

---

## 1. What shipped, per lane

### CORE (`claude/devtest-faction-design-xpfnhe`) — the resolvers, legitimacy, sealift, and acceptance
- **P0 — shared gauges + latent fixes.** The campaign-clock freeze repro (proves the engine half is sound; the freeze was native-client). The per-faction self-sufficiency board. The station-aware AI decision tape. A `SafeDictionary` concurrency fix (events fire *after* releasing the lock; entity queries read a lock-taken snapshot).
- **P3 — the AI stops abandoning its invasion (failure #3).** The UMF now authors a `government` node (Militarism High → its legitimacy war term is +10 pride, not −5 collapse). Rebellion needs *two* consecutive collapsing reads (no one-sample revolt) and legitimacy reads *this* cycle's morale (no stale echo). Three crisis break-glasses so a passed crisis can't lock the brain for 180 days. Operation continuity: a winning in-flight Conquer survives a transient wobble, and a *genuine* Defend actually **recalls in-flight fleets home**.
- **P4 — the sealift sails (failure #4).** Rung-2 already-queued guard (ONE transport, not four). Built hulls boot charged + fuelled (both build paths). Mars's launch pad stocked with fuel (matching Earth). The end-to-end BUILD→LAUNCH→LOAD→SAIL gauge the chain never had.
- **PW — the invasion resolver rungs.** `ConquerResolver` gained: form the landed unit into a battalion; land beachhead parts to feed the combat-engineer FOB build; task an Offensive battalion to raze an enemy building on a reachable hex. (Bombard re-fire tabled; landing resolves per-hex, no region scorer — per your decisions.)
- **P8 — acceptance.** The whole conquest arc as one milestone-by-milestone tape (`OperationEarthfallTests`); the generic player unit→battalion→defend/invade **rails** proven green (`PlayerGroundChainRailsTests`, no hard-coded marine); mid-invasion save/load (`MidCampaignSaveLoadTests`).

### GROUND (`claude/earthfall-ground`) — the invasion machinery (failure #5)
- **G1** — the combat-engineer component + surface parts haulage → a **colony-free on-site beachhead build** and FOB (the thing that made a beachhead impossible before).
- **G2** — AI formation parity (`FormUpLoose`); **the ground tactical brain** (the answer to "is the AI smart enough to be defensive vs offensive" — now yes: `GroundThreat` + `GroundTactics.DecidePosture` + the wire behind `EnableGroundTacticalAI`); and the sustainment loop (ammo drain/silence, depot resupply, standing upkeep values).
- **G3** — per-hex infrastructure **destroy / capture** combat + the first hex-owner consumer (a captured bunker stops fortifying the defender).
- **G4** — the buildable **sealed / environmental component** (the last Space-Marine blocker): a player can now *design* sealing in the unit designer, and a sealed unit survives an airless/toxic world an unsealed twin dies on.

### CLIENT (`claude/earthfall-client`) — the windows (failures #1, #2 + the missing UI)
- **C1** — the freeze fix (failure #1): a finite/on-screen cull on contact blips + finer render breadcrumbs + a tighter watchdog.
- **C2** — sensor truth (failure #2): an honest colony detection *band* (a detected enemy can no longer render outside it) + a held-vs-fresh `[DETECT]` split + the 200 Gm horizon memo (no value changed).
- **C3** — the **Force Management** window: the Fleets tab plus a cross-body **Battalions** tab (location/strength/health, move/stance/ROE/order-queue).
- **C4** — the planet **range overlay** (weapon reach red / radar reach green) + the Entity Assembler Training/Power/Ammo rows.
- **C5** — the **invade-from-orbit** control panel (embark/land troop buttons with a region picker).

### DEV (`claude/earthfall-dev`) — the NPC factions develop (failure #6)
- **D1** — the Kithrin **survey chain** (`ExpandResolver` emits a real geo-survey; the Kithrin field a surveyor ship).
- **D2** — **station income** (ends the structural bankruptcy — a populated station now earns tax, capped by government) + a station-legal Consolidate fall-through so a station-only faction isn't frozen in a crisis.
- **D3** — the AI expand-arc end-to-end gauge (survey → found → in `Colonies`).

### TWOD (`claude/earthfall-2d`) — the 2D group-plane resolver, first slices
- **T0-T3 / S0-S2** — the joints memo (`RESOLVER-2D-JOINTS.md`), the pure `GroupPlane` battle-plane math, the anchor seeding in space combat, and the 2D range gate — **all behind `EnableGroupPlane`, default off** (byte-identical). S3-S6 (role geometry, ground-onto-plane, combined theater, multi-party) are a later campaign.

---

## 2. Developer decisions applied

Answered after all four lanes merged (record: `docs/earthfall/EARTHFALL-DECISIONS.md`). The five build-shaping calls:

1. **The combined fleet + battalion window is named "Force Management."**
2. **No hard-coded Space Marine.** You'll design the sealed + veteran-cadre + power-armour marine *in the game* as your own end-to-end test. So P8 proves the generic unit-creation → battalion → defend/invade **rails** are green and generic; your live build rides them.
3. **All built ships boot charged + fuelled — player ships too.** `ShipDesign.ChargeBuiltPlayerShips` was flipped `false → true` (was the deliberate "player earns charge over game-time" default). One-line revert if you want the earn-it mechanic back. *(Commit `4e26e01`.)*
4. **Orbital bombardment re-fire is TABLED.** The existing one-shot garrison softening stays; no between-waves re-fire cadence was built.
5. **Regions are cosmetic; the hex is the unit of everything.** Landing, infrastructure destroy/capture, and ownership all resolve per-hex. No region-transfer logic.

The balance dials all stand at their defaults, flagged for you to tune from live play (rebellion-debounce 2, crisis-dwell 60 days, station tax 0.15, battalion cap 6, ammo/salvo 1.0, upkeep/mass 0.1, the six ground-tactical-brain thresholds, ground cadre talent-draw, the 200 Gm horizon, launch-pad tonnage 100,000 kg). Every one carries a `// FLAGGED balance value` comment in source (or is noted in the decisions doc for JSON).

---

## 3. The invasion on-switch (the gate flip — and how to revert it)

Two flags gate the AI's ground campaign, and they default **OFF** so the engine test suite stays byte-identical (CI can't run the client, so it can't verify a flip):
- `GroundForcesProcessor.EnableGroundTacticalAI` — runs the ground tactical brain.
- `GroundAssembly.AutoFormUp` — auto-forms raised/landed units into battalions.

For a **real menu-started or DevTest game** you want the invasion to actually play out, so **both are flipped ON on the menu path** (`Pulsar4X.Client/Interface/Menus/NewGameMenu.cs` — `CreateGameCore` + the DevTest block, beside the existing NPC AI gates). *(Commit `ff35533`.)* Without the flip, the `ConquerResolver`'s Offensive-battalion rungs never fire and landed units are never formed up — the ground war stays dormant.

**Easy revert:** delete the two lines in `NewGameMenu.cs` (one in each block). The engine suite is unaffected either way (the flags default off in the engine). The full-game invasion *feel* is your live test — tracked as the **`EF-ONSWITCH`** row in `docs/TESTING-TRACKER.md`'s Operation Earthfall client-runtime backlog; the detailed click-path is a pending CLIENT-lane request (`docs/CLIENT-TEST-CHECKLIST.md` is CLIENT-fenced — see `docs/earthfall/LANE-CORE-NOTES.md` §P8.2).

*(A related mechanical follow-up also landed post-merge: station operating income got its own `TransactionCategory.StationIncome` ledger category so a station-only faction's ledger reads honestly — commit `73acd5f`.)*

---

## 4. Deferred / tabled (explicitly not this campaign)

- **Orbital strike aimed at a specific hex's buildings** — you deferred this earlier (the "W-track" follow-on). Bombardment softens the *garrison* surface-wide; picking a single building from orbit is not built.
- **Bombardment re-fire cadence + cap** — TABLED (decision #4). The one-shot softening stays as-is.
- **Produce-for-captor** — a captured enemy building doesn't produce for you until the whole colony flips (findings R4 decision #4 — the biggest deferred ground piece).
- **The 2D group-plane resolver S3-S6** — role geometry, ground-onto-plane, combined theater (Endor), multi-party FFA. A later campaign, gated by `RESOLVER-2D-JOINTS.md`.
- **Ground cadre talent-scarcity** for elite/sealed units (touches `Factions/ManpowerTools`) — a high-risk balance decision, left for you.
- **An AI-founded colony is born empty + untaxed** (DEV D3 finding) — whether a founded colony seeds population or a governor sets a tax rate is a design call you own. The D3 acceptance milestone for "pays tax" is `[Ignore]`d until then.
- **Command-org 2-tier → 4-tier** expansion (task #22).

---

## 5. What's waiting on your local Windows runtime pass

CI proves the engine logic; it **cannot run the client**. These client behaviours and the whole-invasion *feel* are only verifiable on your machine. They're tracked in `docs/TESTING-TRACKER.md` (the "Operation Earthfall — client-runtime backlog" block) and detailed as click-paths in `docs/CLIENT-TEST-CHECKLIST.md` (sections C1.1 / C2.1 / C3.1 / C4.1 / C5.1 / PW.2). In short:

1. **Freeze fix** — play past 2050-04-22 with an enemy fleet mid-transit, fog on; no freeze.
2. **Sensor ring** — the colony detection band never lets a detected enemy render outside it; peacetime ring unchanged.
3. **Battalions tab** — Force Management lists battalions across worlds; select → move/stance/ROE/order/rename.
4. **Range overlay** — select a unit → red/green reach disk on the globe; assembler shows Training/Power/Ammo.
5. **Embark / land** — the troop-lift order appears with a bay ship; embark → fly → land into a region-picked zone (orbit-gated).
6. **Infra buttons** — raze/capture an enemy building from the battalion order surface + city-zoom.
7. **The invasion, alive** — in a menu game the tactical brain runs: battalions read a stance/ROE/intent, the AI plays its invasion out, and the UMF reads the odds against your Space Marines in force and goes Defensive / withdraws (the moment answering "is the AI smart enough").

And the one you always run first: **T0 — New Game boots and the clock runs.**

---

## 6. The acceptance bar, and where the ceiling is

`docs/MVP.md`'s v1 finish line is "you can take a planet." The full chain — build → load → win orbit → bombard once → land → beachhead → posture → advance → raze → second wave → capture — is now **code-complete and CI-green in the engine**, for the AI (`ConquerResolver` + tactical brain) and for you (the assembler → colony → Force Management → Issue Orders → planet-view windows). The remaining gate is exactly the one CI can't close: your local play-test to confirm it *runs and feels right*, plus the flagged numbers you'll tune once you've watched it happen — "a tear or two" of play-testing, as you put it.

The vision raised the ceiling; this campaign didn't lower the bar. Every capability above is reachable cradle-to-grave: a mineral is mined, refined, built into a component, researched to unlock, assembled onto a unit, fielded, given an order, and lost when it's destroyed. Your Space Marine is not a fixture — it's your own live-designed unit riding those generic rails.
