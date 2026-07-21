# Operation Earthfall — the invasion, step by step, as BUILT

**What this is.** You asked for the whole planetary invasion written out as *"each individual step and click."* This is it — the complete flow of taking a planet, told twice: once from the **AI's seat** (the rungs the computer opponent climbs on its own) and once from **your seat** (the actual windows and buttons *you* press to do the same thing). Both seats ride the *same* machinery — that's the point of the campaign. The AI isn't running a special hidden path; it pulls the same levers you do, just decided by code instead of a mouse.

Read the "what it does" line first, then the plumbing. Every step names the real file so a future session (or you, weeks from now) can find it.

> **Shipboard analogy for the whole thing.** Taking a planet is like a shore assault off a ship: you build the landing force, load it into the boats, win control of the water overhead, shell the beach, land the troops, dig in a supply dump on the sand, push inland, blow the enemy's bunkers, land the second wave, and finally raise your flag over the town. Every one of those is now a real step in the engine. Below is the watch-bill.

---

## 0. The one idea that makes both seats work

A **battalion** is the ground twin of a **fleet**. A fleet is a bag of ships that move and fight as one; a battalion (`GroundFormation`) is a bag of ground units that move and fight as one. Before this campaign the AI could *build* ground units but never *group* them — and stance, rules-of-engagement, and orders all live on the group, not the loose unit. So a loose AI unit had no officer and no orders. The fix (`GroundAssembly.FormUpLoose`) sweeps loose units into battalions the moment they're raised or land — for the AI. You always had the button; now the AI has the reflex.

The other idea: **the hex is the unit of everything.** Regions are just a visual aid — a coloured band on the globe. Landing, blowing up buildings, and who-owns-what all resolve at the **hex** level (`GroundHex`). When this doc says "a region," picture the band; the real bookkeeping is per-hex underneath it.

---

## 1. THE AI's invasion — the rung sequence (what the computer opponent does on its own)

The strategic brain (`NPCDecisionProcessor.Tick`, fires daily) decides *what* the faction wants — and when a militarist faction that's at war and winning wants to take a rival's world, it settles on the **Conquer** objective. The **`ConquerResolver`** (`GameEngine/Factions/ConquerResolver.cs`) is the officer that turns "Conquer" into one concrete action per cycle. It's a ladder of rungs, checked **highest-priority first** — it does the most-finished thing it can, and if it can't, it does the next thing that unblocks it. Here is the ladder, from "nothing built yet" up to "flag raised":

**Rung 3 — MASS the warships.** *What:* build and gather a strike fleet. Until the faction has a real fist (≥ 3 armed hulls, `MilitaryComposition.ReadyStrikeFleet`), every cycle it queues another warship and `FleetAssembly` sweeps finished hulls out of the invisible root-fleet park into a real in-system fleet. It deliberately keeps a **home-defence reserve** (`ConquerResolver.HasHomeReserve`/`ShouldStopMassing`) so it never sends its entire navy — the military-commander's job. *Anchor:* `ConquerResolver.cs`, Rung 3; `Fleets/FleetAssembly.cs`.

**Rung 2 — BUILD the troop transport.** *What:* the invasion needs a boat, so the resolver queues ONE troop transport. *The fix this campaign added:* it now checks `FactionHasTransportQueued` first, so a shipyard with four build lines doesn't queue four transports and choke its own industry (the real-game bug that strangled Mars — findings A4). One in production is enough. *Anchor:* `ConquerResolver.cs` Rung 2; gauge `EfSealiftQueueGuardTests`.

**The BUILT hull now actually sails.** When that transport finishes, it used to boot with a dead reactor and empty tanks — a boat on the slipway that couldn't move (findings A4). Now every production-built hull is charged and fuelled the instant it's built (`ShipDesign.ProvisionBuiltShip`, called at both build paths), so it can spin a warp bubble immediately. Its launch pad also has fuel now (Mars was given a fuel farm + methalox/NTP, matching Earth). *Anchors:* `Ships/ShipDesign.cs` `ProvisionBuiltShip`; `Industry/LaunchComplexProcessor.cs`; gauge `EfSealiftEndToEndTests` drives the whole BUILD→LAUNCH→LOAD→SAIL chain.

**Rung 1.5 — LOAD the troops.** *What:* with a transport parked over its own colony, the resolver loads a garrison unit aboard (`LoadTroopsOrder` → `GroundTransport.TryLoadUnit`). *Anchor:* `ConquerResolver.cs` Rung 1.5.

