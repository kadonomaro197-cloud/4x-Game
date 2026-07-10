# AI Personality — Implementation Spec (code-ready)

> **Status: v1 IMPLEMENTATION SPEC (2026-07-10).** The buildable companion to `AI-COMMAND-AND-COMMUNICATION-DESIGN.md` (the design/discussion doc). Where that doc decides *what* the 12-trait system is and *why*, this doc says *exactly what to code and where*, grounded in a five-agent audit of the real source (every file:line below is verified). Nothing here is built yet — this is the map you follow when it's time to code. **The developer's acceptance rule governs: a trait is kept only if it can be tuned, WIRED, and DEMONSTRATED in-game — so this spec's per-trait audit (§1) IS the acceptance test.**

---

## 1. The acceptance audit — which of the 12 are demonstrable, and when

The five-agent audit located each trait's real wiring point. Verdicts: **NOW** = the lever is built, wire it and a CI test proves it today. **LATER(system)** = the trait is real and defined now, but its lever needs a system that isn't built yet — wire it in the same slice as that system (a labeled socket, not "pretty for pretty's sake"). A **down-payment** = a smaller sub-lever demonstrable now.

| Trait | Verdict | Decision point (file:line) | How the 0–1 dial moves it | Gauge |
|---|---|---|---|---|
| **Collectivism** | **NOW** | `CombatEngagement.ShouldRetreat` — casualty const `CombatEngagement.cs:46` (a labeled socket: *"the real threshold comes from doctrine"*) | `retreatFraction = lerp(0.20, 0.95, Collectivism)` | fork `FleetRetreatTests`: 0→flees at 25% loss, 1→fights to wipe |
| **Zealotry** | **NOW** | same `ShouldRetreat` (`CombatEngagement.cs:1158`) + `Treaties.WouldAccept` (`Treaties.cs:66`) + research-queue skip | combat: hard override `if Zealotry≥0.8 return false` (ignores retreat posture too); treaty: `accept = score − Zealotry·ideoDistance`; taboo: skip tech category | won't-retreat test (inverse of `WithdrawPosture_BreaksOff`) + refuse-good-treaty test |
| **Honor** | **NOW** | `NPCDecisionProcessor.Tick` (`:64`); keep-faith already live via `CombatEngagement.AreHostile→AtPeace` (`:1271`) | `breakScore = payoff − Honor·betrayalCost`; renege iff >0 | pact-intact (Honor 1) vs pact-broken (Honor 0) — needs ~12-line `Diplomacy.BreakTreaty` helper |
| **Xenophobia** | **NOW** | `FirstContact.OnDetection` (`FirstContact.cs:57`) + `Treaties.WouldAccept`/`RequiredScore` | contact: `openingScore = 0 − Xenophobia·range`; treaty: `required += Xenophobia·penalty` | contact-stance test: Xeno 1→Hostile on sight, 0→Neutral |
| **Ambition** | **NOW** (colonies+queue); LATER (territory model) | `NPCDecisionProcessor.Tick` `:79` | expansion cadence = `p(colonize)=Ambition`; queue split `military=1−Ambition` | `FactionInfoDB.Colonies.Count` rises for high, flat for low |
| **Ruthlessness** | **NOW** (bombardment); LATER (WMD); **internal-purge facet CUT** | target-select → `OrderAttack` on colony entity → `OnColonyDamage` (`DamageProcessor.cs:289`, **verified built**: pop casualties + atmosphere + garrison-soften) | `bombardScore = mil + Ruthlessness·civilianCoercion`; 0 never targets pop, 1 bombards | pop-casualties-applied (Ruth 1) vs colony-untouched (Ruth 0) |
| **Authoritarianism** | **NOW** (appease/tax); LATER (suppress-by-force, slave-*output* bonus) | unrest fork on `RebellionDB.IsRebelling` (`:37`) / `LegitimacyDB.IsCollapsing` (`:107`) | `TaxRate = Authoritarianism × GovernmentDB.TaxCeiling()`; >0.5 holds under unrest, ≤0.5 cuts to appease | `SocietyReadout`: low-Auth cuts tax→rebellion clears; high-Auth holds→runs to window |
| **Aggression** | **NOW** (posture/attack); LATER (build-share) | posture + `OrderAttack` built; the *scored choice* is the `NPCDecisionProcessor.Tick` stub `:79` | posture = Aggression band (Hold/ReturnFire/WeaponsFree); attack gate | posture-flip test: 0→standoff (`NewEngagementImminent` false), 1→engages |
| **Risk** | **NOW** (engage-vs-wait fn); LATER (reserves; needs enemy-strength read) | pure `ShouldEngage(own, enemy, Risk)`; execute via `OrderAttack`/`SetEngagementPosture` | `engage when own ≥ (2−Risk)·enemy` | engage-at-parity (Risk 1) vs demand-2× (Risk 0), stamped strengths |
| **Guile** | **LATER (espionage engine)** — **down-payment: EMCON dark NOW** | espionage unbuilt (only a route enum); `FleetEmcon.SetPosture` built | covert budget vs fleet = Guile; EMCON Silent when Guile>threshold | EMCON: Guile 1→fleets Silent, `DetectabilityRange_m` shrinks |
| **Altruism** | **LATER (diplomacy commitment engine)** — **down-payment: direct Ledger gift NOW** | `ExchangeCatalog` is data-only, no executor (`ExchangeCatalog.cs:59`) | `aidScore = Altruism·need − selfCost`, act iff >0 | (deferred) money moves donor→recipient at net loss vs Altruism 0 never |
| **Curiosity** | **LATER (exploration content)** | survey orders exist; no anomaly/"find" system to value | survey/explore budget = Curiosity | (deferred) high-Cur dispatches surveyors, low-Cur doesn't |

