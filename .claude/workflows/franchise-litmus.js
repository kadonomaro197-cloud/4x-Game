export const meta = {
  name: 'franchise-litmus',
  description: 'Franchise battle litmus test — can the Component Designer + Entity Assembler build iconic sci-fi units cradle-to-grave, in detail?',
  whenToUse: 'When stress-testing whether the parametric designer + entity assembler can authentically express famous sci-fi battles (Geonosis / DS9 / Ori Supergate / Cold Steel Ridge). Produces per-battle buildable specs + a master litmus reading + a shared-component build backlog. Read-only w.r.t. engine code — writes design docs only.',
  phases: [
    { title: 'Spec', detail: 'one agent per battle: authentic roster → per-unit cradle-to-grave build spec vs the REAL templates → EXISTS/MISSING/NEEDS ledger + verdict' },
    { title: 'Verify', detail: 'adversarial critic per battle: authenticity + completeness + engine-reality check' },
    { title: 'Synthesize', detail: 'master litmus reading + cross-battle shared-component backlog + does-it-pass verdict' },
  ],
}

// ============================================================================
// COMMON CONTEXT — the ground truth every agent must obey (verified against the
// live base mod 2026-07-21, HEAD 00e0405 on claude/devtest-faction-design-xpfnhe).
// ============================================================================
const COMMON = `
OPERATION: FRANCHISE LITMUS TEST for Pulsar4X (a C# ECS Aurora-4X-like 4X space sim).
GOAL: the developer wants "a few sci-fi battles used as a litmus test" where EVERY unit is
"capable of being created in game using Component Design and Entity Assembler — in detail."
Worked example of the bar: "if a battle involves a Stormtrooper then the E-11 Blaster should
be made correctly as a weapon, along with all applicable capabilities for that single entity."
So the deliverable is NOT lore — it is a per-unit CRADLE-TO-GRAVE BUILD SPEC: the exact
components (each = a template + dial values) + the assembled entity, mapped onto what the
game can ACTUALLY build today, with an honest gap ledger where it cannot.

THE CRADLE-TO-GRAVE CHAIN (the acceptance test for "is this really in the game", from root
CLAUDE.md): mineral (mined) → material (refined) → production (built at a colony/station) →
component (designed in the Component Designer) → gated by research → installed on a chassis
via the Entity Assembler → the in-play decision (the unit fights) → damaged/destroyed (a
component-level loss). A unit "passes" the litmus test only if it can be reached through this
whole chain. A missing rung is a GAP to name (new template / new attribute / a fidelity
compromise), never hand-waved.

═══ THE REAL EXISTS BASELINE (verified — do NOT trust the design docs over this; grep to confirm) ═══
The parametric "11-category designer" (docs/economy/COMPONENT-DESIGNER-CATEGORIES.md) is
DESIGN-LOCKED but NOT BUILT. The running game builds from ~67 hand-authored JSON TEMPLATES in
Pulsar4X/GameData/basemod/TemplateFiles/*.json, assembled via the Entity Assembler
(Pulsar4X.Client ShipDesignWindow — branches Ship/Ground/Station by chassis) and the engine
assembly APIs (GroundUnitAssembly.RegisterAssembledDesign, ShipDesign, StationDesign).

CHASSIS/FRAMES that exist today (grep Pulsar4X/GameData/basemod for the ids):
  human-frame · vehicle-frame · walker-frame · swarm-frame · ship-hull · station-chassis.
COMPONENT TEMPLATES exist for (non-exhaustive — GREP the TemplateFiles + componentDesigns.json):
  Weapons: laser-weapon, railgun-weapon, flak-weapon, disruptor-weapon(anti-shield exotic),
    plasma-repeater, missile-launcher, claw-weapon(melee) — each RICHLY parametric (laser alone
    has ~20 NCalc dials: Range, Target Wavelength, Pulse Energy, Focal Length, ROF, ...).
  Propulsion: conventional-engine, alcubierre-warp-drive, ground locomotion (traction).
  Power: reactor, rtg, steam-turbine-reactor, solarArray, battery-bank.
  Defense: ground-plating, armor (many alloys), deflector-array/shield, armour-hardening,
    bunker, sensor-hardening; cloak-device (EW).
  Sensors: passive-sensor, ground-radar, geo/grav-surveyor, beam-fire-control.
  Logistical: cargo/fuel holds, warehouse, troop-bay (GroundBayAtb — carries ground units!),
    ground-magazine, ordnance hold.
  Enhancers: reflex-booster, power-armor(!), crew-automation, unit-caliber(veteran cadre),
    sealed-systems(environmental sealing — the "space marine sealed suit" component).
  Civic/Command/Industrial: infrastructure, space-habitat, research-lab, admin-complex,
    command-berth/ship-command, mine, factory, refinery, launch-complex, constructor.
MOUNT-FLAGS are REAL: a template's "MountType" is a flags enum (GroundUnit / ShipComponent /
  Fighter / Station / PlanetInstallation / Missile). A component mounts on any chassis its flags
  allow — this is how "universal mounting" already partly works. To make a weapon buildable on a
  Stormtrooper you need it to carry the GroundUnit mount flag (5 direct-fire weapons already do).
FRANCHISE-FLAVOURED CONTENT ALREADY EXISTS: hive-cruiser, spore-lander, claw-weapon,
  cloak-device, crew-automation, walker-frame, swarm-frame — build ON these where they fit.
THE SUPPLY/BUDGET GATE is real and BITES: a chassis has a structural/mass budget + must SUPPLY
  its components (power for energy weapons via reactors; ammo via magazines; crew). An over-armed
  small chassis is correctly refused (GroundUnitAssembly power/ammo gates, ShipDesign mass budget).
KNOWN DESIGN HOLES already catalogued (docs/economy/COMPONENT-DESIGNER-CATEGORIES.md §5): matter
  teleport(H1), innate/pilot powers=People-trait-not-component(H4 — the Force/psionics/Warp powers
  are NOT gear), adaptive/self-repair(H2), modular/separable chassis(H5), self-replication/mobile-
  fab(H3), hybrid dual-role weapons(H7), gate networks(H8→reuse JumpPoints), carrier/nesting(H6),
  conversion/assimilation(H9), crewless/hive-mind(H10→Command shared-span dial), scale extremes(H11),
  exotic power(H12). When a unit hits one of these, CITE the hole # — don't re-derive it.

KEY FILES TO READ (grep/Read as needed — cite file:line, do not assert):
  Pulsar4X/GameData/basemod/TemplateFiles/*.json (the real templates + their dials)
  Pulsar4X/GameData/basemod/ScenarioFiles/designs/componentDesigns.json + shipDesigns.json
  docs/economy/COMPONENT-DESIGNER-CATEGORIES.md (the 11-cat map + the 12 holes)
  docs/economy/UNIVERSAL-ASSEMBLY-DESIGN.md (the governing chassis+components principle)
  Pulsar4X/GameEngine/GroundCombat/CLAUDE.md + GroundUnitAssembly.cs (ground unit assembly)
  Pulsar4X/GameEngine/Ships/ (ShipDesign) + Stations/CLAUDE.md (StationDesign)
  Pulsar4X/Pulsar4X.Client/CLAUDE.md (the Entity Assembler = ShipDesignWindow)

═══ THE PER-UNIT SPEC FORMAT (what you produce for EVERY unit) ═══
For each unit give a compact block:
  UNIT: <name> (<role>, chassis: human/vehicle/walker/swarm/ship/station)
  AUTHENTIC CAPABILITIES (canon, terse): the real weapons/armor/mobility/special that make it IT
    (e.g. E-11: semi-auto plasma-bolt blaster rifle, stun/kill modes, ~short-med range, iron sights).
  BUILD (cradle-to-grave, exact): Chassis <frame> + each component as
    <template-id> {dial: value, ...}  →  what it contributes.  Cite the template that carries it.
  LEDGER: EXISTS (template already covers it, cite id) / MISSING (no template — name the new one
    + its dials, or the fidelity compromise) / NEEDS-CHANGE (exists but needs a mount-flag or dial
    range extension — cite the exact change). Cite a design HOLE # where one applies.
  VERDICT: one of — BUILDABLE-TODAY (stock templates, maybe a mount-flag add) /
    BUILDABLE-WITH-NEW-DATA (needs a new JSON template but no engine code) /
    NEEDS-ENGINE-WORK (needs a new attribute/resolver/mechanic — name it) — with a one-line why.
Be HONEST: the value of a litmus test is exposing what BREAKS, not pretending everything fits.
Prefer "a dial on an existing template" over "a new template" over "engine work" (name the dial
before reaching for a new door — the core designer lesson). The Force / synapse hive-mind /
psionics are People-traits (H4), NOT components — say so, don't fake a component.

OUTPUT DISCIPLINE: plain English + shipboard/mechanical analogies land with this developer
(US-Navy-nuke, hands-on, not a career SWE) — explain jargon on first use. This is READ-ONLY
w.r.t. engine code: you may Read/Grep/Glob and Write ONLY the one design doc you are told to write.
Do NOT edit engine/client/data/test files.
`

