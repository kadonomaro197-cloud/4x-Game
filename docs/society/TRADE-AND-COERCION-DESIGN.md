# Trade, Power Projection & the Negotiation Scene — the Coercion Theater (design)

**Status:** concepts + grounded build path (design conversation 2026-07-14; **re-audited against the main merge 2026-07-16**, idea-dump branch `claude/trade-strategic-theater-3nl61c`). No new engine code yet. Every "exists" claim is a file:line from a code survey (three-agent baseline + a five-probe re-verify after the `devtest-faction-design` merge landed). **See the 📌 UPDATE callout in §0b for what the merge moved** — net: a standing-need pattern (food/power) and a live NPC-acts-back mirror got built, so the pillar is now "extend a proven pattern," but the coercion layer proper (cross-faction money, tariff/embargo teeth, the presence feed loop, the negotiation scene) is re-confirmed still missing.

**In one line:** make **trade a theater of war in its own right** — fought with tariffs, embargoes, dependency and blockade instead of missiles — and give it the two things war has that trade lacks today: a **pressure lever short of shooting** (park a fleet, send a message) and a **face to talk to** (a Fallout-style negotiation scene where all your leverage is spent).

> **Why this doc exists.** The developer's observation: *"Combat, exploration, even espionage have layers to them, but Trade has little. In the modern day, trade wars are a thing — tariffs, embargoes — options to maintain peace. I want trade to be as much an option as full-scale war."* Plus: the naval reality that **parking a carrier off a coast enforces peace and showing an SSBN sends a message** — coercion without a shot — *"and along with that comes in-depth diplomacy,"* the old-Fallout kind where a conversation changes how a character feels about you and branches to many endings.
>
> This doc answers all three as **one connected system**, because they are the same thing seen from three angles: **coercion below the threshold of war.**

---

## 0. The big frame — trade is the missing FIFTH conquest pillar

The design suite already treats war as having *theaters* — the same skeleton with a different weapon. `docs/society/INFLUENCE-PILLAR-DESIGN.md` spells out four:

| The skeleton | **Military** | **Espionage** | **Diplomacy** | **Influence** | **⟶ Trade / Economic (NEW)** |
|---|---|---|---|---|---|
| **Medium** | ships / troops | agents | treaties | belief / culture | **goods, money, dependency** |
| **Delegate** | Admiral | Spymaster | Foreign Minister | Influence Minister | **Trade Minister** |
| **The hand** | fleet / formation | operative | Envoy / Ambassador | missionary | **merchant houses / freight lines** |
| **Target** | hulls / territory | secrets | their stance | their people | **their economy / their supply** |
| **Projection** | weapon range | agent insertion | treaty proposal | missionary delivery | **a dependency you created** |
| **Counter / defense** | armor / PD | counter-intel | diplomatic savvy | state religion | **stockpiles, alt-suppliers, autarky** |
| **The "kill"** | destroy / occupy | steal / sabotage | ally / vassalize | convert → secede | **strangle → capitulate / vassal-by-debt** |

Trade is the **conspicuous empty column.** You're not inventing a foreign concept — you're finishing a pattern the game is already built around. Once it's a peer column, "win the game by making everyone need you and never firing a shot" becomes a real victory path, exactly like the influence pillar's "convert their worlds without a weapon."

**The unifying currency.** Every pillar does its damage in the *same tanks* — money, morale, legitimacy, reputation — read through the *same fog*. That's what makes them theaters of one war instead of five minigames. A trade embargo doesn't blow up a ship; it starves a colony, which bleeds **morale**, which erodes **legitimacy**, which can tip into **rebellion** — the identical tank a lost battle drains. *(See §1.3; the morale/legitimacy/rebellion stack is BUILT: `Colonies/ColonyMoraleDB.cs`, `Colonies/LegitimacyProcessor`, `Colonies/RebellionDB.cs`.)*

---

## 0b. Reality check — how deep is the trade system TODAY (and why rung zero comes first)

*Added 2026-07-14 after a code read of the economy, because you can't design the bite before you know what's there to bite. Grounded in `docs/economy/RESOURCES-AND-MATERIALS-DESIGN.md` + the three-agent survey.*

**There are two economies, and only one is deep.**

- **The "make things" economy — deep and working.** Mine → refine → build runs every game-day (`Industry/IndustryProcessor.cs`): 15 differentiated minerals, 9 refined-material recipes, finished goods that pull on the chain, infrastructure efficiency throttling all three stages, even material-specific armor absorption physics. This half is well-built.
- **The "trade things" economy — shallow, and mostly intra-faction.** This is the half we want to weaponize, and today:
  - **Prices are essentially fixed.** An item's worth is a constant `CreditValue` in JSON. The freight market optimizes *delivery routes* against player-set *desired stock levels* (`LogisticsProcessor.cs:128-152`) — it does **not** discover a price that floats with scarcity. There is no "their fuel price spikes when I embargo it" — that spike has nowhere to come from yet.
  - **Commerce is intra-faction.** The freight market hauls between *your own* colonies. Across faction lines there's only an access on/off flag (`LogisticsAccess`) + a flat, gated-off "1000/agreement" trickle (`TradeIncome.cs:19`, `EnablePayout=false`). **No goods trade for money across factions** — the only cross-faction money move in the whole game is espionage theft (`StealFunds`).
  - **A standing need now exists — but it can't be *cut* yet.** *(Updated after the main merge 2026-07-16 — see the callout below.)* Food (and power) are now real standing colony needs: `SustenanceProcessor` computes food demand = pop × per-capita coefficient vs. locally-produced supply (buildable `FoodProductionAtbDB` farms), and a shortfall both drops morale **and kills population monthly** (a starvation term), with a grave rung (bomb the farm → starvation returns). BUT two things keep it from being a trade weapon: it's **off by default** (the per-capita coefficient defaults to 0; only two scenario factions — UMF, UEF-DevTest — switch it on), and it's **local-only** — food is produced by the colony's *own* farms; there is no food *cargo*, so it can't be hauled, imported, or **embargoed**. **You cannot strangle a supply that can't be imported.**