**Bottom line for the acceptance rule:** **9 of 12 are demonstrable now** (Collectivism, Zealotry, Honor, Xenophobia, Ambition, Ruthlessness, Authoritarianism, Aggression, Risk — most fully, a few by their primary facet). **3 are real-but-sequenced** (Guile→espionage, Altruism→commitment engine, Curiosity→exploration) — each *defined* now and wired when its system lands, two with a down-payment demo available today. **Nothing is cut except one facet** (Ruthlessness's "purge your own population" — no API exists). So we keep all 12; three carry an explicit "wire-when" tag rather than shipping as dead dials.

---

## 2. Data model — `PersonalityDB`

**Decision: one host-agnostic DataBlob, two owners** (a faction entity AND a commander entity) — the same pattern `LegitimacyDB`/`ColonyMoraleDB` use to ride a colony *or* a station. Chosen over a bare struct (the `DoctrineVector` shape) because drift needs a mutable mood bag, a factor gauge, and a single mutation entry point. New file `GameEngine/People/PersonalityDB.cs`:

```csharp
public class PersonalityDB : BaseDataBlob
{
    // 12 durable TRAITS (character), 0..1, default 0.5 = NEUTRAL (see the trap below)
    [JsonProperty] public double Aggression { get; internal set; } = 0.5;
    // ... Ambition, Risk, Honor, Xenophobia, Ruthlessness, Curiosity,
    //     Zealotry, Guile, Collectivism, Authoritarianism, Altruism (all = 0.5)

    // fast MOOD (state): transient 0..1 intensities that DECAY each cycle; several can co-exist
    [JsonProperty] public Dictionary<Mood, double> Moods { get; internal set; } = new();

    // the gauge — why traits moved this era (mirrors ColonyMoraleDB.Factors)
    [JsonProperty] public Dictionary<string, double> LastDriftFactors { get; internal set; } = new();

    public enum Trait { Aggression, Ambition, Risk, Honor, Xenophobia, Ruthlessness,
                        Curiosity, Zealotry, Guile, Collectivism, Authoritarianism, Altruism }
    // + Get(Trait)/Set(Trait) indexer to keep the enum and properties in sync

    public PersonalityDB() {}
    public PersonalityDB(PersonalityDB o) { /* copy 12 + Moods + factors */ }
    public override object Clone() => new PersonalityDB(this);
}
public enum Mood { Wounded, Vengeful, Emboldened, Cornered, Rebuilding }
```

- **⚠ The one trap: `default` is all-EXTREMES, not neutral.** A zeroed struct/blob = Aggression 0 (total pacifist) AND every other trait at an extreme simultaneously. Every construction path MUST seed **0.5** (the property initializers above do it; the scenario-load and factory paths must too). This is the single most likely silent bug. Mirrors how `GovernmentDB` ships all-Mid so New Game is unchanged.
- **Scenario JSON authoring** — mirror the `DoctrineVector` parse in `FactionFactory.LoadFromJson` (`FactionFactory.cs:71-81`): read an optional `"personality"` node next to `"doctrine"`, each key `?? 0.5f`. Example: `"personality": { "aggression": 0.9, "honor": 0.85, "ruthlessness": 0.2, ... }`.
- **Attach:** faction entity in `FactionFactory` (all four creation paths); officer entity in `CommanderFactory.Create*`. Both default neutral.

---

## 3. Scoring engine — how a 0–1 dial becomes "attack vs. build refinery"

Every AI decision is *list options → score each → pick best (or weighted-roll)*, and personality weights the scores (design §3c). A **feature-vector dot-product** is the reusable shape. New file `GameEngine/Factions/AIScoring.cs`:

```csharp
public enum Feature { MilitarySolve, EconGain, TechGain, ExpansionGain, RiskLevel,
                      Ruthless, Covert, Cooperative, Doctrinaire }

public interface IScoredOption { IReadOnlyDictionary<Feature,float> Features { get; } }

// The ONE place that encodes "which trait cares about which axis" (a one-line edit adds a trait).
static class PersonalityWeights {
  static readonly Dictionary<Feature,Func<PersonalityDB,double>> _map = new() {
    { Feature.MilitarySolve, p => p.Aggression },
    { Feature.EconGain,      p => p.Ambition*0.5 + (1-p.Aggression)*0.5 },
    { Feature.RiskLevel,     p => (p.Risk-0.5)*2 },   // >0.5 seeks risk, <0.5 penalizes
    { Feature.Ruthless,      p => p.Ruthlessness },
    { Feature.Covert,        p => p.Guile },
    { Feature.Cooperative,   p => p.Altruism*0.5 + (1-p.Xenophobia)*0.5 },
    { Feature.Doctrinaire,   p => p.Zealotry },
    // ... TechGain, ExpansionGain
  };
  public static double Of(PersonalityDB p, Feature f) => _map.TryGetValue(f, out var fn) ? fn(p) : 0;
}

static class DecisionScorer {
  public static double Score(IScoredOption o, PersonalityDB p)
    => o.Features.Sum(kv => kv.Value * PersonalityWeights.Of(p, kv.Key));
  public static T PickBest<T>(IEnumerable<T> opts, PersonalityDB p) where T:IScoredOption
    => opts.OrderByDescending(o => Score(o,p)).FirstOrDefault();
  // PickWeighted(opts, p, StarSystem.RNG) for variety — deterministic (never Random.Shared)
}
```

**Worked proof it makes distinct factions** (this IS the fingerprint gauge in miniature): options `warship{MilitarySolve:1, RiskLevel:0.3}` vs `refinery{EconGain:1}`.
- **Klingon** (Aggr 0.9, Amb 0.5, Risk 0.8): warship = 1·0.9 + 0.3·0.6 = **1.08**; refinery = **0.30** → **builds warship**.
- **Ferengi** (Aggr 0.2, Amb 0.8, Risk 0.3): warship = 0.2 + 0.3·(−0.4) = **0.08**; refinery = **0.80** → **builds refinery**.

Same function, opposite output, purely from the dials. If two contrasting factions *don't* split here, a trait is unwired.

---

## 4. Drift — mood vs. trait, filtered through identity

Two timescales in one monthly tick (reuse `NPCDecisionProcessor`, already fires monthly per faction). New file `GameEngine/Factions/PersonalityDrift.cs` — pure static, mirrors `ReactiveDiplomacy.RelationDelta`.

- **Fast (mood):** every cycle, each mood decays `intensity *= (1 − 0.15)`, drop below 0.02. **Refinement (also a Collectivism lever):** high-Collectivism factions decay Wounded/Cornered faster (`×(1+Collectivism)`) → a hive doesn't brood = morale-proof.
- **Slow (trait):** only an event moves a trait, and only a *sliver* (`TraitDriftScale = 0.01` per event) — a "lost war" nudges ~0.01; an era of them crosses a visible threshold.

**The single mutation entry point** (like `AdjustScore` is the only mutator of `RelationScore`):

```csharp
public static void ApplyEvent(PersonalityDB p, EventKind e, double weight = 1.0) {
  var pr = PressureOf(e);                       // event → (trait pushes, mood sets) table
  p.LastDriftFactors.Clear();
  foreach (var (trait, raw) in pr.TraitPush) {
    double resist = IdentityResistance(p, trait, raw);          // identity filters the push
    double delta  = raw * weight * TraitDriftScale * (1 - resist);
    p.Set(trait, Clamp01(p.Get(trait) + delta));
    p.LastDriftFactors[$"{e}:{trait}"] = delta;                 // the gauge
  }
  foreach (var (mood, i) in pr.MoodSet)
    p.Moods[mood] = Math.Max(p.Moods.GetValueOrDefault(mood), i * weight); // feelings are immediate
}

// THE load-bearing formula (solves the Klingon puzzle): resist LOSING a trait you're high in.
static double IdentityResistance(PersonalityDB p, PersonalityDB.Trait t, double raw) {
  double cur = p.Get(t);
  return raw < 0 ? cur          // high Aggression → strongly resists a "-Aggression" push
                 : (1 - cur);   // already-high trait resists going higher (diminishing)
}
```

**Event→pressure table** (the `RelationDelta` twin — extend as event sources land): `LostWar → {Aggr −0.5, Risk −0.4} + moods {Wounded 0.8, Vengeful 0.5, Rebuilding 0.6}`; `BetrayedByAlly → {Honor −0.4, Xeno +0.5, Guile +0.3}`; `RebellionCrushed → {Auth +0.5, Ruth +0.2}`; `WonWar → {Aggr +0.3, Amb +0.3} + Emboldened`; etc.

**Worked (the §3e trio, same `LostWar`):** Klingon (Aggr 0.85) resists the −Aggression ~0.85 → barely moves, but Wounded+Vengeful+Rebuilding latch full → *pauses new wars, stays a warrior, returns angrier*. Trader (Aggr 0.3) resists ~0.3 → drifts cautious. Swarm (Collectivism 1) → Wounded decays instantly → regroups unchanged.

**Mood feeds a real decision (so it's not pretty):** the "start a new war?" objective score is multiplied by `(1 − Moods[Wounded])` in the `NPCDecisionProcessor.Tick` body — a mauled warrior scores conquest low this cycle without its Aggression trait changing.

**Wiring:** `DecayMoods` + officer iteration go at the top of `Tick` next to `RunDiplomaticDrift` (`NPCDecisionProcessor.cs:69`). `ApplyEvent` call-sites attach at each event source (war resolution, rebellion, betrayal) as they're reachable — table + math ship first (CI-testable), call-sites wire incrementally.

---

## 5. Blend — faction identity × seated officer

**Rule (the developer's exact words): faction outweighs the officer by default; tenure + experience can override in the officer's domain.** In `AIScoring.cs`:

```csharp
public const double MaxOfficerOverride = 0.45;  // a legend bends the seat ~45%, never 100%
public const double TenureYearsToFull  = 10.0;  // "a decade in the seat" → full tenure weight

public static double OverrideWeight(CommanderDB o, DateTime now) {
  double tenureW = Clamp01((now - SeatSince(o)).TotalDays/365.0 / TenureYearsToFull);
  double expW    = o.ExperienceCap>0 ? Clamp01((double)o.Experience/o.ExperienceCap) : 0;
  return MaxOfficerOverride * (0.5*tenureW + 0.5*expW);   // BOTH needed: green 10-yr ≠ master; brilliant rookie ≠ boss
}
public static double EffectiveTrait(double faction, double officer, double alpha)
  => faction + (officer - faction) * alpha;   // alpha 0 = pure faction, 0.45 = max bend
```

A seat's decision reads `EffectiveTrait(faction.X, officer.X, OverrideWeight(officer,now))`. New xenophobic diplomat in a cooperative faction (α≈0.05) → *slightly* pricklier. Same diplomat after a decade + betrayals (α→0.45, and his own Xenophobia drifted up) → runs a genuinely hostile policy = the "decapitation matters" payoff. **Competence is separate** (quality of execution, a different multiplier) — traits pick *which* choice, competence tilts *how well*.

**Officer career-drift** uses the same `ApplyEvent` on the officer's `PersonalityDB` with a `CareerEvent` table: `FoughtManyBattles → {Aggr +0.4, Ruth +0.3, Risk −0.2}`; `BetrayedInNegotiation → {Xeno +0.5, Honor −0.2, Guile +0.3}`; `CrushedARebellion → {Auth +0.5, Ruth +0.2}`. Runs on the same monthly tick over `FactionInfoDB.Commanders` (`FactionInfoDB.cs:73`).

**⚠ NEEDS-CHANGE before the blend goes live (audit found a gap):** `CommanderDB.CommissionedOn`/`RankedOn` are declared but **never set** by `CommanderFactory` (`CommanderFactory.cs:40-83`) — they default to year 0001. And there's **no `AssignedOn` date**, so "years in *this seat*" can't be computed. Fix: stamp `CommissionedOn = game.TimePulse.GameGlobalDateTime` at creation, and add `[JsonProperty] DateTime AssignedOn` stamped whenever `AssignedTo` is set. Until then tenure approximates from `RankedOn`.

---

## 6. Trait → a game number (the `ModifiableValue` pattern)

For traits that modify a standing number (Authoritarianism → labor output, etc.), mirror `ResearchProcessor.RefreshPointModifiers` (`ResearchProcessor.cs:246`) exactly — one **stable modifier id** per trait-effect, a refresh that removes-then-re-adds, driven off the stored trait. This gets save/load + recompute for free (it's `[JsonProperty]`-serialized). Example:

```csharp
public const string AuthLaborId = "trait-authoritarian-labor";
public static void RefreshLaborOutput(ModifiableValue<double> output, PersonalityDB p) {
  output.RemoveModifier(AuthLaborId);
  double mult = 1.0 + (p.Authoritarianism - 0.5) * 0.8;   // 0.5→1.0 (New Game unchanged), 1→1.4, 0→0.6
  if (mult == 1.0) return;
  output.AddModifier(new Modifier<double>(AuthLaborId, "Authoritarian Coercion", mult, (c,m)=>c*m, 1.0f));
}
```

**No double-count with `GovernmentDB`:** the trait and the government each add their OWN modifier id to the same `ModifiableValue`, so they stack cleanly and neither clobbers the other. (See §8 for the full split.)

---

## 7. Demonstration methodology — the acceptance gate (CI can't run the client)

**7a. Per-trait flip test** (the template is `DiplomacyDriftTests`): two personalities differing only in trait T (0 vs 1), feed the decision T's lever moves, assert the choice *diverges*. **A trait with no passing flip test does not go live.**

```csharp
[Test] public void Aggression_FlipsTheAttackDecision() {
  var sit = new Situation { RelativeStrength = 1.0, Losses = 0.5 };  // an even fight
  Assert.That(AiDecision.ScoreAttack(new PersonalityDB{Aggression=1.0}, sit), Is.GreaterThan(0));
  Assert.That(AiDecision.ScoreAttack(new PersonalityDB{Aggression=0.0}, sit), Is.LessThan(0)); // opposite
}
```

Drift half gets the same treatment: `LostWar_DriftsByCharacter` asserts the warrior HOLDS Aggression (within 0.005) while the trader drops, and the warrior's Wounded mood > 0.5.

**7b. `PersonalityReadout`** (mirror `SocietyReadout` — pure string builder, CI-tested, thin DevTools "Dump Personality" caller + a `[PERSONALITY]` `SessionLog` heartbeat line). Shows **traits (only the off-neutral ones, for legibility) + current mood + last drift breakdown (why it moved)** — the client-observable proof.

**7c. Fingerprint automated test** (the whole-set acceptance gate): each §3a-bis row becomes a named profile (`Borg()`, `Klingon()`, …), then assert divergent behavior — `Borg_IsMoraleProof_KlingonIsNot` (Collectivism: Borg `WouldRout`==false, Klingon==true after the same loss), `Minbari_SurrenderAtTheBrink_HarkonnenDoesNot` (Zealotry), Culture-vs-Ferengi (Altruism), Romulan-vs-Klingon (Guile). **Green across the fingerprint table = the trait set is real.**

---

## 8. Reconciliations & gaps the audit surfaced (read before coding)

1. **Colony bombardment IS built** (contradiction resolved in source): `OnColonyDamage` (`DamageProcessor.cs:289-323`) applies population casualties, atmospheric contamination, installation damage, and garrison-softening. The economy agent's "commented out" flag was a **false alarm** — it tripped on `ColonyComponentDictionary` ("never populated"), which is a **vestigial, unused** targeting hook; the real passes read `ComponentInstancesDB.AllComponents` and `ColonyInfoDB.Population`. So **Ruthlessness (external) is wirable now.** Caveat: the AI must target the colony *entity* via `OrderAttack` (the fleet auto-resolver fights fleets, not colonies), so "decide to bombard" is the new decision work on a built damage kernel.
2. **Authoritarianism vs. `GovernmentDB` — the split, concretely.** `GovernmentDB` (the `Authority` notch, `GovernmentDB.cs:29`) already owns the RULES/CEILINGS: `TaxCeiling()` (`:68`), `CrewPolicy()`=conscription (`:58`), `Discontent()` (`:62`), `MoraleWeight()` (`:71`). The **trait** is TEMPERAMENT = *where inside those ceilings the AI operates*: it sets `ColonyEconomyDB.TaxRate` in `[0, TaxCeiling()]` (`TaxRate = Authoritarianism × TaxCeiling()`) and picks the unrest fork; it **must not** re-implement a ceiling. The tension (authoritarian temperament under a democratic structure → pressure to drift the `Authority` notch) is the intended feature. Different quantities → no double-count. Check `GOVERNMENT-AND-POLITICS-DESIGN.md` at wire time.
3. **No fleet-morale/rout system exists** — combat retreat is pure casualty math in `ShouldRetreat`. Collectivism's "morale-proof" flavor rides that casualty threshold, not a morale model. Don't spec against a morale system that isn't there.
4. **The shared gate:** every trait's live behavior is gated on the same two unbuilt pieces the design already names — the `NPCDecisionProcessor.Tick` decision body (`:79`, still `// TODO`) and the `PersonalityDB` attach. Both are small; the "hands" (`Game.OrderHandler.HandleOrder`) and most levers are built, so filling the stub is the unlock.
5. **Officer tenure fields unset** (§5) — fix `CommissionedOn` + add `AssignedOn` before the blend override is meaningful.
6. **Internal-purge facet of Ruthlessness is CUT** — no population-purge/raze-own-colony API exists; revive if ground-combat "suppress a rebelling colony" (#38) lands.

---

## 9. Build order (live-when-wired, one CI-green slice at a time)

1. **`PersonalityDB` + `PersonalityDrift` (tables + math) + `AiDecision.Score*` pure functions** — all CI-testable with no client, no processor wiring. Ship with the §7a per-trait flip tests + the §7c fingerprint tests. **This is the whole demonstration gate, green, before anything touches the live loop.**
2. **`PersonalityReadout` + DevTools/SessionLog hook** (§7b) — observability.
3. **Attach `PersonalityDB`** to factions + officers, default neutral (New Game unchanged), + scenario-JSON parse.
4. **Drift wiring:** `DecayMoods` + officer iteration into `NPCDecisionProcessor.Tick`; `ApplyEvent` call-sites as each event source lands.
5. **Fix the tenure gap** (§5) before blend override.
6. **Decision consumption:** fill `NPCDecisionProcessor.Tick` — score objectives with `EffectiveTrait` + mood suppression (this is the design doc's §6 "Mars attacks Earth" slice-2). Traits are defined + demonstrated in step 1; wired into the live objective loop here.

Then the LATER traits wire in as their systems land: **Guile** with espionage (EMCON down-payment can ship in step 6), **Altruism** with the diplomacy commitment executor (direct-Ledger gift down-payment optional), **Curiosity** with exploration content.

---

*Files this spec touches (all verified): new `People/PersonalityDB.cs`, `Factions/PersonalityDrift.cs`, `Factions/AIScoring.cs`, `Factions/PersonalityReadout.cs`, `Factions/AiDecision.cs`; edits to `Factions/FactionFactory.cs`, `People/CommanderFactory.cs` + `People/CommanderDB.cs` (tenure fields), `Factions/NPCDecisionProcessor.cs:79` (the stub), `Combat/CombatEngagement.cs:46` (the retreat const), and the diplomacy/first-contact/industry decision sites per §1. Companion: `AI-COMMAND-AND-COMMUNICATION-DESIGN.md`.*