// ============================================================================
// THE FOUR BATTLES (each agent gets a starting roster but must expand to full authenticity)
// ============================================================================
const BATTLES = [
  {
    key: 'geonosis',
    title: 'The Battle of Geonosis (Star Wars — the opening battle of the Clone Wars)',
    doc: 'docs/showcase/LITMUS-GEONOSIS.md',
    seed: `Republic (Clones): Clone troopers (Phase I armour, DC-15A/S blaster rifle/carbine), ARC troopers,
Clone commanders, Jedi (LIGHTSABER = Melee/Energy weapon that also DEFLECTS bolts = hole H7; the FORCE =
People trait, hole H4 — NOT a component), AT-TE (six-legged walker, heavy mass-driver projectile cannon +
anti-personnel laser turrets, troop bay), LAAT/i gunship (troop transport + door laser turrets + mass-driver
missiles — a flying APC), SPHA-T (self-propelled heavy artillery, anti-armour beam), TX-130 fighter tank.
Separatist (Droids): B1 battle droid (E-5 blaster, cheap swarm), B2 super battle droid (integral wrist
blaster, armoured), droideka/destroyer droid (twin blasters + a personal deflector SHIELD + rolling mobility),
Hailfire droid (twin missile racks on hoop wheels — a fast missile tank), dwarf/homing spider droid (beam
cannon walker), Geonosian warrior (winged, sonic blaster), Geonosian starfighter, Vulture/tri-fighter droids,
the Core/Trade-Federation ships. Setting: Geonosis arena + desert. Spans EVERY chassis: infantry(human/swarm),
walkers, vehicles, gunships, fighters, capital ships — the widest fidelity probe of the four.`,
  },
  {
    key: 'ds9',
    title: 'The Battle for Deep Space 9 (Star Trek — the Dominion War, "Operation Return"/"Sacrifice of Angels")',
    doc: 'docs/showcase/LITMUS-DS9.md',
    seed: `Federation/Alliance: Galaxy-class, Nebula, Excelsior, Miranda, Akira, Defiant-class (the over-gunned glass
cannon — validates the supply/budget gate) — PHASERS (Energy, wide-arc, continuous beam), PHOTON & QUANTUM
TORPEDOES (Guided), deflector SHIELDS, ablative armour. Klingon: Bird-of-Prey, Vor'cha, Negh'Var — DISRUPTORS
(Energy) + CLOAK (EW, exists as cloak-device) + a de-cloak-to-fire config-state (hole H5). Dominion: Jem'Hadar
attack ship & battlecruiser (phased polaron beam that pierces shields = anti-shield nature like disruptor-weapon;
suicide ramming run = a config/order), Cardassian Galor & Keldon (spiral-wave disruptors), swarm tactics (huge
fleets of small ships). DEEP SPACE 9 ITSELF: a Cardassian station (station-chassis) retrofitted by the Federation
with phaser banks + photon-torpedo launchers + deflector shields — the fortress that must hold. Setting: space
around the station + the Bajoran wormhole (a natural stable wormhole = a JumpPoint, hole H8). Probes: capital
ships, a WEAPONISED STATION, shields-vs-shield-piercing, cloak, and massed small-craft swarms.`,
  },
  {
    key: 'ori-supergate',
    title: 'The Battle at the Ori Supergate (Stargate SG-1 — "The Pegasus Project" / the anti-Ori fleet stand)',
    doc: 'docs/showcase/LITMUS-ORI-SUPERGATE.md',
    seed: `The allied fleet vs the first Ori warships through the Supergate. Tau'ri (Earth): Daedalus/Odyssey-class
BC-304 battlecruisers — rail guns (Ballistic/kinetic), Asgard PLASMA BEAM weapons (very high energy), nuclear
missiles (Guided), SHIELDS, ASGARD BEAMING = matter/personnel TELEPORT (hole H1) + F-302 fighter hangar
(carrier, hole H6). F-302 fighters (railguns + missiles, hyperspace-capable). Allies: Goa'uld/Free-Jaffa Ha'tak
(staff cannon Energy batteries, shields, hyperdrive), Asgard ships (the pinnacle — near-unlimited power H12,
beaming, plasma beams). Ori warship: enormous, a single devastating ENERGY BEAM, shields with NO known weakness
(the deliberately near-unbeatable capital — a great over-power probe), self-repairing (H2). THE SUPERGATE ITSELF:
a giant Stargate assembled in space (from a black hole's matter) that opens a stable intergalactic wormhole —
a buildable, addressable GATE NETWORK NODE (hole H8 → reuse the JumpPoints subsystem) and a strategic objective
(hold/destroy it). Probes: capital ships, teleport-beaming, carriers, gate-network infrastructure, and a
deliberately overpowered enemy (does the budget/supply model let you even EXPRESS an Ori ship, and is it beatable).`,
  },
  {
    key: 'cold-steel-ridge',
    title: 'The Battle of Cold Steel Ridge (Warhammer 40,000 — the Battle for Macragge, Ultramarines vs Hive Fleet Behemoth)',
    doc: 'docs/showcase/LITMUS-COLD-STEEL-RIDGE.md',
    seed: `Ultramarines (Space Marines) + Macragge defenders vs the Tyranids of Hive Fleet Behemoth, on the frozen
polar fortress. Space Marines: POWER ARMOUR (Enhancers ▸ power-armor exists!) + sealed-systems, BOLTER (mass-
reactive .75cal self-propelled rocket rounds = Ballistic/Guided hybrid), plasma gun (Energy, overheats = a risk
dial), chainsword (Melee, exists as claw-weapon-like), Terminators (heavy armour + storm bolter + power fist),
Dreadnought (a walker sarcophagus — walker-frame), Rhino/Razorback (vehicle APC), Predator/Land Raider (heavy
tanks), Whirlwind (missile artillery), Marneus Calgar (a hero — commander/People traits). Imperial Guard support
(lasguns, tanks). Tyranids (the bio-swarm — everything is GROWN, no metal): Termagants/Hormagaunts (swarm-frame,
fleshborer bio-gun / scything claws melee), Genestealers (fast melee infiltrators), Tyranid Warriors (synapse
creatures), Carnifex (a bio-walker heavy), spore mines (cheap floating bombs), the SWARMLORD (a synapse hive-node
— the HIVE MIND is a Command shared-span/hive dial, hole H10, and a People/synapse layer not a component), bio-
ships in orbit (hive-cruiser + spore-lander already exist!). Probes: power-armour elites, the swarm-frame at
scale (a true Tyranid horde), melee-heavy combat, the hive-mind command model, and bio-organic "grown not built"
units (does the mineral→material→built chain even fit a creature that is GROWN — a cradle-to-grave edge case).`,
  },
]