**Rung 1.3 / 1 / 1b — SAIL to the target.** *What:* a loaded transport that isn't at the enemy world yet warps there (Rung 1.3 `SailTransport`); the escorting warfleet sails too (Rung 1 `StrikeFleet`), gated on the odds meeting the faction's Risk trait (`CombatRisk.WouldEngage` — a bold faction commits at even odds, a cautious one waits for 2:1). If the target is in another star system with a discovered jump route, Rung 1b (`StrikeJump`) sails the next leg through the gate, one gate per cycle. *Anchor:* `ConquerResolver.cs` Rungs 1/1b/1.3; `MilitaryReach.cs`, `JumpRouter.cs`.

**The one-shot orbital bombardment — soften the beach.** *What:* once warships hold the orbit, firing on the colony from space drains health off the *defending* garrison before the troops land (`DamageProcessor.OnColonyDamage` → `ApplyGroundBombardment`). A shielded/armoured defender resists it. This is a **single softening pass** — there is deliberately **no repeated bombardment cadence** (you tabled the "re-fire between waves" idea; see the deferred list). *Anchor:* `Damage/`; gauge `GroundBombardmentTests`.

**Rung 0 — LAND the invasion (and form up).** *What:* a transport that's *at* the target world, still carrying troops, holding the orbit → it drops the troops into a landing zone (`LandTroopsOrder` → `GroundTransport.TryLandUnit`), and **immediately forms the landed unit into a battalion** (`GroundAssembly.FormUpLoose`) so the tactical brain has hands to command. Landing and ownership resolve **per hex**. *Anchor:* `ConquerResolver.cs` Rung 0.

**Rung 0a — LAND the beachhead parts (the supply dump on the sand).** *What:* if the invaders hold a patch of ground and have a ship overhead carrying crated **building parts** in its cargo, the resolver lands one crate per cycle onto that ground (`GroundParts.LandPartsFromShip`). Then, over the following ground ticks, a landed **combat engineer** (a unit carrying a `GroundConstructorAtb` component) *erects a fortified building on the spot with no colony present* (`GroundBeachhead.TickBuilds`) — a forward operating base. That FOB fortifies the position, becomes a resupply point, and is itself a target that can be bombed back off the map (the grave rung). *Anchors:* `ConquerResolver.cs` Rung 0a; `GroundCombat/GroundParts.cs`, `GroundCombat/GroundBeachhead.cs`.

