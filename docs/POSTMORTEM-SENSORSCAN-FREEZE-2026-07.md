# Postmortem — The SensorScan Freeze Hunt (2026-07-16 → 17)

> **Why this doc exists:** the developer's own words — *"my last commit was a difficult place to get to last night and I
> never want to get to the point again."* This is the full record: what the bug was, everything we tried, what we got
> wrong, how we finally caught it, and the rules that make sure we never spend a night chasing this class of bug again.
> If you are about to touch the sensor scan, the combat time-loop, or any self-scheduling processor, **read this first.**

---

## The one-line lesson

**A self-rescheduling processor must book its own next run EXACTLY ONCE per invocation.** If that "book the next run"
line ever sits inside a per-component (or per-anything) loop, the number of pending events multiplies by the loop count
**every cycle** — geometric growth that freezes the game. And: **when the game freezes, read the gauge before you touch
code — don't fix the system you were last working in.**

---

## The symptom (what the developer saw)

The DevTest game **froze** during continuous fast-forward. Not a crash — no error dialog, no stack trace. Game-time
just **crawled to a standstill** while the machine still ran. The client's SIM-STALL watchdog printed
`[SIM-STALL] SensorScan STILL CLIMBING` — the clock was stuck and the `SensorScan` counter was exploding.

This is the **worst kind of fault to diagnose**: a freeze leaves no trace. The log simply stops. It reads identically to
a crash, a native access violation, or an infinite loop — you cannot tell which from the outside.

---

## What it actually was (the root cause)

`SensorScan.ProcessEntity` walks an entity's **sensor receivers** (one pass per installed sensor component) to do
detection. The line that books the entity's **next** scan —
`manager.ManagerSubpulses.AddEntityInterupt(now + scanTime, "SensorScan", entity)` — was sitting **INSIDE that
per-receiver loop.**

So an entity with **K** sensor receivers booked **K** next-scans every time it scanned. Next cycle, each of those K
scans booked K more → K² → K³ … **exponential growth with base K.** A colony with **2** receivers doubled its pending
scan count every cycle: 2, 4, 8, 16 … **2¹⁸ = 305,426** by cycle 18. The instance queue drowned; the clock couldn't
advance because the scan processor never came up for air.

The developer's own instrumented log named it exactly:
`#283(owner=281,colony,ScanTime=3600s)=305426` while **every other entity in the game sat at ~50.** A single
2-receiver colony, doubling; everyone else flat. That contrast *is* the signature of a per-loop reschedule.

**The fix (`449048a`):** track the shortest scan interval across the receivers inside the loop, then book the next scan
**once**, after the loop, guarded on `InstanceStates.Count > 0`. K receivers → 1 reschedule. Flat, not exponential.

---

## The full timeline — everything we did, including the misses

### Attempt 1 (MISS): we fixed the wrong system first

The freeze arrived right after a big combat/fleet-composition merge, so the first assumption was **"it's the combat
SIM-STALL."** We hardened the whole combat freeze class (`001eb60`):
- Guarded every unguarded fleet-tree walk against cycles/diamonds (`FleetCombat.Collect`, `TreeHierarchyDB.TryGetChild`).
- Made fleet-tree mutations maintain the single-parent / no-cycle invariant (`FleetOrder.ChangeParent`).
- Bounded the combat fine-step perf explosion in continuous play (`MasterTimePulse` fine-step cap).
- Added deterministic engine gauges for the freeze class (`CombatFleetTreeSafetyTests`).

**These were all real latent bugs, and the fixes are CI-green and worth keeping.** But they were **not this freeze.**
The developer re-ran and hit the identical freeze ("same thing").

> **The miss:** we fixed the system we had *just been working in*, on a plausible-but-unverified hunch, instead of
> reading what the freeze was actually telling us.

### The turn (Visibility Gate): read the gauge, stop guessing

Rather than pile on a second combat guess, we STOPPED and looked at what the log actually showed:
- **ZERO `[Combat]` narration.** `in-combat = 0`. **No battle ever formed.**
- If combat never engaged, the freeze **cannot** be in the combat resolver.

That single observation redirected the whole hunt. The freeze had to be somewhere that runs *without* combat — and the
SIM-STALL watchdog was already pointing at `SensorScan`.

### Attempt 2: build the culprit-naming gauge

The global `SensorScan.ScanCount` said "scans are exploding" but not **which entity** was responsible. So we built the
gauge that would name it (`2236162`):
- Per-entity scan **attribution** (`ScansByEntity`, off by default → zero overhead).
- The SIM-STALL watchdog now **dumps the top scanners** when it fires (id / owner / kind / ScanTime / count).
- A CI reproduction test (`SensorScanStormTests`).

### Attempt 3 (MISS): the reproduction test hung a CI shard

The first storm test advanced **3 game-hours single-threaded with the combat flags on** through the building storm —
which pinned a CI runner for ~20 minutes. Worse: **NUnit `[Timeout]` cannot abort a synchronous CPU hang on .NET 8**
(`Thread.Abort` is unsupported), so the test could not save itself.