// ============================================================================
// SCRIPT BODY
// ============================================================================
phase('Spec')
log(`FRANCHISE LITMUS TEST — ${BATTLES.length} battles, cradle-to-grave build specs vs the real templates`)

const SUMMARY_SCHEMA = {
  type: 'object',
  additionalProperties: false,
  required: ['battle', 'docPath', 'unitCount', 'buildableToday', 'buildableWithNewData', 'needsEngineWork', 'topGaps', 'sharedComponentsNeeded', 'headline'],
  properties: {
    battle: { type: 'string' },
    docPath: { type: 'string', description: 'the design doc this agent wrote' },
    unitCount: { type: 'integer', description: 'distinct unit types specced' },
    buildableToday: { type: 'integer' },
    buildableWithNewData: { type: 'integer' },
    needsEngineWork: { type: 'integer' },
    topGaps: { type: 'array', items: { type: 'string' }, description: 'the load-bearing gaps (missing templates / engine work), each one line' },
    sharedComponentsNeeded: { type: 'array', items: { type: 'string' }, description: 'new components this battle needs that OTHER battles likely also need (e.g. "troop-transport gunship bay", "plasma-bolt infantry weapon", "capital energy-beam superweapon") — for the cross-battle backlog' },
    headline: { type: 'string', description: 'one-sentence verdict: how well does the game express this battle, and the single biggest gap' },
  },
}

