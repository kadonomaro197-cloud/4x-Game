# Espionage & Intelligence — the Hidden-Information Engine (design)

> ### ✅ AS-BUILT (2026-07) — this design is substantially BUILT and WIRED, but ships GATED OFF by default
>
> This document reads as forward design with a build order still ahead of it. That framing is now **stale**: most of the spine below exists in code and is wired end-to-end. It does **not run in a default game** — the two runtime gates default to `false` — so treat this as **built, default-OFF (runtime unverified by CI — CI can't run the client)**, NOT as "working." The build order and "open" sections below are the *history and the remaining calibration*, not a to-do list for the core.
>
> | Designed element | Code home (verified 2026-07-13) | State |
> |---|---|---|
> | **Information Ledger** (per-rival, per-facet Inferred→Confirmed→Stale) | `GameEngine/Factions/InformationLedger.cs` (`IntelFacet`, `IntelLevel`, `InformationLedgerDB`) | Built; kept live by `NPCDecisionProcessor.UpdateInformationLedger` **behind `EnableIntelLedger` (default false)** |
> | **Covert-action catalog** (gather / steal-tech / steal-funds / sabotage / sow-unrest / turn-or-assassinate / disinfo / counter-intel) | `GameEngine/Factions/CovertActionCatalog.cs` (`CovertAction` enum + data) | Built |
> | **Risk model** (clean / traced / caught detection roll) | `GameEngine/Factions/CovertRisk.cs` | Built |
> | **Op resolver + tasking** (grave-rung agent loss on caught) | `GameEngine/Factions/Espionage.cs` (`TaskAgent`), `EspionageProcessor.cs` (`IInstanceProcessor`), `CovertOpDB.cs` | Built and wired: resolves via CovertRisk/CovertActionCatalog, writes the Ledger, applies caught consequences (relation hit + suspicion + agent destroyed) |
> | **Intelligence HQ component** (the capacity seat) + **agent recruitment** | `GameEngine/Factions/IntelDirectorateAtb.cs`, `IntelDirectorateDB.cs`, `IntelDirectorateProcessor.cs` | Built: the `Atb` seats a directorate on install; the processor recruits `Intelligence`-type commanders up to op capacity; stops when the HQ is destroyed |
> | **The NPC mirror** (they spy on you) | `NPCDecisionProcessor.RunEspionageMirror` | Built but **behind `EnableEspionageMirror` (default false)** — so the "always-on mirror" is aspirational at runtime today |
> | **Player-facing window** | `Pulsar4X.Client/Interface/Windows/IntelligenceWindow.cs` | Built (client compiles; runtime unverified — CI can't run the client) |
>
> **The two runtime gates:** `NPCDecisionProcessor.EnableIntelLedger = false` and `EnableEspionageMirror = false` (`GameEngine/Factions/NPCDecisionProcessor.cs:62,77`). Default-off keeps the start byte-identical; a default game runs the NPC brain (which IS built — `Tick` is a full body, not a stub) but no ledger updates and no spy mirror until a client/test opts in. The player can still task an agent by hand through the window — that path (`Espionage.TaskAgent` → `EspionageProcessor`) is not gated. Guard tests: `Pulsar4X.Tests/InformationLedgerLiveTests.cs`, `NPCEspionageMirrorTests.cs`.

**What it does, in one line:** turns "what do they actually intend?" into a thing you can *spend to learn* and *risk getting caught doing* — so diplomacy stops being a spreadsheet of known numbers and becomes a **reading game** where you act on incomplete information, pay to reduce uncertainty, and gamble on exposure.

**Why it matters (and why it's NOT optional):** the developer locked the **FULL hidden-information version** of external politics (`docs/DIPLOMACY-DESIGN.md` → "Making politics FUN"): you do not see a rival's true stance or plans; you infer them from behavior, and **intelligence is the currency that buys the truth.** Espionage is the system that *mints that currency*. Without it, the locked "you infer, you can be deceived, you can deceive" loop is just a label. With it, the whole external layer becomes poker — which is exactly the fun 4X has always missed. This is also the Babylon-5 "covert agents" pillar the developer named.

> **Analogy (the developer's wheelhouse):** think of a rival empire the way you'd think of a contact on **sonar**. You don't *see* the boat — you hear a noise signature and infer course, speed, intent. A good sonar tech reads more from the same noise; better sensors resolve more; and you can run **silent** so *they* can't read *you*. Espionage is buying a clearer picture of a contact you can't see directly — and the risk that pinging actively gives away that you're listening.

---

## The core decision (the lever)

**Spend scarce intelligence effort to convert *hidden → known* about a rival — and accept a chance of being caught, which can cost you more than the secret was worth.** Every intel op is a bet: the value of knowing (or of the sabotage/theft you pull off) vs. the **detection risk** and what exposure triggers (a soured relation, a betrayal penalty, even a casus belli handed to *them*). You never have enough good agents or attention to know everything about everyone — so *what you choose to learn, and from whom,* is the strategy.

---

## The INFORMATION LEDGER — the heart of the system

The load-bearing new concept. For every rival faction you've met, you hold an **intel level on several FACETS** — your picture is sharp on some, fuzzy on others, blank on the rest:

| Facet | What it tells you | How it's fed |
|---|---|---|
| **Disposition** | Do they *actually* like you / plan war? (vs. the face they show) | a posted ambassador (passive) · agents (active) |
| **Military** | fleet counts, locations, tech, where the gaps are | sensors/detection (passive) · agents (active) |
| **Economy** | what they need, what they hoard, their money | trade contact (passive) · agents |
| **Internal politics** | which blocs are strong, their unrest, a wavering province's legitimacy | ambassador (passive) · agents (active) |
| **Secrets** | their treaties with others, war plans, a hidden weakness | agents only (active) |

Each facet has an **intel level**, roughly three bands (calibration later):
- **Inferred** (default) — you only see *behavior* (fleet moves, who they treat with, message tone) and a **fuzzy estimate** with error bars. This is the poker default.
- **Confirmed** — you've raised intel on that facet: the estimate sharpens or the truth is revealed.
- **Stale** — intel **decays**; a confirmed picture drifts back toward inferred as the world changes, so you must *refresh* (you can't learn a rival once and know them forever).

**This is fog-of-war for politics**, and it's the same idea as the combat fog already built — extended from "where are their ships" to "what are they thinking." It rides the **detection substrate** (sensors/EMCON), but the **graduated richness lives HERE, in the Ledger** — driven by agents + ambassadors + decay — **not** in a sensor field.

> **UPDATE 2026-07-07 — the "detection-quality fix" is no longer a prerequisite; `SignalQuality` is being CUT** (`docs/DETECTION-DESIGN.md` decision banner). The passive-sensor contribution to the Ledger is now **binary**: *physical detection on a rival → their Military facet is known at **Inferred** level.* The Inferred→Confirmed→Stale gradient is produced by the Ledger's own systems working together (agents raise it, decay lowers it), not by a graduated sensor-resolution number. This is cleaner — the graduated-ness is in one place, and the prerequisite becomes "cut a field," which is free.

---

## Agents — the people who do it (the M3 talent arm)

Espionage is the **intelligence arm of the people-as-a-resource pool** — same pattern as admirals/governors/diplomats:

- **Spymaster** — the **delegate** (the Every-Layer symmetry). You set a **stance** ("steal tech," "counter-intel focus," "destabilize [Rival]," "keep us informed"), and the spymaster auto-runs the espionage portfolio **at a competence cost** (a skilled one lands ops and reads threats; a poor one gets agents caught). The "I don't want to micro spies" path.
- **Agents / operatives** — individual people (skill + traits) you task on specific ops. Skill **raises success chance and lowers detection**; traits spawn flavor and stories. Scarce (M3 talent-gated) — you have a few good ones, so *where you point them* is a real choice.
- **Grave rung:** an agent can be **caught, killed, or TURNED** (a rival's counter-intel flips them into a double agent feeding *you* disinformation — the mirror of you turning *their* diplomat). Losing a master agent hurts like losing a veteran admiral.

---

## The COVERT-ACTION CATALOG — what an agent can do (build broad, data-driven)

Mirror of the diplomacy **exchange catalog**, but **unilateral and risky**. Build it broad; each entry names the effect, what facet/system it routes into, and the cost of being caught:

| Action | Effect | Routes into | Caught = |
|---|---|---|---|
| **Gather intel** | raise the intel level on a facet (disposition / military / secrets…) | Information Ledger | a relation hit (minor) |
| **Steal technology** | copy a rival's researched tech | Research / `FactionTechDB` | relation hit + they may re-secure |
| **Steal funds / resources** | divert money or materials to you | Ledger / Logistics | relation hit |
| **Sabotage** | damage an installation/station, slow production, wreck a shipyard | Industry / Stations (damage) | **betrayal penalty** + casus belli for THEM |
| **Sow unrest** | boost a discontented bloc, drop a province's legitimacy, incite rebellion | their INTERNAL politics (per-system legitimacy) | **betrayal penalty** + casus belli for THEM |
| **Turn / assassinate a person** | defect or kill a rival commander/minister/diplomat | People (grave rung) | **betrayal penalty** + possible war |
| **Plant disinformation** | feed them FALSE intel — make them misread your intent/strength | their Information Ledger (about you) | they realise they were played |
| **Counter-intelligence** (defensive) | protect your secrets, catch/мole-hunt their agents in you | your own ledger security | — (it's the shield) |

The **EXTERNAL-into-INTERNAL reach** is the spicy one: *sow unrest* lets your spies reach into a rival's **internal politics** — pour fuel on a bloc, push a wavering frontier system toward secession (the per-system legitimacy model from `GOVERNMENT-AND-POLITICS-DESIGN.md`). And the **mirror** is designed to be always on: NPC factions run the same playbook against *you*, so **counter-intelligence is a standing decision, not optional** — neglect it and your secrets leak and your provinces get destabilised. *(As built: the mirror code — `NPCDecisionProcessor.RunEspionageMirror` — exists and is wired, but ships behind `EnableEspionageMirror` (default false), so it does not run in a default game yet.)*

---

## The RISK side — what makes it a gamble, not a free menu

Every **active** op is a **detection roll**: agent skill (+ spymaster stance) vs. the target's counter-intelligence. The outcome scales by **severity**:
- **Clean success** — the effect lands / intel rises, no one the wiser.
- **Success but traced** — it works, but they know *someone* did it (and may suspect you).
- **Caught** — the op fails AND you pay: a **relation hit** for soft ops (gather), the full **betrayal penalty** for hard ops (sabotage/assassinate/sow-unrest), and — the sharp part — **a casus belli handed to THEM** (they now have a justification to come after you, gated by *their* militarism). Your **agent may be lost** (captured/killed/turned).

So a covert op is a genuine risk/reward bet: a juicy sabotage might cripple their shipyard — or hand them a righteous war. **That tension is the gameplay.**

---

## The hidden-information game, made concrete (how it all plays)

1. By default you read a rival's **behavior** + a **fuzzy disposition estimate** (Inferred). Are those fleets massing on your border a real threat or a bluff?
2. You **spend intelligence** — post an ambassador (passive), or task an agent (active) — to raise the intel level and **sharpen or confirm** the picture.
3. You can be **deceived**: their *disinformation* feeds you a false estimate; their **EMCON-dark** buildup is invisible to your passive intel (the detection tie-in). And you can deceive *them* the same ways.
4. You **act on incomplete information** — sign the pact, mass the counter-fleet, call the bluff — knowing you might be wrong. That's the poker, and intelligence is how you tilt the odds before you commit.

This is the loop that makes the **diplomacy-as-a-closing-fight** metaphor real: intelligence is the reconnaissance phase before the engagement.

---

## Espionage in gameplay — physical delivery + the NPC counterparty (2026-07-07)

**The load-bearing rule (developer): none of this earns its keep unless the NPC can DETECT these actions and REACT to them.** A player running spycraft against a blind, inert opponent is solitaire with extra steps. **The value is the STACK — your ops × the NPC's detection-and-reaction — never the ops alone.** Everything below serves that.

### Physical delivery — agents travel to their targets (resolving the abstract-vs-physical fork → PHYSICAL, with a delegate shortcut)

An agent is a person who must physically reach the target to operate. That turns espionage from a menu into a loop the player lives inside, and it's ~90% **CONNECT** (feasibility-traced 2026-07-07): it reuses the troop-transport pipeline, movement/jump, EMCON, and trade/diplomatic access.

**The delivery arc — each stage a decision on an existing system:**
```
pick COVER → run the ROUTE (chokepoints) → INSERTION roll → OPERATE (heat builds) → EXTRACT or embed
```
- **Cover** (three routes, each riding a different system): **stealth** (run EMCON-dark — the run-cold mechanic IS the stealth-insertion mechanic), **trade cover** (hide in legitimate traffic — needs a `LogisticsAccess` grant), **diplomatic cover** (insert via a posted ambassador — needs the embassy).
- **Route** — jump points are the **border chokepoints**; the detection asymmetry lets a cold spy ship *see the pickets before they see it* and time its run.
- **Insertion** — a detection roll (your stealth + agent skill vs their counter-intel / border security), gated by `HasOrbitalControl` (can't insert where an enemy fleet holds the orbit).
- **Operate** — embedded; **discovery heat builds over time** → the sleeper-vs-active tension (extract safe, or stay embedded and ready but accumulating risk).
- **Extract** — the reverse delivery, or leave them as a long-term sleeper/mole.

**The reuse map (feasibility 2026-07-07):**
- **Model on troop transport** — `GroundBayAtb` carrier + `GroundTransportDB` store + `TryLoadUnit`/`TryLandUnit` gates (`ShipIsAtBody`, `HasOrbitalControl`) are the exact pipeline to copy.
- **No cloak substrate exists — and that's correct.** "Stealth" = EMCON-dark + timing + the detection asymmetry (all built). A spy ship is detectable and **interceptable en route** — the tension we want, for free.
- **Agents stay full leader-entities (Option A).** A separate entity does NOT auto-follow a ship through a jump (jump transfers only the ship), so the delivery action **explicitly co-transports** the agent via `EntityManager.Transfer`. Small extra plumbing; preserves the leader pipeline (competence/experience/loss). *Do NOT* model the agent as fungible cargo (identity mismatch) or rely on passenger plumbing (doesn't exist).
- **The one genuinely new mechanic: "attach as infiltrator."** Troop landing drops a unit onto a visible region roster; a spy instead **attaches to the target colony hidden from that faction, accumulating discovery heat.** That attach-and-hide state is the real new code — and it's what makes an embedded agent a *place on the map*, not a menu entry.

### The NPC counterparty — detect + react (the co-requisite, not a follow-on)

Because the value is the stack, the NPC's ability to detect and react is built **with** the core, not after it. And it needs no bespoke spy-AI — it's the **mirror**: the NPC runs the same systems, pointed at you.

- **NPC detects** via: its own **sensors** (catch your spy ship in transit), its **counter-intel agents (target = itself)** rolling against your embedded agent's skill + heat, and the graduated op outcome (**clean / traced / caught**).
- **NPC reacts** via three escalating wires: (1) **catch/kill/turn your agent** → feeds *your* rung 6 (the loss has teeth); (2) **relationship + suspicion** → the reactive-diplomacy engine drops relations, repeated *traced* events build a **suspicion state** until they're sure → **casus belli** (gated by their militarism → possible war); (3) **posture shift + retaliation** → the NPC's Director-of-Intelligence delegate raises counter-intel focus and/or spies back.
- **Deniability makes reaction meaningful:** *traced* = "they know *someone* spied" (suspicion climbs); *caught* = "they know it's *you*" (full reaction). The NPC must hold and act on a suspicion state — the poker, from the other side.
- **It's bidirectional by construction** — the NPC gathers on you; your counter-intel catches *their* agent; you choose to kill/turn them. Both directions, same code.

**Why this is the right kind of expensive — it ties back to the whole design.** The NPC's spy-and-counter-spy behavior is the **delegate = NPC AI** principle: the NPC's Director delegate + the reactive-diplomacy engine, both built anyway. **So espionage value is downstream of the NPC-delegate loop actually firing and deciding.** *(As built 2026-07: `NPCDecisionProcessor.Tick` is now a full decision body — reactive-diplomacy drift, strategic-objective selection, the espionage mirror — NOT the empty stub this line once described; `ReactiveDiplomacy` is live. What's still gated off is the runtime firing: the mirror + ledger sit behind their default-false flags, so the loop is built but idle in a default game.)* Espionage isn't a side-quest from the AI work — it's the AI work's **showcase**.

**Visibility Gate — both ways.** The reaction must be *legible* or the tension evaporates: the player must SEE they were traced/caught, relations drop, an agent hunted — and equally see "we caught an enemy spy" when defending. Feedback is part of the build, not polish.

### The reshaped MVP

The counterparty is inside the MVP, not after it:
> player `gather` + **NPC counter-intel detection** (rolls against the op) + **minimal NPC reaction** (relation hit + suspicion + surfaced) + **the mirror** (NPC gathers on you; you detect + react). Physical delivery for the hands-on player; the Spymaster delegate auto-runs it for the CEO-altitude player.

Still the **second** leader vertical slice (after the Governor), and still gated on the leader pipeline + cutting `SignalQuality`. But its worth is now correctly tied to the NPC being a live opponent — which is the point.

## Covert weapons — the bioweapon flagship (2026-07-07)

A **covert weapon** is a designed component like any other (cradle-to-grave, built in the weapon designer), but its "combat" happens in the **population / espionage / influence** systems, not the fleet auto-resolve. This class is espionage's WMD tier. The flagship is the **bioweapon**; the class also holds cyber/logic bombs (wreck a rival's networks), assassination toxins/devices (kill a specific leader → rung 6), memetic/propaganda payloads (an influence weapon → spread unrest), and sabotage charges.

**Why it belongs here and not in the weapon triangle:** a covert weapon is delivered and defended-against through espionage/influence, and its effect lands on *population and legitimacy*, not hulls. It is the intersection weapon — deliberately requiring a *multitude of systems* to pull off.

### The bioweapon cradle-to-grave (the connect showcase)

| Stage | What happens | System it lights up |
|-------|-------------|---------------------|
| **1. Target research** | Understand a *specific species'* biology. Two acquisition routes: **abduction** (fast — an espionage op to grab specimens, risky) or **study-at-a-distance** (slow, safe, passive observation) | Xenobiology → **fills the empty Biology & Genetics tech category**; ties to the native-biosphere field-site (`docs/EXPLORATION-CONTENT-DESIGN.md`) |
| **2. Design** | The weapon designer, full intricate detail: lethality, contagiousness, incubation, **species-specificity** (a targeted strain vs broad-spectrum), persistence, detectability | The universal-assembly weapon designer |
| **3. Build** | From bio-materials / a bio-lab | Industry / materials |
| **4. Deliver** | **(a)** a vessel you fly (military / scout / **science** / **trade** — cover matters; a trade/science ship is plausible, a warship screams intent); **(b)** the espionage delivery loop (an agent plants it — the infiltrator mechanic above); **(c)** the **proxy route** — fund or incite a terrorist group / rebellion to deploy it (deniable) | Movement + the spy-delivery loop + the influence/rebellion system |
| **5. Effect** | Casualties, contamination, morale + legitimacy collapse; can **tip the world into rebellion**; a contagious strain can **spread between worlds** | `DamageProcessor.OnColonyDamage` (built) + rebellion + influence |
| **6. Grave / blowback** | Detection = a **WMD-scale provocation** (relations crater with *everyone*, casus belli, coalitions form); a contagious weapon can **spread back** to your worlds or neutrals (MAD); the delivery asset is lost | Diplomacy reputation + the mirror |

### Earns its weight (not a "click to genocide" button)

Every stage is a decision that stacks: acquire *fast-and-risky vs slow-and-safe*; design *contagious vs targeted / lethal vs incapacitating*; deliver *by control vs by deniability*; and the **go/no-go** itself. It's expensive and scarce (a whole research→design→build→deliver chain), so it's a **strategic weapon of last resort**, not spam. The **blowback + war-crime reputation** make "should I even build this" the real decision — a villain/desperation path that can turn the galaxy against you. That reputation weight is the balancing force.

### The proxy / terrorist route (generalizes beyond bioweapons)

*Deniable warfare:* rather than deliver it yourself, **fund a rival's rebels, incite a coup, create a terror cell, or arm a secessionist world**, and let *them* act. Ties espionage (funding = a covert action) → the influence pillar (inciting rebellion = the belief-war attack on legitimacy, `docs/INFLUENCE-PILLAR-DESIGN.md`) → the built rebellion system. A proxy war with no fingerprints — and a delivery vector that keeps *your* name off the bioweapon. This is a covert-action-catalog entry in its own right (arm-the-rebels / back-the-coup), not just a bioweapon delivery.

## Cradle to grave (espionage)

> research **spy tech** → design/build the **gear** (a covert-ops component / an intelligence HQ — the seat that gives intelligence capacity) → recruit/train an **agent** (people, M3) → seat a **Spymaster** delegate or task the agent directly → spend scarce **intelligence capacity** on a **covert op** from the catalog → **roll detection** vs their counter-intel → success raises **intel** or lands the **effect**; caught = the **betrayal penalty** + a **casus belli for them** + the **agent is lost** (captured/killed/turned — the grave rung) → re-research / re-recruit / re-run. Destroy a rival's **intelligence HQ** (sabotage or war) and you blind their spy network — the grave rung wired to the damage system.

Every rung is reachable and losable — it is NOT a parachuted-in "spy points" abstraction. Spy capability is a **component** you research/build/install/lose (the law of `CONVENTIONS.md` §6), exactly like a sensor.

---

## Connections (Prime Directive)

- **Detection / sensors / EMCON** (built) — the **inference substrate**: physical detection on a rival feeds their Military facet at **Inferred** (binary). **No longer a prerequisite** — `SignalQuality` is CUT (2026-07-07); the gradient lives in the Ledger, not the sensor. EMCON-dark still hides your buildup from their passive intel (the bluff), via *strength*.
- **Diplomacy** (designed) — intel is **negotiation leverage** (dirt at the table); caught espionage **craters the relation TRACK** and can hand the other side a casus belli. The ambassador is the *passive* intel feed.
- **Internal politics** (designed) — *sow unrest* reaches into a rival's **blocs / per-system legitimacy**; their agents do it to **you** → counter-intel defends your provinces. The reactive demand-engine can surface "we are being destabilised by [Rival]."
- **People** (M3 talent) — Spymaster + agents are the intelligence arm; *turn/assassinate* hits the **people grave rung** (theirs and yours).
- **Tech / research** (built) — *steal-tech*; and spy capability is **research-gated** (better gear, better counter-intel). Cradle-to-grave via components.
- **Military / combat** (built) — *sabotage* damages production/installations; **military intel feeds the combat first-strike** (knowing where they are = the seer's edge already built in the detection slice).
- **Stations / industry** (built) — sabotage targets; an **intelligence HQ** is a station/colony component (the capacity seat); a fragile station spy-node is the easy-to-lose frontier (ties to the station fragility).

---

## Locked vs. open

**Locked design forks (developer, 2026-06-30) — the options pass:**
- **D — information model = PER-FACET ledger (D2).** Separate intel on disposition / military / economy / internal-politics / secrets, each Inferred→Confirmed→Stale — so the player SPECIALISES intelligence (what you learn is a real choice). NOT a single fog-% (too shallow) and NOT continuous error-bars (overload). Sub-lock: **passive (ambassador + sensors) = the cheap fuzzy baseline; active (agents) = the sharp/secret stuff** — two distinct intel taps.
- **E — catalog breadth = E2 NOW (intel + economic: gather/steal-tech/steal-funds/sabotage) as the FOUNDATION for E3 (political + personal: sow-unrest/turn/assassinate).** Build the cold-war core first; **E3 sow-unrest is the high-value wire into the REBELLION hub (B)** but lands later and **needs further discussion** (especially balance, gated behind counter-intel so it isn't oppressive).
- **F — risk model = F2 + F3 TOGETHER.** Graduated outcomes (clean / success-but-traced / caught) feeding a per-rival **suspicion meter** — hurt them quietly, but each "traced" builds suspicion until they're sure. The deniability game IS the espionage fun; "caught" scales into the betrayal penalty / casus-belli-for-them.
- **G — the mirror = G3 (always-on, per archetype), tuned LOW and scaling with hostility.** NPCs spy on you per their nature; counter-intel is a STANDING (cheap-baseline, ramp-when-threatened) decision. Non-negotiable for the system to feel fair/alive — a one-way spy toy is the shallow version. *(Developer: "go with your read, may tweak later.")*
- **H — deception = aim for H2 (simple disinformation — plant a false estimate in THEIR ledger about you), then move to H3 (framing/false-flag) from there.** H2 completes the hidden-information triangle: HIDE (EMCON) · LEARN (intel) · LIE (disinfo). Built last in the spine; H3 is the deeper multi-party intrigue to grow into.

**Locked (developer, 2026-06-30):**
- **Espionage is the hidden-information ENGINE** — load-bearing for the locked full version, not optional flavor.
- **The INFORMATION LEDGER** — per-rival, per-facet intel level (Inferred → Confirmed → decays to Stale). Fog-of-war for politics, on the detection substrate.
- **Covert-action catalog built BROAD + data-driven** (mirror of the exchange catalog) — gather / steal-tech / steal-funds / sabotage / sow-unrest / turn-or-assassinate / disinformation / counter-intel.
- **Every active op is a RISK/REWARD detection bet** — caught scales from relation-hit → betrayal penalty → **casus belli for THEM**; agents can be **caught/killed/turned**.
- **Agents = the M3 people intelligence arm** — Spymaster (delegate) + taskable operatives; talent-gated; grave-rung.
- **The MIRROR is always on** — NPCs spy on you; **counter-intelligence is a standing decision**, not opt-in.
- **Spy capability is a COMPONENT** (research → build → install → lose) — an intelligence HQ is the capacity seat; not a bespoke "spy points" flag.

**Locked (developer, 2026-07-07) — seating espionage in the leader pipeline (`docs/AI-SELF-PLAY-DESIGN.md`):**
- **Agents are full leader-entities** — they ride the whole born→skilled→seated→acts→improves→lost pipeline (a new `CommanderType`, academy-trained, scarce). Losing one *hurts* — that's the point.
- **Espionage org mirrors the Foreign Minister:** an empire-wide **Director of Intelligence** (cabinet, sets doctrine, owns empire-wide counter-intel) → a **per-faction Spymaster / station chief** (offense against that rival) → **agents** under them.
- **Counter-intel = the same agent, target = your own faction** (not a separate mechanic). Assign an agent against a rival = offense; assign it to your own house = it hunts enemy agents/moles. The "focus" cost is the opportunity cost — a guard isn't out stealing. Empire-wide defense sits under the Director.
- **`SignalQuality` is CUT** — the passive-sensor feed is binary (detection → Inferred); the gradient lives in this Ledger. Retires the old detection-quality prerequisite.
- **NOT a parallel system** (`CONVENTIONS.md` §6) — espionage is espionage-specific *people* (agents/spymasters, on the leader pipeline), *components* (Intelligence HQ, on the component/designer architecture), and *data* (this Ledger + the covert catalog). Shared substrate (sensors, academy, money ledger) is *ridden*, not duplicated.

**Resolved (added 2026-07-07):**
- **Agent delivery = PHYSICAL, with a delegate shortcut.** Agents ride a ship to the target and are inserted (the delivery arc above); the Spymaster delegate auto-runs it for players who don't want the micro. Reuses troop-transport + movement + EMCON; the only new mechanic is "attach as infiltrator." No new unit class (a courier/recon ship is a normal ship designed with the right components).
- **The NPC counterparty (detect + react) is a MVP co-requisite, not a follow-on** — the value is the stack; it rides the mirror + reactive-diplomacy + the NPC delegate loop.

**Open (decide when we build):**
- Information-ledger granularity (per-facet bands vs. a finer score) + the **decay rate** (how fast confirmed intel goes stale).
- Detection-roll math (agent skill + spymaster stance vs. counter-intel) — calibration/feel.
- How much *sow unrest* can move a province's legitimacy (so a spy can *nudge* a rebellion but not single-handedly topple an empire).
- Disinformation mechanics (how you plant false intel; how the target can detect they were played).
- Whether intel is **shareable** as a diplomacy exchange (sell dirt — already a row in the exchange catalog; confirm it reads the ledger).

**Build order (after the keystone + diplomacy substrate + the LEADER PIPELINE):**
**cut `SignalQuality`** (free; replaces the old "detection-quality fix" prereq) → the **Information Ledger** (per-rival intel state + the Inferred/Confirmed/Stale bands) → **passive intel** (ambassador + physical detection feed it, binary → Inferred) → the **agent/Spymaster** people layer (rides the leader pipeline) → the **covert-action catalog** (gather FIRST — the pure hidden-info lever — then steal/sabotage, then sow-unrest) → **detection + counter-intel + caught-consequences** (the risk side) → the **NPC mirror** (they spy on you; reactive "we are being destabilised") → **disinformation** (last; the bluff weaponised).

> **The Spymaster MVP (2026-07-07):** Information Ledger + passive intel + a per-faction Spymaster seat + full-entity agents running the `gather` op. Delivers the poker-of-intent core without the balance-sensitive hard ops. It is the **second** leader vertical slice (after the Governor) — it proves the pipeline generalizes AND stands up the Ledger. Prereqs: the leader pipeline (agents/spymasters are leaders) + cutting `SignalQuality`.