**The TACTICAL BRAIN — pick a posture (the officer of the deck).** *What:* every ground tick, for each of its battalions, the AI reads the *detected* enemy strength around it (fog-honest — an enemy it hasn't scouted counts as zero) and decides a posture: **Offensive + close + advance** when it has the edge its personality demands; **Defensive / dig-in + hold** when outnumbered or on a fortified line; **Retreat** toward a friendly beachhead when it's losing badly and has somewhere to fall back to (cornered, it digs in instead — no suicide marches); **Balanced / stand-off** at parity (it uses its range advantage). The odds bar is the *same* curve the space fleets use, so the UMF is recognizably the UMF on the ground and in orbit. This is the answer to your question "is the AI smart enough to know when to be defensive vs offensive" — today, yes. *Anchors:* `GroundCombat/GroundThreat.cs` (the fog-honest read), `GroundCombat/GroundTactics.cs` (`DecidePosture` — the pure decision), `GroundCombat/GroundTacticalBrain.cs` (the wire); design `docs/earthfall/GROUND-TACTICAL-AI-DESIGN.md`. Every decision records a plain-English **Reason** you can read back.

**Rung 0b — RAZE the enemy's infrastructure (blow the bunkers).** *What:* a battalion the brain has put in an **Offensive** posture, standing within reach of an enemy building's hex, is tasked to destroy it (`GroundOrder.DestroyInfra`). The building is staged-drained until it's rubble; the hex it sat on can also be **captured** (`CaptureInfrastructure`), flipping ownership and stopping that bunker from fortifying the enemy. Only an Offensive battalion gets siege tasking — a dug-in defender doesn't. *Anchors:* `ConquerResolver.cs` Rung 0b; `GroundCombat/GroundForcesProcessor.cs` (`ResolveInfraOrder`); gauge `EfGroundInfraCombatTests`.

**The SECOND WAVE and CAPTURE.** *What:* the resolver keeps its ladder turning — if the first wave is thinning, it builds/loads/sails more (the rungs recycle), lands them, forms them up, and the tactical brain advances them. When a region's garrison is cleared and an invader holds it, ownership flips (`GroundHex.OwnerFactionID`); when every region on a world flips, the colony itself changes hands (`TryCapturePlanet`). That last flip is "you took the planet."

**And the AI won't abandon its own invasion.** The real game's ugliest bug was the UMF walking away from a winning invasion because a one-month phantom rebellion flipped it to "Defend" and locked it there for 180 days. Three fixes close that: (1) a winning in-flight Conquer is *protected* from a transient internal wobble (`ObjectiveTransition.ShouldProtectInFlightConquest`); (2) if the flip to Defend is *genuine*, the faction actually **recalls its fleets home** instead of leaving them coasting at the enemy (`DefendResolver` Rung 0); (3) a passed crisis can't lock the brain — the commit releases the instant the thing that forced it clears (the hysteresis break-glasses). *Anchors:* `Factions/ObjectiveTransition.cs`, `DefendResolver.cs`; gauges `EfOperationContinuityTests`, `EfHysteresisBreakGlassTests`.

---

## 2. THE PLAYER's invasion — the windows and buttons for the same acts

Everything the AI does above, you do by hand through the UI. Same engine calls underneath — the AI just skips the mouse. Here is your click-path, in order.

### Act 1 — Design a unit (the Entity Assembler)
- **Open the component designer / Entity Assembler** (`ShipDesignWindow`, ground branch). Pick a **chassis** (Personnel / Vehicle / …) and mount **components** onto it: a weapon, armour plating, a locomotion drive, a radar, a magazine for ammo, a **sealed-systems** component if the world you're invading has no air or poison air, and — for an engineer — a **ground-constructor** component. The unit's *role* emerges from what it carries; there is no "scout unit" or "engineer unit" type, only a chassis carrying the parts that make it one.
- The readout now shows the numbers that used to be invisible: **Training** (veterancy), **Power (draw vs supply)** in red if you've over-drawn your reactor, and **Ammo Capacity**. If the design is illegal (guns out-draw the reactor, an ammo weapon with no magazine), it says so.
- Click to register the design (`GroundUnitAssembly.RegisterAssembledDesign`). It's now a buildable, exactly like a ship part. *Anchor:* `Pulsar4X.Client/.../ShipDesignWindow.cs`; `GroundCombat/GroundUnitAssembly.cs`.

> This is where **your Space Marine** lives. There is no hard-coded marine in the game — you *design* one: a sealed, veteran-cadre, power-armoured unit, built from these generic parts. It rides the exact same rails everything below describes.

### Act 2 — Build it (the colony)
- Open the colony (Colony Management → Production). Queue the unit design like any installation; it consumes minerals over build time and, when it finishes, **fields the unit on the planet** (the `OnConstructionComplete` hook raises it). *Anchor:* the industry UI you already use; `GroundUnitDesign.OnConstructionComplete`.

### Act 3 — Form a battalion + set its orders (Force Management)
- **Open Force Management** (the retitled Fleet window — `FleetWindow.cs`; toolbar button "Force Management (fleets + battalions)"). It has two tabs: **Fleets** (unchanged) and **Battalions**.
- The **Battalions tab** lists your battalions across *every* world — location, strength, health. Select one to:
  - **Move** it (region / global-hex picker),
  - set its **Stance** (Offensive / Defensive-Dig-In / Balanced) and **ROE** (Hold Ground / Close-to-Engage / Stand-Off) — the same levers the AI's brain sets,
  - view/queue its **order queue** ("march here, then dig in"),
  - **Rename** it.
- *Anchor:* `FleetWindow.cs` (`DisplayBattalions`, `DrawBattalionOrders`).

(To form loose units into a battalion, the planet view's formation panel is the other door; and in a menu-started game your landed AI opponents auto-form theirs — see the gate flip in the campaign report.)

### Act 4 — Embark and land (the invasion lift)
- **Open Force Management → Fleets → Issue Orders** on a fleet that has a **troop-bay ship**. A new order appears: **"Embark / land troops …"** (it only shows if a ship in the fleet actually has a troop bay).
  - The **Embark** section lists your ground units on the body you're orbiting, with a per-class bay-capacity readout — click **Load** to put a unit aboard (`LoadTroopsOrder`).
  - Fly the ship to the target world.
  - The **Land** section (enabled only once you **hold the orbit**) has a **region picker** — pick the landing zone and click **Land** (`LandTroopsOrder`, region-index addressed). The unit drops into that zone.
- *Anchor:* `FleetWindow.cs` (`DisplayTroopOrders` / `DrawEmbarkSection` / `DrawLandSection`); gauge `EfC5TroopLiftOrderTests`.

### Act 5 — Read the ground, push, and raze (Planet view)
- **Open the planet view** (`PlanetViewWindow`). Select a unit group and the globe **tints its reach**: **red** = weapon reach, **green** = radar reach — so you can see who you can hit and who you can see, fog-honestly (your own units only). *Anchor:* `PlanetViewWindow.DrawGlobalHexWindow`.
- To **raze or seize** an enemy building: with a battalion standing on/near it, the battalion order surface and the city-zoom both offer **"Raze / Capture infrastructure"** buttons, which queue the same `DestroyInfra` / `CaptureInfra` orders the AI's Rung 0b uses. *Anchor:* `FleetWindow.cs` / `PlanetViewWindow.cs` (`DrawBattalionInfraOrders` / `DrawCityInfraOrders`); gauge `EfPwInfraButtonContractTests`.
- Clear a region's garrison while you hold it and ownership flips; clear the whole world and the colony is yours.

---

## 3. The support loop (the stuff that keeps an army in the field)

An army is not a one-shot — it eats. The campaign wired the sustainment loop so the ground war has the same logistics teeth the space war has:

- **Ammo.** A unit with a magazine burns ammo when it fires (`AmmoPerSalvo_kg`); a **dry** unit is silenced — it stops shooting until resupplied. The tactical brain reads this: a dry battalion never picks Offensive (a silent gun line doesn't charge). *Anchor:* `GroundForcesProcessor.ResolveRegionCombat`.
- **Resupply.** A magazine unit standing in a friendly-held region that has a base — a colony installation *or* your landed beachhead FOB — auto-rearms each tick (`GroundForces.ResupplyUnit`). This is why the beachhead matters: it's a supply dump on the enemy's shore, and losing it costs you your line of resupply *and* your line of retreat. *Anchor:* `GroundForcesProcessor` step 0d; `GroundBeachhead.HasBeachhead`.
- **Upkeep.** A standing army now **costs money every month just by existing** (`GroundUpkeep.BillIfDue`), billed to the owning faction's ledger — so a garrison is a real economic weight, not a free wall. *Anchor:* `GroundCombat/GroundUpkeep.cs`.
- **Fire support.** The one-shot orbital bombardment (Section 1) is your naval gunfire — soften the beach before you land. It respects the defender's armour and shields.

---

## 4. The honest LIMITS — what is NOT there yet (so nobody trips on it)

Plain and honest, because "it's built" should never mean more than it does:

1. **CI can't run the client.** Everything above is proven *green in the engine* (the code paths are tested), but the client windows, the map draw, and the whole-invasion *feel* are only verifiable on **your** Windows build. The runtime checklist for these is in `docs/CLIENT-TEST-CHECKLIST.md` (Operation Earthfall sections) and `docs/TESTING-TRACKER.md` (the Layer-3 backlog). Until you play it, "built" means "the logic is right," not "it feels right."
2. **No repeated bombardment.** You get *one* softening pass, not a bombardment you can re-fire between waves. You tabled that cadence deliberately — it's parked, not lost.
3. **No orbital strike aimed at a specific hex's buildings.** Bombardment softens the *garrison* surface-wide; you can't yet pick a single bunker from orbit and drop a shell on it. Deferred (the "W-track" follow-on).
4. **Regions are cosmetic.** They're a coloured band for your eyes; all the real ownership/landing/raze bookkeeping is per-hex. There is no "transfer a region" logic — capturing is per-hex.
5. **Resupply is instant-to-full and range-free.** A unit at a friendly base tops off completely with no supply-line distance or throughput model. Good enough for v1; a real supply-line is a later slice.
6. **A dry mixed-weapon unit goes fully silent.** The aggregate resolver can't yet fire a unit's energy weapon while its ammo weapon is dry — a dry magazine silences the whole unit. Fine while ground units are mostly single-type; revisit if mixed loadouts get common.
7. **A captured enemy building doesn't yet produce for you.** Capturing a hex stops the enemy's bunker from fortifying *them*, but a captured factory doesn't start building for *you* until the whole colony flips. (Aurora does the mid-siege production hand-over; it's the biggest deferred piece — findings R4 decision #4.)
8. **The tactical-brain thresholds are first-cut numbers.** The six dials that shape the AI's aggression (when it retreats, how much an unscouted flank scares it, etc.) ship at sensible defaults **flagged** for you to tune from live play. Same for ammo-per-salvo, upkeep rates, and the beachhead build-rate. None of these are balanced yet — they're honest starting points.
9. **An AI-founded new colony is born empty and untaxed** (the DEV-lane finding): it's real and enrolled but earns nothing until it grows. Whether a founded colony seeds a starting population or a governor sets its tax rate is a design decision you own.

---

## 5. The one-line summary

The chain **build → load → win orbit → bombard once → land → dig in a beachhead → let the brain pick a posture → advance → blow the bunkers → land the second wave → capture** is now a real, connected sequence in the engine — climbed automatically by the AI's `ConquerResolver` + tactical brain, and driven by hand through the Entity Assembler → colony → Force Management → Issue Orders → Planet view windows. Your Space Marine is your own live-designed unit riding those generic rails. What's left is your local play-test to make it *feel* right, and the flagged numbers to tune once you've watched it happen.
