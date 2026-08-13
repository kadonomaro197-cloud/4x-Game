# Units on the Map — Orders, and Trade That Stays Put

**As of 2026-08-10 · design-only (no engine/GameData change) · prototype: `docs/ground/planetview.html` rev-T.**
Reader: the developer. Plain-English. This is the opening chapter of the **units + orders + intra-planet
logistics** work — how a unit shows on the map, how it takes orders, and how goods move between places now that
**a resource stays in the little hex where it was dug.**

**Grading vocab (same as the prototype):** **LIVE** = the engine reads it today · **DATA** = the field exists but
nothing consumes it · **THEORY / DESIGN** = a proposal, not wired.

---

## The one ruling that drives the whole chapter

You handed down a rule: **a mined resource stays in the mini-hex where it was dug — it does not walk to the factory
on its own.** That single rule is a forcing function. Today all ore lands in one big colony bin and every factory
drinks from that same bin, so *where* a thing sits means nothing. Break the bin into a hundred little buckets — one
per hex — and a shipyard can suddenly **starve next to a full bucket three hexes away that it can't reach.** The
moment that's true, **carrying stuff becomes a real job.** That "something that carries" is a hauler; a hauler is a
unit; a unit has to be on the map and take orders.

So the four things in the title — **units · orders · trade · locality** — are not four features. They are **one
chain**, and this doc lays the links in the order they have to be welded.

---

## 1. What a "unit on the map" is, and the one move order

**The point:** a ground unit is *already* on the map and *already* dies like a ship. What's missing is that you
can't **click one**, you can't **read one**, and the player and the AI move units through **two different doors** —
which your own law says is one door too many.

**What a unit is (LIVE).** Think of a ship's crew roster: one line, one hull, alive or sunk. A ground unit is the
same shape — a plain data record (`GroundUnit`) in a list on the planet body, tagged by a stable ID
(`GroundForcesDB.cs:28`). It carries **one health pool**, whittles down under fire, and the instant it hits zero the
processor deletes the whole record (`GroundForcesProcessor.cs:334`). **Whole-or-dead — exactly like a ship.** It
never splits into half-units. (It's a *data object*, not a full map entity like a ship; an assembler-designed unit
gets an inert backing entity for its components, but that backing has no map position — the unit's whole map
presence is the coordinate stamps below.)

**Where it sits.** A unit carries *four* position stamps at once, like a ship having a fleet position, a system
position, and a berth number:

| Stamp | What it is | Grade | Field |
|-------|-----------|-------|-------|
| **Region** | which province (the coarse band) — the tier combat/capture/attrition key off | LIVE | `GroundForcesDB.cs:46` |
| **Global hex** | the real-distance operational grid — **the tier the top-level map draws the token from** | LIVE | `GroundForcesDB.cs:174` |
| **Region hex** | the per-region patch the closing-fight runs on | LIVE | `GroundForcesDB.cs:155` |
| **Mini-hex** | the fine "city block" grid (~22 km²/tile) — **the tier your resource ruling lives on** | DATA | `GroundForcesDB.cs:185` |

So the picture on screen is drawn off the *global* hex; the *mini*-hex is where the ore rule bites — but today it
sits parked at muster (0,0), used only in the city-zoom. Closing that gap is part of the work.

**The one move order (the law).** Your rule — *"if the AI can't drive it with the same primitive the player uses,
it's too complex"* — is violated right now. There are **two doors for one verb:**

- The **AI** issues a move by dropping a `GroundOrder.MoveHex` into the unit's order queue
  (`GroundForcesDB.cs:342`), which the processor runs (`ProcessFormationOrders`). **Correct door.**
- The **player**, click-to-marching on the map, calls the engine *directly*
  (`GroundForces.OrderMoveToGlobalHex`, `PlanetViewWindow.cs:581`; `OrderMove`, `:1172`) — **skipping the queue.**
  That order carries no "who ordered this" tag, can't be sequenced, and **the AI literally cannot use that path.**

| Verb | LIVE today | Must be built |
|------|-----------|---------------|
| **Draw** a unit token | ✅ grouped tokens off the global hex (`PlanetViewWindow.cs:443`) | select **one** unit (the `UnitId` exists — pure UI, **Failure A**) |
| **Read** a unit | ❌ only a group caption (`:543`) | a per-unit stat card — every number (Attack/Defense/Health/`Range_m`/loadout) already lives on `GroundUnit`, never surfaced |
| **Move** a unit | ✅ pathfinder + processor-walk both work | **collapse to ONE queued MOVE order both seats issue; delete the direct client call** |