> **The miss:** a reproduction of a HANG that is not itself hang-proof becomes a *second* hang.

We made it hang-proof (`24d4254`): the clock advance runs on a **background task with a hard wall-clock cap** and
**bails the instant the storm is evident**, so a wedging sim fails the test fast instead of pinning the runner.

### Attempt 4: the storm wouldn't reproduce in the CI harness

The storm needed **~15+ game-hours of continuous play** to detonate (2¹⁵ before the count is obviously huge), and the
lightweight test harness didn't reproduce the client's continuous month-stepping regime. So we pivoted to
**instrumenting the actual client** where it reproduced (`3a74cce`): the SIM-STALL watchdog names the top scanners in
the developer's own `game_logs/`.

### The catch

The developer ran the instrumented client and committed the log (`a175eab`). It read:
`#283(...colony,ScanTime=3600s)=305426` vs everyone else ~50. **≈ 2¹⁸.** Exponential. That number pointed straight at a
reschedule that multiplies — which is exactly the per-receiver-loop reschedule. Fixed in `449048a`. Confirmed by the
developer's next run: the scan count **plateaued** (`+0` per heartbeat) instead of exploding. "Good signs."

---

## What we still got wrong (own it, so we don't repeat it)

1. **We fixed the wrong system first** (combat), costing a full pull→build→re-freeze round-trip with the developer,
   because we trusted a hunch ("the freeze is in what I just merged") over the gauge.
2. **We over-scoped the reproduction test** and hung a CI shard — and only then learned `[Timeout]` can't abort a
   sync hang on .NET 8. A repro of a hang must be born hang-proof.
3. **We under-estimated how long the exponential takes to show** — short/small tests passed while the real game froze,
   because 2ⁿ hides in the single digits for the first several cycles.

---

## How we finally got there — the Visibility Gate did the work

Every step that moved us forward was **building a gauge, not writing a fix**:
- The redirect came from *reading* the log (zero combat narration), not from a new guess.
- The root cause came from the **per-entity attribution** we built — it turned "scans are high" into
  "#283 = 305,426, everyone else = 50," and that contrast *is* the diagnosis.
- The fix was almost trivial **once the gauge named the culprit.**

> **The freeze was the one fault with no gauge. The moment we built the gauge, the bug had nowhere to hide.**

---

## The permanent rules (how we never do this again)

1. **Read the gauge before you touch code. Do NOT fix the system you were last working in.** A freeze that arrives after
   you merged system X is not evidence the freeze is in system X. Look at what the sim was actually doing (here:
   *zero combat* ruled out the combat resolver in one line).
2. **A self-rescheduling processor books its next run EXACTLY ONCE per invocation.** Never inside a per-component /
   per-anything loop. If it is, the pending-event count multiplies by the loop count every cycle — geometric, not
   linear. This is nastier than an ordinary busy-loop because it *hides* on small/short tests. (Gauge:
   `SensorScanStormTests` — advances long enough and checks PER-ENTITY, not just the global count.)
3. **A reproduction of a HANG must itself be hang-proof.** Run the suspect advance on a **background task with a
   wall-clock cap + early bail**. NUnit `[Timeout]` **cannot** abort a synchronous CPU hang on .NET 8 — do not rely on
   it. (Pattern: `SensorScanStormTests`, `CombatFleetTreeSafetyTests`.)
4. **Exponential bugs hide on short tests — attribute PER-ENTITY and advance long enough.** A global counter says
   "something is high"; a per-entity breakdown says *who*, and the *shape* (one entity doubling while the rest are flat)
   names the bug class. Build the per-entity gauge first.
5. **When a fix misses, STOP — build the finer gauge, don't pile on a second guess.** The single most valuable thing we
   did was the culprit-naming instrumentation. It should have been step one, not step three.
6. **A freeze is the fault with no trace — so instrument for it BEFORE you need to.** The SIM-STALL watchdog, the
   per-processor liveness counters (`SensorScan.ScanCount`, `CombatEngagement.TickCount`), and the `[HANG]`/`[FATAL]`
   nets are what make a freeze diagnosable at all. Keep them; add to them; never remove them to "clean up."

---

## Related landmines (already in the codebase docs)

- `GameEngine/Sensors/CLAUDE.md` → "The reschedule-in-loop freeze" (rule #2 above, at the source).
- `GameEngine/CLAUDE.md` → `MasterTimePulse` fine-step cap (the combat PERF-freeze cousin — a *different* freeze in the
  same family: a standing false-positive re-arming a per-hour tax).
- Root `CLAUDE.md` → "The Visibility Gate" (the discipline this whole hunt validated) + the Landmine Index.
- `Pulsar4X.Tests/CLAUDE.md` → `CombatFleetTreeSafetyTests` / `SensorScanStormTests` (the hang-proof gauge board).