> ### 📌 UPDATE — main merge (devtest playtest branch), 2026-07-16
> A large merge (the `devtest-faction-design` playtest + AI/combat/designer work) landed and **moved three of this doc's code-reality claims.** Net: much of the scaffolding this doc said was *needed* got built — so the trade pillar is now more "extend a proven pattern" than "build from scratch." Verified by a five-probe code re-audit (file:line):
> 1. **Standing need — PARTLY BUILT (as food/power sustenance).** The demand → shortage → morale + starvation → buildable-supply → grave-rung loop *exists* (`Colonies/SustenanceProcessor.cs`, `FoodProductionAtbDB.cs`, `PopulationProcessor.cs`). It's the reusable *pattern* rung zero called for — but it's **local-only and off-by-default**, so it is not yet strangleable. The residual work is the **cross-faction / importable** half, not the need itself.
> 2. **The AI is ON in real games.** The four NPC gates (`EnableOrderEmission`/`EnableDiplomaticProposals`/`EnableEspionageMirror`/`EnableIntelLedger`) default false *only* for engine-test byte-identity; **every menu-started game flips all four on** (`NewGameMenu.cs:547-550`). And the **military mirror already fires by default** — `RunWarPolicy` opportunistically declares a war of choice (appetite = ½·Aggression + ½·Ambition, out-muscle by 1.25×, relation ≤ −25) and `ConquerResolver` runs the full **load → sail → land** invasion. So "an opponent who never retaliates = solitaire" is **stale**: the NPC already retaliates militarily. The trade/presence/negotiate-back mirror is still unbuilt — but it's now a **build** co-requisite (add the behaviors to the same `Tick`; they go live under the already-flipped gates), not a flip-a-flag one, and the military pillar proves the pattern works end-to-end.
> 3. **The AI uses EMCON.** `NPCDecisionProcessor.RunEmconPolicy` now sets each fleet's loud/dark posture by personality (bold/aggressive → loud, guileful/cautious → dark) via the shared `DecisionScorer`. But it's a standing **combat-stealth stance**, not targeted intimidation — the **presence → pressure feed loop is confirmed still missing** (`RelationDelta(FleetNearBorder)` is still `_ => 0`; `Overture()` still has zero callers; the territory model still doesn't exist). So the core of PART B is intact; the AI's posture-picking is useful *scaffolding* for it.

**So rung zero — before ANY trade-war lever can bite at all — is three foundations. The merge advanced #1; #2 and #3 are still open:**
1. **A standing need — the *pattern* is now BUILT (food/power sustenance).** The remaining work is making a need **importable so it can be cut** (see the trade-weapon test). The cheapest path is **ship fuel** (below), not food.
2. **Real cross-faction commerce** (goods one way, money the other) — *still unbuilt*. The thing tariffs tax and embargoes cut.
3. **A scarcity-responsive price** (the gauge that moves when supply tightens) — *still unbuilt*.

These three ARE "establishing the trade system." The build order (below) is revised to put them first.

> **The trade-weapon test (the load-bearing filter):** a standing need only becomes a *trade weapon* when the need can be met by **import** — so an embargo can **cut** it. Food fails this today (local-only, no cargo). **Ship fuel passes it almost completely:** fuel is *already* consumed by movement (Tsiolkovsky burn → cargo drawdown, `Movement/NewtonSimpleProcessor.cs:98-99`) *and* is *already* a haulable cargo (`fuel-cargo-hold`; rp-1/methalox/hydrolox). Fuel's *only* missing piece is a **cross-faction fuel market** — so the recommended rung-0.5 is not "build fuel consumption" (it exists) but "let fuel be bought/sold across faction lines, then cut." Fuel is the better first weaponizable need than food.

### The bite model — LOCKED 2026-07-14: exponential PAIN on a linear ratio the AI reads

The developer's two constraints — *bite should feel exponential* AND *the AI must use it intelligently* — pull against each other, and the resolution is the load-bearing design idea of this whole pillar:

> **Don't make the *damage* an exponential knob. Make it an exponential *pain curve* riding on a simple, linear number both sides read: the coverage ratio.**

The **coverage ratio** = *(what a colony is getting) ÷ (what it needs)* of a good it depends on. Bounded 0…1. Fully supplied = 1.0; half-supplied = 0.5; cut off ≈ 0. It does **four jobs at once**, which is why it's the keystone quantity:

- **It's the dependency map** (§A.1) — the battlefield, per pair, per good.
- **It's the embargo's target** — an embargo/blockade drives the victim's ratio down.
- **It's the axis the exponential PAIN rides on** — pain (price spike → production loss → morale/unrest) is ~nothing at 0.9 coverage, stings at 0.6, and falls off a cliff below ~0.3. The steep curve is where "exponential" lives — in the *consequence*, not a raw damage number.
- **It's the AI's decision input** — and this is the payoff. A coverage ratio is *legible and monotonic*, so the NPC brain can reason about it exactly as `GrowEconomyResolver` already reasons about desired-vs-actual stockpiles today: "fuel coverage fell to 0.4 and is still dropping → seek an alt-supplier / build domestic capacity / sue for peace / retaliate." An AI **cannot** reason about "you took 4,000 abstract economic damage"; it **can** reason about "I'm 40% covered on a thing I need." The player suffers the curve; the AI plays the flat, readable variable underneath it. That is what makes the bite both brutal and intelligently-counterable.

> **The merge already built the shortage half of this.** `ColonyMoraleDB`'s `MoraleInputs.FoodShortage`/`PowerShortage` (0 = none … 1 = total) is *exactly* `1 − coverage_ratio`, and it *already* feeds a capped morale penalty + a starvation death term (`ColonyMoraleDB.cs:159-162`, `PopulationProcessor.cs:52-57`). So build-slice 4 ("add a supply-shortage term to morale") is **not** a new build — the term exists. The net-new work is only making that fraction **respond to a cross-faction embargo** (route supply across faction lines) and bending its response through the exponential pain curve. The pain curve rides an existing gauge.

---

## PART A — TRADE AS ECONOMIC WARFARE

### A.1 The battlefield is the dependency map (this is "range and position")

In a shooting fight, *position* decides who can hit whom. In a trade fight, **dependency** is position. If a rival imports 80% of their fissile fuel from you, that dependency is a gun already aimed at their head — you don't have to move a fleet, you just have to *stop selling*. Conversely, a rival who supplies *you* holds a gun on *you*.

So the first thing the trade theater needs is a legible **dependency reading**, per pair, per good:

> "Faction B gets 71% of its refined *duranium* from me, has ~40 days of stockpile, and no alternative supplier in reach." — that sentence is the trade equivalent of "the enemy flagship is inside my beam range and outside its own."

The raw material for this already flows: the logistics market moves goods with a supply/demand price weight (`LogisticsProcessor.cs:128-152`, reads `material.CreditValue`/`mineral.CreditValue`). What's missing is that **cargo never crosses faction lines with money** (the market is intra-faction only; `LogisticsProcessor` never touches a `Ledger`) and there is **no dependency ledger** that accumulates "who bought how much of what from whom." Build that accumulator and the battlefield draws itself.

### A.2 The weapons are an escalation ladder (this is the "weapon triangle / calibers")

Give the player a graduated set of economic weapons, mild to nuclear — the trade version of choosing a caliber. Each has a **cost to you**, a **counter**, and a **blowback**, so none is free:

| Rung | Weapon | What it does | Cost to YOU | Their counter |
|---|---|---|---|---|
| 1 | **Tariff** (a % per pair, per good) | tax their goods at your border — revenue + a warning shot | raises your own consumers' prices; invites a retaliatory tariff | reroute demand, eat it, or tariff you back (tit-for-tat) |
| 2 | **Quota** | cap the *volume* they can sell you | forgo cheap imports | find a third buyer |
| 3 | **Export control** | refuse to sell them *one strategic good they can't make* — the chokehold | lose that sale | scramble for an alt-supplier or a domestic substitute (slow, costly) |
| 4 | **Embargo** | cut a whole category both ways | your own shortage of what they sold you | stockpile burn, then unrest |
| 5 | **Sanctions / full blockade** | cut *everything* — the "declare war" of trade (and a blockade is trade-war *backed by a fleet* → §B) | your merchants scream; a bloc may form against you | autarky, or a patron who breaks your blockade |
| — | **Dumping / subsidy** *(offensive)* | flood their market below cost to bankrupt *their* domestic industry | you eat the loss to buy the kill | protective tariffs (rung 1, pointed back) |
| — | **Preferential bloc** *(the "defensive pact" of trade)* | cheap terms + priority supply to an ally; freeze out a rival | you're now entangled — their enemy is your trade-enemy | a rival counter-bloc |

The **tit-for-tat ladder is the gameplay**: you tariff, they retaliate, you can climb to embargo or de-escalate to a deal. It's a closing fight run in percentages instead of kilometers — the same "apply pressure, read the response, commit or back off" loop the combat-range design (`docs/combat/FLEET-COMBAT-CLOSING-DESIGN.md`) already uses, just with money.

### A.3 The damage lands in the tanks you already built (this is "connect")

This is the load-bearing connection — the thing that makes it a *theater* and not a spreadsheet:

- Embargo a good a colony **depends on** → its supply gap widens → **production slows / a shortage term hits** → that feeds **`ColonyMoraleDB`** (inputs now: conditions / crowding / employment / comfort / tax / power / **food-shortage** / food-quality — `ColonyMoraleDB.cs:159-162`; a **supply-shortage → morale + starvation term already EXISTS** for food/power, so a trade-good shortage *reuses that exact pattern* rather than adding one from scratch) → morale drop feeds **`LegitimacyProcessor`** → past threshold, **`RebellionDB`** wakes up. *Trade damage kills a colony the slow way a siege does — through a wire the merge already built.*
- Cut a rival's **income** (deny the trade agreement's revenue, or out-compete their exports) → their `Ledger` thins → they can't fund fleets/research/upkeep. Economic attrition = fewer enemy ships next year, without firing this year.
- **Dumping** attacks their *industry* directly: flood the market, their domestic producers can't sell, the sector withers. That routes into the industry/employment side of the colony model.

