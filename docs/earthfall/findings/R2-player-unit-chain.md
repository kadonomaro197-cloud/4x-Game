# R2 — PLAYER GROUND-UNIT (SPACE MARINE) CHAIN — verified step-and-click ledger (condensed)

## VERDICT: the chain is ~90% BUILT. Three verified gaps: sealed component, embark UI, AI formation parity.

## 1. Component dials — ALL EXIST except sealing
ComponentDesignWindow enumerates unlocked templates into Category▸Door tree (ComponentDesignWindow.cs:45,55 → ComponentDoors.Classify). All ground templates in GameData/basemod/TemplateFiles/installations.json, MountType GroundUnit, GuiSelectionMaxMin sliders:
- Frames: human-frame :1994 (vehicle :2547, walker :2696, swarm :3134) — GroundChassisAtb (BaseStrength/BaseHP/Size/Locomotion/CarryClass)
- Weapons: ground-rifle :2073, autocannon :2477, cannon :2626, energy :2775, claw :3213 — GroundWeaponAtb (CarryMass/Attack/Range/Mode); + 7 unified SHIP weapons with GroundUnit mount (weapons.json:29,367,445,533,880,963,1019)
- Armor: ground-plating :2143, ablative :2204, reactive :2301 — GroundArmorAtb (+ VsKinetic/VsEnergy/VsExplosive/VsExotic nature dials)
- Shields: shield-generator :2845; ward-projector :2924 (ShieldRegenFraction dial :2991) — GroundAugmentAtb
- power-armor :2398 (StrengthBonus +300/Evasion/Toughness/Shield) — the marine augment
- Locomotion ground-locomotion :3327; Radar ground-radar :3388 (Range km); Training cadre :3091 (TrainingMultiplier dial :3113); Magazine :3283; Reactors (energy.json:24,157,282 GroundUnit mount)
- **SEALED/environmental component: MISSING** — no template carries an environmental-seal atb; EnvironmentalResistance is design-authored on GroundUnitDesign only, NEVER wired at GroundUnitAssembly.ToGroundUnitDesign; GroundCombat/CLAUDE.md confirms "the next slice". Vacuum/ToxicAtmosphere hazards now exist → this is the one blocking marine gap.

## 2. Entity Assembler — EXISTS (ShipDesignWindow ground branch)
Kind combo ###assembly-kind → Ground Unit (ShipDesignWindow.cs:1051; names :46, GroundKindIndex=1 :47). Parts filter to ComponentMountType.GroundUnit (:1076-1080; ActivePartMount :285); frame locks kind via BudgetKind.Carry (:1042-1044). "+ Add" → SelectedComponents (:1103-1106). Live readout DisplayGroundStats → GroundUnitAssembly.Compute (:417,:438): Carry Budget (:454-457), Attack/Defense/HP/Range/Evasion/Shield/BuildMass/DamageType (:460-467). Gates: carry always-visible; POWER + AMMO only as red "Problems" on violation (:473-485; engine strings GroundUnitAssembly.cs:200/:203). **TrainingMultiplier computed+baked (GroundUnitAssembly.cs:191/:237) but NO readout row — invisible in UI.** Name (:495) → "Save Ground Unit" (:506) → SaveGroundDesign → GroundUnitAssembly.RegisterAssembledDesign (:539; registers on faction.IndustryDesigns, GroundUnitAssembly.cs:269-276).

## 3. Build + field — EXISTS
ColonyManagementWindow Production → IndustryDisplay lists IndustryDesigns filtered by line IndustryTypeID (IndustryDisplay.cs:102-119); GroundUnitDesign.IndustryTypeID = "installation-construction" (GroundUnitAssembly.cs:238). Field: OnConstructionComplete → GroundForces.RaiseUnit. Tests: GroundUnitFieldingTests.cs:36; GroundUnitAssemblyTests (:28,49,64,110,347); GroundUnitDesignerTests.

## 4. Form up — EXISTS for the PLAYER (only)
PlanetViewWindow Formations panel: "Form up N unit(s) in Region" :987 → CreateFormation :991 + AssignUnit loop :993; Disband :1040; leader/rally :1012. Engine: GroundForcesDB.cs CreateFormation :717, AssignUnit :778. **The ONLY non-test CreateFormation caller = the player UI. The AI NEVER forms** (grep ConquerResolver + GroundStartGarrison: zero matches) — garrisons + invaders field LOOSE. Sub-formation nesting engine-only (GroundSubFormationTests:29-40).

## 5. Move — EXISTS (player, full)
PlanetViewWindow: click-to-move global hex → OrderMoveToGlobalHex :455 (whole-cylinder A*); region march OrderMove :852; formation march ◀/▶ → OrderFormationMove :1163; ORDER QUEUE panel DrawOrderQueue — Clear :1061, queue MoveRegion :1074/:1077, +Hold 6h :1082, +ROE :1084/:1086, Shift-click hex = queue MoveHex waypoint. Stance TrySetStance :1146; ROE SetEngagementStance :1106.

## 6. Embark/deploy to another world — engine-only, NO PLAYER UI
LoadTroopsOrder (LoadTroopsOrder.cs:20, CreateCommand :35), LandTroopsOrder, GroundTransport.TryLoadUnit/TryLandUnit, GroundBayAtb, troop-bay component all EXIST + tested (TroopOrderTests.cs:40,72). Full client grep: exactly ONE hit (ComponentDoors.cs:125 label). **Zero windows/buttons issue these** — matches MVP §5 Stage 4 "the missing control panel". AI issues them (ConquerResolver :62/:228).

## MISSING list (marine flow)
1. Sealed/environmental component (blocks surviving vacuum/toxic worlds).
2. Embark/land UI (deploying marines off-world is AI/tests-only).
3. AI formation parity (player can form up; AI cannot — inverse gap).
Flags: TrainingMultiplier invisible in assembler readout; power/ammo gates violation-only.

## Test coverage map
Dials: GroundUnitBaseModTests, GroundScoutPartsBaseModTests, GroundTrainingCadreTests, BaseModIntegrityTests. Assembler: GroundUnitAssemblyTests. Build→field: GroundUnitFieldingTests:36, GroundUnitDesignerTests. Form: GroundForcesTests :707+, GroundSubFormationTests. Move: GroundForcesTests HexPath_*, GroundMobilityTests, GroundLocomotionTests. Embark: TroopOrderTests, GroundTransportTests. AI formation: NO test (feature absent).