const VERDICT_SCHEMA = {
  type: 'object',
  additionalProperties: false,
  required: ['battle', 'authenticityIssues', 'engineRealityErrors', 'missedUnits', 'verdict'],
  properties: {
    battle: { type: 'string' },
    authenticityIssues: { type: 'array', items: { type: 'string' }, description: 'places the spec got the CANON wrong or hand-waved a signature capability (e.g. "E-11 specced as continuous-beam laser but it fires semi-auto plasma bolts")' },
    engineRealityErrors: { type: 'array', items: { type: 'string' }, description: 'places the spec claims a template/dial/mount exists that does NOT (verified by grep) — cite the false claim' },
    missedUnits: { type: 'array', items: { type: 'string' }, description: 'iconic units of this battle the spec omitted' },
    verdict: { type: 'string', enum: ['SOLID', 'NEEDS-REVISION'], description: 'SOLID if the spec is authentic + engine-grounded + complete; NEEDS-REVISION otherwise' },
  },
}

// PIPELINE: each battle is specced, then adversarially verified as soon as its spec lands
// (no barrier — DS9 verifies while Cold Steel Ridge is still being specced).
const results = await pipeline(
  BATTLES,
  (b) => agent(
    COMMON + `
YOU ARE THE SPEC AGENT for: ${b.title}.
Starting roster (EXPAND to full canonical authenticity — add every iconic unit that fought here;
research the real capabilities; a Stormtrooper-grade "E-11 done correctly" bar applies to EVERY unit):
${b.seed}

Produce the full cradle-to-grave build spec for this battle and WRITE it to ${b.doc} (create the
docs/showcase/ folder if needed). Structure the doc:
  1. The battle in one paragraph (who, where, what makes it iconic) — plain English.
  2. Force composition (both sides, the real order of battle — battalions/squadrons/fleets).
  3. THE UNITS — one per-unit spec block (the format in COMMON) for EVERY distinct unit type,
     grouped by side then by chassis (infantry → vehicles/walkers → air/gunships → ships → station).
     Ground the EXISTS/MISSING/NEEDS ledger in the ACTUAL templates (grep them — cite file:line/id).
  4. The gap ledger: a table of every MISSING template + NEEDS-ENGINE item this battle surfaced,
     each with proposed dials/home and a design-hole # where one applies.
  5. Verdict: how completely can the game stage this battle TODAY, and what's the shortest path to
     "playable" (which gaps are cosmetic vs load-bearing).
Then RETURN the structured summary. Cite the templates you verified. Be honest about what breaks.`,
    { label: `spec:${b.key}`, phase: 'Spec', schema: SUMMARY_SCHEMA },
  ),
  (specSummary, b) => agent(
    COMMON + `
YOU ARE THE ADVERSARIAL VERIFIER for: ${b.title}.
The spec agent wrote ${b.doc} and summarized: ${JSON.stringify(specSummary)}.
READ ${b.doc} in full. Attack it on three axes, CITING evidence:
  (1) AUTHENTICITY — did it get the CANON right, or hand-wave a signature capability? The bar is the
      developer's E-11 example: a signature weapon/ability must be specced as what it ACTUALLY is
      (a bolter fires mass-reactive rocket rounds, not a laser; a droideka's shield is a personal
      deflector, not armour; the Force is a People trait not a component). List every miss.
  (2) ENGINE REALITY — GREP the base mod. Did the spec claim a template/dial/mount-flag that does NOT
      exist, or misread what a template's dials can express? Cite each false claim vs the real id.
  (3) COMPLETENESS — what ICONIC unit of this battle did it omit?
Return the structured verdict. Default to NEEDS-REVISION if any axis has a load-bearing miss.`,
    { label: `verify:${b.key}`, phase: 'Verify', schema: VERDICT_SCHEMA },
  ),
)
// `results` = the per-battle verifier verdicts (pipeline returns the last stage's result per item).
// The spec summaries were logged; the synthesis reads the actual written docs as the source of truth.