The point: the trade theater's "hit points" are **morale, legitimacy, and treasury** — three tanks that already exist and already cause visible consequences. You wire the economic weapons to *drain those tanks*, and the game does the rest.

### A.4 It cuts both ways — that IS the peace-keeping engine (mutual assured economic destruction)

The developer's core insight — *trade wars are how you "maintain peace"* — falls out for free the moment costs are symmetric:

- Every embargo **costs the embargoer**: lost revenue + your own shortage of whatever they used to sell you. Cutting off a partner you also depend on is **mutually assured economic destruction** — so a dense web of trade makes a shooting war *expensive on purpose*. Two economies grown into each other can't cheaply fight; the entanglement is the deterrent.
- That gives the player a genuine strategic choice the game doesn't offer today: **build dependency to bind a rival to peace** (they can't afford to fight the country that feeds them), or **cultivate autarky to keep your hands free** (you sacrifice cheap trade for the freedom to swing). That trade-off — *guns vs. entanglement* — is the pillar's headline decision.
- And it deters *asymmetrically*: strangling a partner who depends on you far more than you on them is a cheap, devastating opening — the economic equivalent of crossing the enemy's T. The **dependency reading (§A.1) is the targeting solution.**

### A.5 The exchange catalog is already written — it's just inert

The good news from the survey: the *data* for all of this substantially exists and is **DEAD only because nothing consumes it.**

- `Factions/ExchangeCatalog.cs` — **27 exchange rows across 7 families**, each tagged with which physical system it routes into (`ExchangeRoute` enum). It already contains `trade-agreement` ("…+ tariff terms"), `sanctions` ("cut trade"), standing supply lines, resource-access rights, tribute, subsidies. Self-documented at `:55`: *"Nothing consumes it yet."*
- `Factions/RelationshipState.cs:64` — a boolean `TradeAgreement` flag per pair (BUILT). **Needs a scalar** — a tariff-rate and an embargo latch per good/category — to carry a *trade war* instead of just on/off.
- `Factions/TradeIncome.cs:19` — a flat `1000/agreement` self-booked to the owner, no counterparty, no goods, no price. `TradeIncomeProcessor` exists but is **GATED OFF** (`EnablePayout = false`, flipped true only in tests). Needs to derive income from *actual goods flow × a per-pair rate* so a tariff/embargo can bite it.
- `Factions/Ledger.cs` — single-faction; **no `Transfer(from, to, amount)`**. The *only* cross-faction money move that exists today is espionage theft (`StealFunds`). Trade needs a real transfer.