**The finish line for §1:** click one unit → read its sheet → give it a MOVE through the *same* queue the AI uses.

---

## 2. Why hauling becomes mandatory (the resource-locality consequence)

**The point:** today all ore lands in one colony bin and every factory drinks from it — location means nothing.
Your ruling breaks the bin into little per-hex buckets. The instant you do, **carrying stuff becomes a real job.**

**How it works today (no locality anywhere).** A mine pulls from the *whole planet's* deposit and dumps into the
colony's **one** cargo store (`MineResourcesProcessor.cs:90`). A factory consumes from that **same one** store
(`IndustryTools.ConsumeResources`). One pot fills, the same pot empties. The per-hex deposit numbers you already see
on the map (`GroundHex.DepositMineralId`/`DepositAmount`) are just a *picture* of where the ore lies — nothing
spends them. **DATA, not plumbing.**

**What the ruling forces — four welds:**

1. **The hex deposit becomes the real tank.** A mine draws from and depletes the deposit on *its own* hex, not the
   planet-wide pool. (Promotes `GroundHex.DepositAmount` from picture to source-of-truth.)
2. **Each hex gets its own stockpile.** A place to *hold* refined goods locally. **This field does not exist yet** —
   `GroundHex` holds a deposit + a building-id list, no "goods on hand." A new field → the DataBlob discipline:
   `Clone()` + copy-ctor + `[JsonProperty]` (landmine L12).
3. **A facility eats only what's in its own hex — or it starves.** `ConsumeResources` must read the *local* bucket,
   not the colony pool. This is the whole point: no free teleport of materials.
4. **Therefore hauling is mandatory.** A shipyard hex fed by a mine three hexes away needs something to physically
   carry the ore. **That carrier does not exist today** — see §3.