phase('Synthesize')
const master = await agent(
  COMMON + `
YOU ARE THE SYNTHESIS AGENT. All four battle specs are written under docs/showcase/ (LITMUS-GEONOSIS.md,
LITMUS-DS9.md, LITMUS-ORI-SUPERGATE.md, LITMUS-COLD-STEEL-RIDGE.md) and each was adversarially verified;
the verifier verdicts were: ${JSON.stringify(results)}.
READ all four docs. Write the MASTER litmus reading to docs/showcase/FRANCHISE-LITMUS-TEST.md:
  1. THE VERDICT up top: does the Component Designer + Entity Assembler PASS the litmus test — can a
     player build these four battles' units in-game, in detail? Give the honest headline (e.g.
     "~X% of all N units are buildable today or with new JSON data; the load-bearing gaps are ...").
  2. THE SCOREBOARD: a table — per battle, unit count / buildable-today / buildable-with-new-data /
     needs-engine-work — and the grand totals.
  3. THE SHARED-COMPONENT BACKLOG (the highest-value section): the NEW components/attributes that
     recur ACROSS battles — build once, unlock many units. Rank by (how many units it unblocks ×
     how cheap it is: dial < new-template < engine-work). Examples to look for: a plasma-BOLT infantry
     weapon (E-11, Jem'Hadar polaron, bolter, staff), a troop-carrying gunship/APC bay (LAAT, Rhino,
     BC-304, Ha'tak), an anti-shield weapon nature (already: disruptor-weapon), a personal-shield
     enhancer (droideka, power-armour), a capital energy-beam superweapon (Ori beam, SPHA-T), the
     hive-mind/synapse Command dial (Tyranids, Borg-like, droid control ship), matter-teleport beaming
     (Asgard, transporter, rings — hole H1), a gate-network node (Supergate, wormhole — hole H8).
  4. THE BUILD PLAN: a phased, gated recommendation — which battle to build FIRST (pick the one that
     exercises the most shared components + is most buildable-today), the shared components to author
     first, and what each phase would gate in CI (a BaseModIntegrityTests-style "every unit builds"
     check). Map each phase to the cradle-to-grave chain.
  5. DESIGNER REPORT CARD: which of the 12 known holes (§5) each battle actually hit, and whether the
     hole is cosmetic-for-these-battles or load-bearing.
Plain English, shipboard analogies. Return a 6-8 sentence executive summary of the whole reading for
the developer (what passes, the top-3 shared components to build, and the recommended first battle).`,
  { label: 'synthesize', phase: 'Synthesize' })

return {
  battles: BATTLES.map(b => b.key),
  verifierVerdicts: results,
  masterDoc: 'docs/showcase/FRANCHISE-LITMUS-TEST.md',
  executiveSummary: master,
}