So the trade theater is roughly: **turn the inert exchange catalog into a live executor**, add cross-faction money + a dependency accumulator, and put a tariff/embargo scalar on the relationship. The design (the "COMMITMENT model" — a deal emits real orders each cycle, promised-vs-delivered tracked) is already locked in `docs/society/DIPLOMACY-DESIGN.md:374-427`; this pillar is what finally *consumes* it.

---

## PART B — POWER PROJECTION ("presence = pressure")

*The carrier off the coast. The SSBN in an obscure corner. Coercion by being seen.*

> **Terminology guard — two kinds of "force projection" (don't confuse them).** `docs/combat/CARRIER-DESIGN.md` (merged 2026-07-14) is titled "force projection from standoff range" but is **tactical**: throwing evasive strike craft from beyond gun range *inside a battle*. THIS section is **strategic**: a fleet's *presence* coercing a rival *without firing a shot*. They're complementary, not competing — and they meet at one point: a **carrier group is the archetypal loud-signature presence asset** for the loudness lever below (a flattop parked loud off their coast is a potent threat precisely because they know it can strike from beyond their reach). Cross-reference, no contradiction.

### B.1 The mechanic

A fleet's **coercive pressure** on a rival ≈ **(how strong they think it is) × (how close it is to something they value) × (how loudly it's broadcasting).** All three terms already have gauges:

- **How strong they think it is — through their fog.** `Factions/ThreatAssessment.cs:27` `DetectedStrengthOf(observer, rival)` already sums the signal strength of the *live, non-memory* contacts the observer holds for that rival. It's fog-correct: a fleet running dark under-reads. This is *exactly* "how scary is that fleet parked off my coast, given what I can actually see."
- **How loudly it's broadcasting — the SSBN twist.** `Sensors/Emcon/FleetEmcon.cs:55` `SetPosture(Full/Cruise/Silent)` is a first-class per-fleet order that scales the ship's detectability (`ActivityMultiplier` 1.0/0.5/0.15). **To intimidate, you go LOUD on purpose** — the whole point of showing the boomer is that they *know* it's there. To sneak or bluff, you go dark. **Choosing to be seen is the message** — and this lever is already built and tested. *(Merge update 2026-07-16: the AI now flips this lever too — `NPCDecisionProcessor.RunEmconPolicy` picks each fleet's loud/dark posture by personality via the shared `DecisionScorer`, bold/aggressive → loud, guileful/cautious → dark. That's a standing **combat-stealth stance**, NOT yet targeted intimidation of a rival near its space — but it's useful scaffolding: the AI already knows how to be loud-on-purpose; §B.3's missing piece is making it choose loud **at** a specific rival's border and having the rival react.)*
- **How close — parking.** `Movement/MoveToSystemBodyOrder.cs:55` warps a fleet into a body's low orbit. Parking a fleet in orbit of their colony is one existing order away.

Pressure then feeds the **reactive diplomacy** engine (§C) and becomes **leverage in the negotiation scene** — a fleet on their border is a `+intimidate` option you can only pick because the fleet is *there and loud*.

### B.2 It's a bluff / credibility gamble (the reputation grave-rung)

Presence without follow-through is a bluff, and bluffs get called:

- Park-and-never-shoot, repeatedly, and a rival *learns you won't* — your threats stop moving the needle (their model of you updates; this rides the same reputation/memory track as everything else).
- Occasionally **mean it** and you earn a fearsome standing where the mere arrival of your fleet extracts concessions — the real-world deterrence payoff.
- A **called bluff is the grave rung**: credibility, once spent, is dear to rebuild. That makes "do I actually back this up?" a genuine, costly decision, not a free tooltip.
- The **counter** is symmetric: they can mass their own fleet (counter-deterrence), call the bluff, invoke a **defensive pact** to drag in an ally, or — if they're a militarist regime — *rally domestically against the intimidation* so your pressure **backfires** into their legitimacy *gain*. Gunboat diplomacy against the wrong government makes them stronger.

### B.3 What's missing (the honest gap — three items)

The survey is clear that the *pieces* exist but the *wire* doesn't:

1. **A territory/border model — MISSING (re-confirmed on the merged tree).** "Near their space" has no denominator today. The code comment still says so — now at `NPCDecisionProcessor.cs:347` (the merge shifted it from `:271`): *"border-proximity reactions need a territory model … which does not exist yet."* (The dead `FactionSystemInfoDB.cs` is still a corpse — don't build on it.) **Cheapest v1: derive it** — a faction "claims" the system of any colony it holds; "a hostile fleet near their border" = a hostile contact detected in a system that contains their colony. That composes from `FactionInfoDB.Colonies` + the star-system manager + the contact table, **no new persistent state.**
2. **The feed loop — MISSING (re-confirmed).** The AI now sets EMCON posture (§B.1), but **nothing feeds fleet *presence* into diplomacy.** `RunEmconPolicy` reads only `PersonalityDB` — never the contact table, a territory model, a rival's proximity, or `ThreatAssessment`. `Factions/ReactiveDiplomacy.cs:64` has the table — `FleetNearBorder` → `WarningToStop` (hostile view) / `AreWeGoodProbe` (neutral) — but it still has **no caller**, and `RelationDelta(FleetNearBorder)` is still hardcoded to **0** (`:90-96`). The class docstring still says *"Nothing calls this yet."* This is the carrier-diplomacy table, written and waiting for a wire. **This is the genuine core of PART B, and the merge did not touch it.**
3. **A "show the flag / blockade / station" posture order + its effect — MISSING.** Movement has move/attack/warp but no deliberate *presence* posture, and no morale/legitimacy term responds to a superior hostile presence (only the *declared-war* flag moves legitimacy today, via `LegitimacyProcessor.WarTermFor`). Add the posture order + an "intimidation" term.

None of these is large. All three follow the codebase's **additive, default-off-in-tests, on-in-real-games** convention — and note (per the merge) that once a behavior is wired under the existing `EnableOrderEmission`/`EnableDiplomaticProposals` gates, it goes **live automatically** in every menu-started game (those gates are already flipped on), the same way the military mirror did.

---

## PART C — THE NEGOTIATION SCENE (the Fallout conversation)

*The face. Where all the leverage from Parts A and B gets spent, and where a conversation changes how they feel about you — with many endings.*

### C.1 Why this is the payoff screen for the whole cluster

The developer's Fallout reference maps precisely onto the design's already-locked **"negotiation-as-a-scene (C2)"** (`docs/society/DIPLOMACY-DESIGN.md:344-356`), which calls itself the **convergence interface**: diplomat skill + military leverage + economic chokehold + espionage intel all flow *into one interaction.* Everything Parts A and B build is **ammunition for this screen.**

What made Fallout's conversations sing, mapped to Pulsar:

- **You talk to a character, not a number.** Each rival faction is led by a commander entity with an archetype, traits, and a **hidden agenda** (`docs/ai/AI-COMMAND-AND-COMMUNICATION-DESIGN.md` gives every faction/officer scored-choice weights). "Chancellor Vex is proud and fears your fleet," not "+trade with Faction 3."
- **Options are gated by what you bring to the table** — the speech-check equivalent:
  - **Persuade** — gated by your diplomat's skill (the M3 people pool: Envoy/Ambassador).
  - **Intimidate** — *only available if you have real leverage*: a fleet parked loud on their border (§B) or a chokehold on their fuel (§A).
  - **Bribe** — spend money (needs the cross-faction transfer from §A.5).
  - **Appeal to a shared enemy** — gated by a *fact* (you're at war with their rival).
  - **Deceive / bluff** — mass a dark fleet and claim peace; but **if their intel on you is good, they catch it** and it craters trust. Fallout *plus poker.*
- **Their disposition shifts and is remembered.** The relationship track (`RelationshipState.RelationScore`, −100..+100, BUILT) *is* the disposition meter; the "memory" is that it's earned and lost by your *actions* over time.
- **Hidden information makes it a reading game.** You don't see their *true* stance or agenda unless you've invested in intel — a posted **Ambassador** or a **spy** converts hidden→known (`docs/society/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md`, the Information Ledger). So a "speech check" is a gamble *under fog* — which is what makes bluffing and reading real.
- **Many endings at the scene scale.** The same encounter can end in alliance, a signed deal, an insult that craters the score, or a declaration of war — depending on the path you walk and the leverage you spend. That's the multiple-outcome Fallout feel, one conversation at a time.

### C.2 What exists to build it on (and the one honest hole)

- **The overture seam is written.** `Factions/ReactiveDiplomacy.cs:58` `Overture()` already enumerates the AI's *opening lines* — `AreWeGoodProbe`, `AllianceOffer`, `TradeProposal`, `WarningToStop` — and their gating conditions. It is **DEAD** (no caller) but it's the natural mouth for "the AI opens the conversation."
- **The reactive engine design is locked.** `docs/society/DIPLOMACY-DESIGN.md:429-450` — *a sim OBSERVATION (your fleet near their border, you at war with their enemy, you weak, a shortage they have) → the faction generates an OVERTURE/QUERY → you respond → the response nudges the relation and reveals or conceals your intent.* The developer's own example — *"the AI sees your fleet near their space and messages a diplomat asking 'Are we good?'"* — is already the headline row of that table. **Presence (§B) is the #1 observation that should trigger it.**
- **A modal-choice UI primitive exists.** `Pulsar4X.Client/Interface/Widgets/ResultModal.cs:72` supports fully custom buttons — the reusable seed of a choice popup.
- **The honest hole:** there is **no branching-conversation engine and no player-response side.** The diplomacy window is read-only (`DiplomacyWindow.cs:58`); interactive diplomacy is DevTools-only today. So the scene needs: (a) the **observation feeder** (§B.2's loop — also unblocks presence), (b) a **player-response scored-choice model** (the mirror of the AI's `Overture` — options gated by leverage/skill/intel), and (c) the **conversation UI** built on `ResultModal`. These three are the real build; the AI's side of the table is mostly written.

---

## The EXISTS / MISSING / NEEDS-CHANGE ledger (file:line — the honest picture)

| Piece | State | Evidence |
|---|---|---|
| **DiplomacyDB + RelationshipState** (score track, stance ladder, treaty flags, war latch) | ✅ BUILT | `Factions/DiplomacyDB.cs:21,29`; `Factions/RelationshipState.cs:12,52,58,62-70,111` |
| `TradeAgreement` / `LogisticsAccess` flags per pair | ✅ BUILT (boolean only) | `RelationshipState.cs:64,66` |
| Tariff-rate / embargo scalar per pair/good | ❌ MISSING (only the boolean) | needs a new field on `RelationshipState` |
| Single-faction Ledger + `Trade` category | ✅ BUILT | `Factions/Ledger.cs:8-21,61-142` |
| Cross-faction `Transfer(from,to,amount)` | ❌ MISSING (only espionage `StealFunds` crosses lines) | `DIPLOMACY-DESIGN.md:70,466` |
| Intra-faction logistics market + price weight | ✅ BUILT | `Logistics/LogisticsProcessor.cs:87,128-152` |
| Cross-faction **access** gate (diplomacy-driven) | ✅ BUILT (default-off flag) | `LogisticsProcessor.cs:166-177` |
| Cross-faction priced **buying/selling** + dependency accumulator | ❌ MISSING | logistics never touches a `Ledger`; no per-pair goods ledger |
| Trade-income processor | ⚠️ BUILT but GATED + flat-rate | `TradeIncome.cs:19`; `TradeIncomeProcessor.cs:25` (`EnablePayout=false`) |
| **Exchange catalog** (27 levers incl. tariff/sanctions rows) | ⚠️ BUILT-as-DATA, INERT (no consumer) | `Factions/ExchangeCatalog.cs:55,65,100` |
| Commitment executor (deal → real orders, promised-vs-delivered) | ❌ DESIGNED-ONLY | `DIPLOMACY-DESIGN.md:374-379` |
| **EMCON loud/dark posture** ("choose to be seen") | ✅ BUILT | `Sensors/Emcon/FleetEmcon.cs:55`; `EmconActivityProcessor.cs:104` |
| **Fog-limited "how scary is that fleet"** | ✅ BUILT | `Factions/ThreatAssessment.cs:27,70` |
| Park a fleet in a body's low orbit | ✅ BUILT | `Movement/MoveToSystemBodyOrder.cs:55` |
| **Standing consumption need** (food / power) — demand→shortage→morale+starvation→buildable supply→grave rung | ✅ BUILT but **local-only + off-by-default** (not import-cuttable → not yet a trade weapon) | `Colonies/SustenanceProcessor.cs:57-60`; `FoodProductionAtbDB.cs`; `PopulationProcessor.cs:52-57`; demand coeff defaults 0 (`ColonySustenanceDB.cs:23`), on only for UMF/UEF-DevTest |
| **Ship fuel** as a *consumed, haulable, cuttable* cargo (the better weaponizable need) | ✅ BUILT (consumption + cargo); ❌ MISSING a cross-faction fuel market | `Movement/NewtonSimpleProcessor.cs:98-99`; `fuel-cargo-hold`; rp-1/methalox/hydrolox |
| **Territory / border model** ("near their space") | ❌ MISSING (derive from colonies) | `NPCDecisionProcessor.cs:347` (comment, was `:271`); dead `FactionSystemInfoDB.cs` |
| **Presence → reaction feed loop** | ❌ MISSING (table exists, no caller, delta=0 — merge did NOT touch it) | `ReactiveDiplomacy.cs:64,90-96` |
| "Show-the-flag / blockade / station" posture order + intimidation morale term | ❌ MISSING | movement has move/attack only; morale has no *presence* term (though it now has food/power *shortage* terms — `ColonyMoraleDB.cs:159-162`) |
| AI picks **EMCON loud/dark** by personality | ✅ BUILT (merge) — combat-stealth stance, NOT presence-targeted | `NPCDecisionProcessor.cs:779-806` (`RunEmconPolicy`) via `DecisionScorer.cs:82-103` |
| Reactive **drift** (militarist cools / treaty warms) | ✅ BUILT / always-on | `NPCDecisionProcessor.cs` `RunDiplomaticDrift` |
| Reactive **"Are we good?" `Overture`** table | ⚠️ BUILT but DEAD (no caller) | `ReactiveDiplomacy.cs:58` |
| NPC treaty/coalition/war policy (mirror) | ✅ BUILT and **ON in every menu-started game** — military mirror (declare-war→invade) fires by default | gates default-false only for tests, flipped on at `NewGameMenu.cs:547-550`; `RunWarPolicy`/`RunTreatyPolicy`/`RunEspionageMirror`; `ConquerResolver` LOAD/SAIL/LAND |
| Casus belli + militarism gate | ✅ BUILT (one-time morale delta computed, unapplied) | `Factions/CasusBelli.cs:10,52`; `DIPLOMACY-DESIGN.md:314` |
| **Branching conversation / event-choice engine** | ❌ MISSING (generic `ResultModal` unused for diplomacy) | `Client/…/ResultModal.cs:72`; `DiplomacyWindow.cs:58` |
| Morale / legitimacy / rebellion stack (the damage tanks) | ✅ BUILT | `Colonies/ColonyMoraleDB.cs`, `LegitimacyProcessor`, `RebellionDB.cs` |

**Reading (updated post-merge 2026-07-16):** the *substrate* is overwhelmingly BUILT, and the merge **built more of the scaffolding** — a standing-need pattern (food/power sustenance), an AI that acts for real in every game (gates on; military mirror live), and personality-driven EMCON. The *coercion layer proper* — cross-faction money, tariffs/embargoes with teeth, the dependency reading, the **presence feed loop**, and the conversation scene — is still **inert or missing** (re-confirmed on the merged tree). This remains a wire-and-add-a-lever program — and it's now closer to "extend a proven pattern" than "build from scratch," because the standing-need loop and the NPC-acts-back mirror both already exist for other pillars.

---

## Cradle to grave (the acceptance test for each pillar)

**Trade / economic warfare:** a **mineral** is mined → refined to a **material** → a rival grows **dependent** on your supply (the battlefield forms) → you research/build the **customs & commerce capability** (a colony/station component — trade must be a *thing you build*, per `CONVENTIONS §6`, not an engine flag) → the **decision**: tariff / embargo / dump / bloc, or bind them to peace → the **damage** lands in their morale/treasury → **grave:** they find an alt-supplier, achieve autarky, or a bloc forms against you and *your* merchants revolt — the weapon can be blunted and turned, so it's a real contest, not an "I win" button.

**Power projection:** ships are built from minerals→materials (BUILT) → massed into a **fleet** (BUILT) → the **decision**: park it, and go **loud or dark** (the EMCON order) → the **pressure** shifts their stance / feeds the negotiation → **grave:** a bluff called burns your **credibility** (the reputation loss), or the intimidation **backfires** against a militarist regime and hands them legitimacy. Presence is earned and can be squandered.

**The negotiation scene:** a **diplomat** is drawn from the people pool (scarce talent) → **posted** as Ambassador (built/installed) → opens the **window** that converts their hidden agenda to known → the **decision**: which leverage to spend, and whether to bluff → the **outcome** moves the relationship track → **grave:** a diplomat expelled, caught bluffing, or assassinated; a burned reputation that every faction remembers. Losing a master diplomat hurts like losing a veteran admiral.

---

## Connections (Prime Directive)

- **Economy / logistics / commerce** (BUILT substrate) — the trade weapons route through `Logistics` (deny/priority routes) + `Ledger` (cross-faction transfer, tariff revenue). `docs/economy/RESOURCES-AND-MATERIALS-DESIGN.md` is the economy this fights over.
- **Colonies / morale / legitimacy / rebellion** (BUILT) — the *damage tank* for both trade and presence; add a supply-shortage morale term and a presence-intimidation term.
- **Sensors / detection / EMCON** (BUILT) — presence is *only* pressure if they can see it; the loud/dark posture is the message; fog makes the "how scary" read honest and makes bluffing real.
- **Fleets / movement** (BUILT) — parking + a new "station/blockade/show-the-flag" posture order; a blockade is the seam where the trade theater and the military theater fuse.
- **Diplomacy** (`DIPLOMACY-DESIGN.md`, BUILT substrate + locked teeth) — the parent. This doc *consumes* its inert pieces: the exchange catalog, the reactive `Overture` table, the commitment model, the casus-belli gate. Presence and trade become the **observations** that drive the reactive engine and the **leverage** spent in the C2 scene.
- **Espionage / intelligence** (BUILT; engine-default off but **ON in every menu-started game** — `NewGameMenu.cs:549-550`) — the Information Ledger is what buys the *truth* about a rival's disposition, making the negotiation a reading game; caught spying is a betrayal that craters the same track. (In real play the NPC espionage mirror + intel ledger actually run.)
- **Influence** (`INFLUENCE-PILLAR-DESIGN.md`, concepts) — the sibling fifth-ish pillar; both are "war without a warship." Trade attacks the treasury/supply; influence attacks allegiance. Same skeleton.
- **Internal politics / government** (BUILT/designed) — the two-way handoff: a trade deal grows the Merchant bloc; an embargo that causes shortages feeds unrest; a militarist regime *rewards* being intimidated (backfire); government re-skins how deals are made (a democracy ratifies, a dictatorship decrees).
- **People / delegates** (M3 pool, designed) — the Trade Minister (auto-run the economic theater) and the diplomats (the hands). Same six-rung leader pipeline as every pillar (`docs/society/GOVERNANCE-AND-DELEGATION-DESIGN.md`).
- **NPC AI** (`NPCDecisionProcessor`, **ON in real games as of the merge**) — the **mirror is a co-requisite**, same lesson as espionage/diplomacy/influence. But the framing is now updated: the NPC action gates default false *only* for engine-test byte-identity and are **flipped on in every menu-started game** (`NewGameMenu.cs:547-550`), and the **military mirror already retaliates by default** — `RunWarPolicy` declares an opportunistic war of choice and `ConquerResolver` runs the full load→sail→invade chain. So "an opponent who never retaliates = solitaire" is stale for a real game: the NPC *does* fight back. What's genuinely missing is the **trade-war / power-projection / negotiate-back** behavior in the `Tick` — and once built there, it goes **live automatically** under the already-on gates (the way the war mirror did). So this is a **build** co-requisite, not a flip-a-flag one; the military pillar has already proven the pattern works end-to-end. *(Note: even the live war trigger scores on true `FactionRollup.MilitaryStrength`, not on any trade coverage-ratio — so wiring trade leverage into the AI's pressure decision is part of the trade-mirror build.)*

---

## Build order (smallest gauged slices first; each additive + default-off)

Trade and presence share the same three enablers (cross-faction plumbing, a derived territory read, the reactive feed loop), so build those once and both pillars light up.

**Rung 0 (the true foundation — see §0b; revised after the 2026-07-16 merge): make an existing need *strangleable*.** The standing-need *pattern* now exists — food/power sustenance (`SustenanceProcessor`) already does demand → shortage → morale + starvation → buildable supply → grave rung. But it's local-only, so nothing can cut it. So rung 0 is no longer "build a need from scratch"; it's **"make a need meetable by import so an embargo can cut it."** Smallest slice: **ship fuel** — it's *already* consumed by movement *and* already a haulable cargo, so the only missing piece is letting it be **bought/sold across faction lines**. *Gauge:* a fleet/colony that depends on imported fuel loses readiness (or industry) when the cross-faction supply is cut. Without a cuttable need, everything below is a punch at air.

1. **Cross-faction money + priced cross-faction cargo** — add `Ledger.Transfer(from,to,amount)`; make a cross-faction cargo order book payment. *Gauge:* a test that moves goods A→B and asserts money moved B→A. Smallest thing that makes "trade" real.
2. **The dependency accumulator + coverage ratio** — a per-pair, per-good running ledger of who-bought-what, expressed as the **coverage ratio** (got ÷ needed, the §0b keystone). *Gauge:* after N cycles the reading matches the goods that flowed, and a colony denied a needed good shows a coverage ratio falling toward 0. This is the battlefield map (§A.1) AND the number the AI reads.
3. **The tariff/embargo scalar** — a rate + embargo latch on `RelationshipState`; the cross-faction cargo path reads it (tariff → revenue + higher price; embargo → route denied). *Gauge:* an embargo zeroes the flow and a shortage term appears on the buyer's colony morale.
4. **The pain curve on the EXISTING shortage term** — the merge already wired a supply-shortage → morale + starvation term (`ColonyMoraleDB.cs:159-162`, = `1 − coverage`), so this slice *reuses* it rather than building it: route the trade coverage-ratio into that shortage input and bend the response through the **exponential pain curve** (§0b: flat above ~0.9, steep below ~0.3). *Gauge:* a sustained embargo drives a dependent colony toward the rebellion threshold, and the pain is negligible at high coverage but catastrophic once coverage collapses. **This is the "trade damage is real damage" proof AND the exponential-bite proof.**
5. **The derived territory read + the presence feed loop** — "claim = my colonies' systems"; each cycle scan the contact table for hostile presence and feed `ReactiveDiplomacy` (wire `RelationDelta(FleetNearBorder)` off 0; call `Overture`). *Gauge:* a loud fleet parked over a rival colony fires an `AreWeGoodProbe`; the same fleet dark fires nothing.
6. **The "station / show-the-flag / blockade" posture order + intimidation term** — a deliberate presence order; a superior hostile presence applies a morale/legitimacy pressure (and *backfires* under a militarist government). *Gauge:* pressure appears loud, vanishes dark, inverts sign under militarism.
7. **The negotiation scene (C2)** — the observation/overture → **player-response scored-choice model** (options gated by trade-leverage §A + presence-leverage §B + diplomat skill + intel) → a `ResultModal`-based conversation UI. *Gauge:* the same encounter reaches ≥3 distinct outcomes on different leverage/paths.
8. **The NPC mirror + the delegates — BUILD the behavior, the gate is already on.** Add trade-war / power-projection / negotiate-back logic to `NPCDecisionProcessor.Tick` (scoring on the trade coverage-ratio + presence, the way `RunWarPolicy` scores on military strength today); seat the Trade Minister + diplomats in the leader pipeline. Because `EnableOrderEmission`/`EnableDiplomaticProposals` are **already flipped on in every menu-started game**, the behavior goes live automatically once wired — no new flag to flip (this is how the military mirror shipped). *Gauge:* an NPC embargoes you, parks a fleet, and opens a negotiation unprompted, in a normally-started game.

Follow the repo rule: **one slice, push, wait for CI green, then the next.** Each slice stays byte-identical to a stock game until its behavior is wired (the action gates are already on, so wire carefully and gauge each slice).

---

## Locked vs. open

**Proposed as locked (developer to confirm):**
- **Trade is the fifth conquest pillar** — a peer to Military/Espionage/Diplomacy/Influence, same skeleton, same delegate model, same NPC mirror. It finishes the pattern.
- **Trade damage lands in the existing tanks** (morale → legitimacy → rebellion, and treasury) — no new damage model; wire the economic weapons to drain what's already there.
- **Symmetric cost = the deterrence engine** — every embargo hurts the embargoer, so entanglement is a peace lever and autarky is a freedom-to-fight lever. That trade-off is the pillar's headline decision.
- **Presence = pressure, gated by visibility** — the EMCON loud/dark choice IS the message; pressure is a bluff/credibility gamble with a real grave-rung; it backfires under a militarist regime.
- **The negotiation scene is the convergence interface** — trade + presence + espionage + diplomat skill all spend into one Fallout-style branching scene with many outcomes; hidden information (bought with intel) makes it a reading game.
- **The NPC mirror is a co-requisite for all three**, not a later polish.
- **The bite = exponential PAIN on a linear coverage ratio** (§0b, LOCKED 2026-07-14) — the ratio (got ÷ needed) is what the AI reads; the exponential lives in the pain curve on top of it. Damage is never a raw exponential knob.
- **Rung 0: the standing-need PATTERN now exists (food/power); the residual is making a need *cuttable*** (§0b, updated 2026-07-16). Nothing to strangle = no trade war — and today's food need is local-only, so the rung-0 work is a *cross-faction/importable* need (ship fuel is the near-ready target), not a need from scratch.
- **No solo economic-victory** (developer, 2026-07-14) — a dominant player does NOT win by dependency; instead the rivals **UNITE against him**, reusing the existing `GalaxyCrisis` coalition-vs-ascendant trigger fired on economic dominance. (Far-down-the-line; the point is entanglement creates *pressure and coalitions*, not a solo win button.)

**Open (decide when we build):**
- Tariff/embargo math — the exact coverage-ratio→pain curve shape (§0b) and how fast dependency accrues, calibrated so a trade war *bleeds* over months rather than flipping overnight.
- Whether "territory" stays derived-from-colonies (cheap v1) or eventually earns a real sovereignty/border model (needed for cleaner presence + colonization-rights deals).
- Presence-pressure formula — the exact weighting of perceived-strength × proximity × loudness, and the backfire coefficient vs. the militarism dial.
- Negotiation scene scope — how many response verbs (Persuade/Intimidate/Bribe/Appeal/Deceive) ship in v1, and how deep the leader-agenda reveal goes before espionage is built.
- The coalition trigger — what threshold of economic dominance wakes the `GalaxyCrisis` "rivals unite" response, and how it reads a player's dependency web (far-down-the-line).

---

## Relationship to existing docs (don't duplicate)

- **Parent:** `docs/society/DIPLOMACY-DESIGN.md` — the substrate + the locked "politics with teeth" (relationship track, treaties, casus belli, the reactive engine, the exchange catalog, the commitment model). *This doc is the pillar that consumes those inert pieces.* The trade-war levers, the presence lever, and the negotiation scene deepen its EXCHANGE CATALOG and REACTIVE sections; they don't replace them.
- **Sibling:** `docs/society/INFLUENCE-PILLAR-DESIGN.md` — the "war without a weapon" template this pillar follows.
- **Economy it fights over:** `docs/economy/RESOURCES-AND-MATERIALS-DESIGN.md`, `docs/economy/OFF-WORLD-INFRASTRUCTURE-DESIGN.md`.
- **The fog that makes it a reading game:** `docs/combat/DETECTION-DESIGN.md` (EMCON) + `docs/society/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md` (the Information Ledger).
- **The delegates & the scene metaphor:** `docs/society/GOVERNANCE-AND-DELEGATION-DESIGN.md` (Trade Minister + diplomats) and the C2 "negotiation is a closing fight" lock in the diplomacy doc.
- **Tactical vs. strategic force projection (terminology guard):** `docs/combat/CARRIER-DESIGN.md` is *combat* standoff projection (strike craft in a battle); PART B here is *strategic* presence-pressure (coercion without a shot). Complementary — a carrier is the archetypal loud-signature presence asset.
- **The NPC-mirror + standing-need reality this doc now depends on:** `docs/ai/AI-BRAIN-BUILD-TRACKER.md` (the AI's true built-state — gates on, war/EMCON/coalition mirrors live), `docs/DEVTEST-AI-BUILD-LOG.md` + `docs/DEVTEST-RUNTIME-FINDINGS.md` (the food-need + morale-floor fixes and the AI playtest), and the food-sustenance code (`Colonies/SustenanceProcessor.cs`, `FoodProductionAtbDB.cs`). These are why the 2026-07-16 update above softened several "gated / no need" claims.
- **Where opening trade/dependency terms would be authored:** `docs/FACTION-CREATION-GUIDE.md` — a faction's `openingRelations` (target/atWar/score) and `strain` (tax/power/food/manpower demand) nodes are the current authoring surface; cross-faction tariff/embargo/dependency terms would extend them when trade becomes a pillar.