**Cradle-to-grave for a per-hex resource** (name every rung, or it's not really in the game):

> iron in the ground **on a specific hex** → a **mine** installed on that hex digs it → **hauled** to a **refinery**
> hex → refined **material sits in that hex's stockpile** → the **shipyard** hex needs it → **a hauler carries it
> over** → **consumed** to assemble the hull → shipyard or hauler **destroyed** → you re-mine, re-haul, rebuild.

Every rung but two is LIVE or a small add. The two genuinely new rungs are **the per-hex stockpile** (weld #2) and
**the carry step** (§3).

---

## 3. The hauler + trade — the smallest thing that carries cargo, at two scales

**The point:** you need the smallest possible "pickup truck" — a unit whose job is to move a load from hex A to hex
B — and a *route* so you set it once and forget it. Good news: the **load/unload machinery already exists and works
for both seats.** The gap is (a) nothing moves goods **between two hexes of the same planet**, and (b) there's no
place to *decide* what to ship.

**The hauler is a COMPONENT, not a magic flag.** Per cradle-to-grave and `CONVENTIONS.md` §6, "can carry cargo" is a
**part you research → build → bolt onto a ground unit → can lose** — not an engine abstraction. A hauler is any
ground unit carrying a cargo-hold component; kill it and the ore is stranded. That's what buys research-gating,
construction, save/load and the design UI **for free.**

**What EXISTS to build on:**

| Piece | What it does | Grade |
|-------|-------------|-------|
| `CargoTransferAtb` → `CargoStorageDB` | the hold — install the part, you get the cargo bay | **LIVE** (`Storage/CargoTransferAtb.cs`) |
| `CargoTransferOrder` → `CargoTransferProcessor` | **the one order both seats use to move a load between two things standing together** — meters mass at a real kg/s rate | **LIVE** (`Storage/CargoTransferOrder.cs`) — this is your load/unload verb |
| `LogiBaseAtb` → `LogiBaseDB` → the bidding loop | the automated "freight market" that runs colony→colony routes | **structure LIVE, but INERT** — nobody seeds demand, no UI sets it (`LogisticsProcessor.cs:114` early-returns when `DesiredLevels` is empty) |
| faction `Ledger` + `Trade` category | the books, with a slot for commerce income | **LIVE**; `TradeIncome` payout is scaffold, switched off |

**What's NEW (the actual build):**

- **A hex-to-hex HAUL** — moving goods between two hexes of the *same* body. Doesn't exist. (The nearest cousins
  carry *troops* ship↔ground or *build-parts* ship→region — neither moves ore hex-to-hex.) But it's
  `CargoTransferOrder` **twice** — pick up at the mine hex, march to the shipyard hex, set down — bracketed by the
  §1 MOVE.
- **The per-hex stockpile** (from §2) — the thing a haul loads from and unloads into.
- **The TRADE DECISION** — *what* to ship and *how much to keep on hand.* Today `LogiBaseDB.DesiredLevels` ("keep
  1,000–5,000 iron here") is **never set by anything** and there's no screen to set it, so the whole freight loop
  sits idle. **This is the real missing rung.**

**Both seats, always.** The haul and the "set import/export" decision must each be **one order** issued the same way
the AI issues it (through the order queue / `Game.OrderHandler`, exactly like the existing "build here" orders) —
**never a player-only panel with a crude AI shortcut behind it.** That's the trap the law forbids.

**Two scales of trade fall out of the same verb:**

- **Intra-planet (the near one):** mine hex → shipyard hex. A standing HAUL route = the new haul verb, repeated,
  over the per-hex stockpiles. **This is the one the locality ruling demands.**
- **Inter-place (the reach):** province→province, or **colony→colony for money.** This is the *existing* logistics
  loop — already the right shape, just starved. Light it up: seed `DesiredLevels`, add the import/export order
  (both seats), finish the trade-ship's job list, and wire `TradeIncome` into the Ledger's `Trade` bucket so a route
  actually pays.

---

## 4. The first prototype cut (planetview.html rev-T)

The prototype already draws the mini-hex map, the five provinces (Ferrum Basin mining district; Naval Yards shipyard
in the capital), the resource nodes, and grades everything with LIVE/DATA/THEORY badges. rev-T adds the **fewest new
marks** to make the whole chain **visible and clickable** — no engine claims; every new mark is badged **DESIGN**
with its LIVE anchor named. Four things:

1. **Unit tokens on mini-hexes** — a 🚛 hauler on the Deep Cut Mine and a 🪖 garrison in the company town (Ferrum
   Basin), a 🛡 garrison in the capital — drawn with a health bar, like a ship's token. *(Note: the engine draws
   from the global hex today; the mini-hex token is the proposal.)*
2. **Click-to-select ONE unit** → a stat card opens (Health / Attack / Defense / strike range / loadout / cargo),
   captioned honestly: *"every number here already lives on `GroundUnit` — this is the missing select-one/read-one
   UI (Failure A), not a missing number."*
3. **A MOVE order** — with a unit picked, click any mini-hex; the A* path draws across the grid to a 🚩. Labelled
   *"issued through the ONE order queue — the same verb the AI uses (One-Verb-Both-Seats)"*, noting the pathfinder +
   processor-walk under it are **LIVE**.
4. **Trade/haul routes** — an **intra-province** dashed arrow (⛏ mine → 🏭 smelter) on the mini-grid, and a
   **regional** dashed arrow on the main map (Ferrum Basin ⛏ → Naval Yards ⚓, "ore → shipyard"). Captioned: *"the
   load/unload verb (`CargoTransferOrder`) is LIVE; the per-hex stockpile and the hex-to-hex haul are NEW."*

A new **🪖 Units & trade** toolbar toggle shows/hides all of it. Verified headless (both themes, 0 console errors;
unit select → stat card, move order → path, haul + regional routes render, toggle clears, planet-switch clean).

---

## 5. Build order — welds in dependency order

Each slice names what it plugs into. **Do not stack a slice on an unbuilt one** (pre-flight rule #6).

| # | Slice | Plugs into | Depends on |
|---|-------|-----------|-----------|
| **U1** | **Select-one + read-one unit** (UI wiring) | client `PlanetViewWindow` draw + `GroundForcesDB.Units` (the `UnitId` handle) | nothing — pure UI (Failure A) |
| **U2** | **Collapse MOVE to one queued verb** both seats issue; delete the direct client call | the ground order queue (`GroundFormation.Orders`) + `ProcessFormationOrders` + the AI tactical brain | U1; the M1 One-Verb ruling |
| **R1** | **Per-hex resource locality** — hex deposit = source-of-truth + per-hex stockpile field + facility eats local | `HexMinerals` / `MineResourcesProcessor`, `GroundHex` (add stockpile w/ `Clone`+ctor+`[JsonProperty]`, L12), `IndustryTools.ConsumeResources` | **the locality ruling** |
| **H1** | **Hauler component + hex-to-hex HAUL order** | `CargoTransferAtb`/`CargoStorageDB` (the hold), `CargoTransferOrder`/Processor (load/unload verb), the order queue (both seats) | R1 (nothing to carry until ore is local) |
| **T1** | **Intra-planet route** (mine hex → shipyard hex) = a standing haul over per-hex stockpiles | H1 + the per-hex stockpiles | H1 |
| **T2** | **Inter-place route + money** — seed `DesiredLevels`, import/export order (both seats), finish the trade-ship job list, wire `TradeIncome`→Ledger `Trade` | `LogiBaseAtb`/`LogiBaseDB`/`LogisticsProcessor`, the `Ledger` | independent of R1/H1; can run in parallel after U2 |

**Blast radius (open `docs/SYSTEM-CONNECTION-MAP.md`, add any row this surfaces before writing code):** the
**surface grid** (global hex draws the token; mini-hex is where locality lives) · the **order queue** (one MOVE
verb, one HAUL verb, both seats) · **per-hex resources** (`GroundHex` + `HexMinerals`) · **logistics**
(`LogiBaseDB` + `CargoTransferOrder`) · the **Ledger** (money for inter-place trade).

**Two rulings everything here rests on — both flagged:**

- **(i) Workforce → production staffing** (`ENGINE-WIRING-BACKLOG` TIER 2.6; prototype rev-R). One finite pool of
  people crews the ships, leads the units, *and* staffs the factories. **A hauler unit draws crew from that same
  pool** — building trucks competes with building hulls. This chapter must respect that shared tank, not open a side
  account.
- **(ii) Resource mini-hex locality** — *the* load-bearing ruling. Without it, ore stays in one colony pool, hauling
  has no reason to exist, and R1/H1/T1 have nothing to stand on. **Nail the ruling before R1.** (Recorded as a
  missing-edge in `SYSTEM-CONNECTION-MAP.md`.)

---

## Honest state, in one line

The map **draws units and kills them like ships (LIVE)**; the **pathfinder, the queue, and the load/unload verb are
all LIVE** — but **you can't pick a unit, can't read one, move it through the wrong door, and no per-hex bucket or
hex-to-hex truck exists yet.** This chapter welds those five gaps in the order above.

---

## Appendix — grounding ledger (file:line, re-verified against source 2026-08-10)

| Claim | Source |
|-------|--------|
| Unit = data object in a list on the planet body, stable `UnitId` | `GroundCombat/GroundForcesDB.cs:28` |
| Four position tiers (Region / RegionHex / Global / Mini) | `GroundForcesDB.cs:46,155,174,185` |
| Whole-or-dead removal at Health ≤ 0 | `GroundForcesProcessor.cs:334` |
| Queued MOVE verb (the AI's door) | `GroundForcesDB.cs:342` (`GroundOrder.MoveHex`) |
| Client bypasses the queue (the second door) | `PlanetViewWindow.cs:581` (`OrderMoveToGlobalHex`), `:1172` (`OrderMove`) |
| Client draws grouped tokens / selects a group / reads a group caption | `PlanetViewWindow.cs:443,63,543` |
| Mine → one colony pool; factory ← same pool (no locality) | `MineResourcesProcessor.cs:90`; `IndustryTools.ConsumeResources` |
| `GroundHex` carries deposit + installations, **no stockpile field** | `Galaxy/GroundHex.cs:34,38,58` |
| Cargo hold component + the load/unload order (both seats) | `Storage/CargoTransferAtb.cs`; `Storage/CargoTransferOrder.cs` |
| Freight market exists but inert (no seeded demand) | `Logistics/LogiBaseDB.cs:17`; `LogisticsProcessor.cs:114` |

*Companions: `docs/ground/PLANETARY-VIEW-AND-INTERACTION-DESIGN.md` (the planet board this sits on) ·
`docs/assembler/ENGINE-WIRING-BACKLOG-2026-08-06.md` TIER 2.6 (the workforce-staffing ruling) ·
`docs/SYSTEM-CONNECTION-MAP.md` (the two missing-edge rows) · `docs/AUTO-RESOLVER-GROUND-TRUTH-2026-07-29.md` §11
(the "units work like ships" north star).*
