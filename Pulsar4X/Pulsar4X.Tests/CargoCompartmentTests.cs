using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Pulsar4X.Blueprints;
using Pulsar4X.Colonies;
using Pulsar4X.Components;
using Pulsar4X.Factions;
using Pulsar4X.Storage;
using Pulsar4X.Technology;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// WHAT A SHIP CAN CARRY — the Logistical door's first door, and the three classes that were declared and could
    /// not be built (docs/economy/DESIGNER-NORTH-STAR.md §46).
    ///
    /// <para><b>What a cargo is.</b> Anything implementing <see cref="ICargoable"/> declares a
    /// <c>CargoTypeID</c> — the KIND of compartment it needs. A hold declares the same string through
    /// <see cref="CargoStorageAtb"/>, and <c>CargoMath.GetFreeVolume</c> looks the item's string up in the hold's
    /// <c>TypeStores</c> dictionary. <b>Miss, and it returns 0 — no exception, no log, nothing.</b> A silent zero is
    /// the worst failure shape in the codebase, and it is what these tests exist to make loud.</para>
    ///
    /// <para><b>The bug this fixture pins.</b> Six cargo classes were declared in <c>cargoTypes.json</c>. Only three
    /// had any component that provided them. <b><c>passenger-storage</c> and <c>cryogenic-storage</c> were declared
    /// and provided by nothing at all</b> — while <see cref="TeamObject"/> (a research team; every
    /// <c>Scientist</c> is one) has always declared <c>CargoTypeID = "passenger-storage"</c>. So a research team
    /// could never be loaded onto anything, and asking how much room there was for one answered <b>0</b> forever.
    /// The producer was missing and the consumer had shipped — the same shape as <c>EmploymentAtbDB</c> (§50) and
    /// <c>LogiBaseAtb</c> (§46), and the reason §51 asks for one data-only test over all of them.</para>
    ///
    /// <para><b>What landed:</b> four compartment templates (<c>passenger-cabin</c>, <c>cryo-bay</c>,
    /// <c>refrigerated-hold</c>, <c>containment-hold</c>), the two classes the taxonomy still lacked
    /// (<c>perishable-storage</c> for food and anything alive, <c>contained-storage</c> for cargo that is dangerous to
    /// its own carrier), and a corrected figure for how much room a person takes up.</para>
    /// </summary>
    [TestFixture]
    public class CargoCompartmentTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[cargo] " + m);

        /// <summary>Pulls the cargo type out of an <c>AtbConstrArgs('the-type', …)</c> formula. Also accepts the
        /// <c>UniqueID('the-type')</c> spelling, which <c>installations.json</c> uses for the same thing (NCalc's
        /// <c>UniqueID</c> just returns its argument — see <c>ChainedExpression.cs:513</c>).</summary>
        private static readonly Regex StoreTypeInFormula =
            new Regex(@"AtbConstrArgs\(\s*(?:UniqueID\(\s*)?'([a-zA-Z0-9-]+)'", RegexOptions.Compiled);

        private const string CargoStorageAtbFqn = "Pulsar4X.Storage.CargoStorageAtb";

        /// <summary>Every component template in the mod, unlocked or not — a class provided only by a locked template
        /// is still provided, so the structural rule must see the whole set.</summary>
        private static Dictionary<string, ComponentTemplateBlueprint> AllTemplates(TestScenario s)
        {
            var data = s.Faction.GetDataBlob<FactionInfoDB>().Data;
            var all = new Dictionary<string, ComponentTemplateBlueprint>();
            foreach (var kv in data.LockedComponentTemplates) all[kv.Key] = kv.Value;
            foreach (var kv in data.ComponentTemplates) all[kv.Key] = kv.Value;   // unlocked wins, same object anyway
            return all;
        }

        /// <summary>cargo class → the template ids that provide a compartment of that class.</summary>
        private static Dictionary<string, List<string>> ProviderMap(TestScenario s)
        {
            var map = new Dictionary<string, List<string>>();
            foreach (var kv in AllTemplates(s))
            {
                foreach (var prop in kv.Value.Properties ?? new List<ComponentTemplatePropertyBlueprint>())
                {
                    if (prop.AttributeType != CargoStorageAtbFqn) continue;
                    var m = StoreTypeInFormula.Match(prop.PropertyFormula ?? "");
                    if (!m.Success) continue;
                    if (!map.TryGetValue(m.Groups[1].Value, out var list))
                        map[m.Groups[1].Value] = list = new List<string>();
                    list.Add(kv.Key);
                }
            }
            return map;
        }

        /// <summary>
        /// 🔒 THE STRUCTURAL RULE, and the one that cannot rot: <b>a declared cargo class must be providable by
        /// something.</b> A class with no compartment is a promise the designer cannot keep — and it fails as a silent
        /// 0 out of <c>CargoMath</c>, so nothing else in the game will ever tell you.
        ///
        /// <para>The allow-list carries a REASON per entry, which is the §51 discipline: <i>"it is stored by another
        /// system"</i> is a valid reason; <i>"nobody got round to it"</i> is not, and is what this test is for.</para>
        /// </summary>
        [Test]
        [Description("Every cargo class declared in cargoTypes.json is provided by at least one component template. passenger-storage and cryogenic-storage were declared and provided by NOTHING, so a research team — which has always asked for passenger-storage — could never be loaded onto anything, and the failure was a silent 0 from CargoMath rather than an error.")]
        public void EveryDeclaredCargoClass_IsProvidedBySomething()
        {
            var s = TestScenario.CreateWithColony();
            var data = s.Faction.GetDataBlob<FactionInfoDB>().Data;

            // Declared classes = unlocked + locked, because declaring one is a mod-wide statement.
            var declared = new SortedSet<string>(data.CargoTypes.Keys);
            foreach (var k in data.LockedCargoTypes.Keys) declared.Add(k);

            var providers = ProviderMap(s);

            // Allow-list. Each entry states WHY it needs no CargoStorageAtb provider.
            var allowed = new Dictionary<string, string>
            {
                // Energy is held by EnergyStoreAtb (battery-bank), a separate mechanism that predates cargo types.
                // Whether energy SHOULD be shippable cargo (power cells, charged capacitors — a real science-fiction
                // trade good) is an open developer ruling, recorded in §46. Until it is answered this class is
                // vestigial, not broken.
                ["battery-storage"] = "energy is stored by EnergyStoreAtb, not by a cargo compartment (open ruling: should power be shippable?)",
            };

            Log($"declared classes: {string.Join(", ", declared)}");
            foreach (var c in declared)
                Log($"  {c,-20} providers: {(providers.ContainsKey(c) ? string.Join(", ", providers[c]) : "🔴 NONE")}");

            var orphans = declared.Where(c => !providers.ContainsKey(c) && !allowed.ContainsKey(c)).ToList();
            Assert.That(orphans, Is.Empty,
                "these cargo classes are declared and NOTHING can carry them, which fails as a silent 0 from "
                + "CargoMath.GetFreeVolume rather than an error: " + string.Join(", ", orphans));

            // …and the four classes this slice is about are each provided by a named template, not by accident.
            foreach (var (cls, tpl) in new[]
                     {
                         ("passenger-storage",  "passenger-cabin"),
                         ("cryogenic-storage",  "cryo-bay"),
                         ("perishable-storage", "refrigerated-hold"),
                         ("contained-storage",  "containment-hold"),
                     })
            {
                Assert.That(providers.ContainsKey(cls), Is.True, $"{cls} must have a provider");
                Assert.That(providers[cls], Does.Contain(tpl), $"{cls} is provided by {tpl}");
            }
        }

        /// <summary>
        /// ⚠ THE MIRROR OF THE TEST ABOVE, AND THE ONE I FAILED TO WRITE FIRST.
        ///
        /// <para><c>EveryDeclaredCargoClass_IsProvidedBySomething</c> walks <b>classes → providers</b>. That is only half
        /// the joint. This walks <b>GOODS → providers</b>: every <c>Mineral</c> and <c>ProcessedMaterial</c> declares a
        /// <c>CargoTypeID</c>, and if nothing provides that compartment then <b>the good cannot be stored or shipped by
        /// anything in the game</b> — reported, as always, as a silent 0 out of <c>CargoMath</c>.</para>
        ///
        /// <para><b>The first run found two, and my own allow-list had excused them.</b> <c>electricity</c> and
        /// <c>lithium-battery</c> both declared <c>battery-storage</c>, which nothing provides — and I had allow-listed
        /// that class on the grounds that "energy lives in <c>EnergyStoreAtb</c>". That excuse holds for
        /// <c>electricity</c> (a charge) and is <b>plainly wrong for <c>lithium-battery</c></b>, which is a
        /// <b>manufactured object</b>: you can build one and then have nowhere to put it. <b>An allow-list entry that
        /// reasons about the CLASS can hide a bug about a GOOD</b>, which is exactly why both directions need walking.</para>
        ///
        /// <para><c>lithium-battery</c> is now <c>general-storage</c>. <c>electricity</c> stays allow-listed as the one
        /// genuine open ruling: <i>should power be shippable cargo?</i> Wiring it through
        /// <c>CargoStorageAtb</c> would give the game a second way to hold a charge, and a fifth parallel store is not a
        /// fix (§46a).</para>
        /// </summary>
        [Test]
        [Description("Every shippable good — mineral or refined material — names a compartment that some component actually provides. This found two goods no compartment could hold: electricity and lithium-battery, the latter a manufactured object you could build and then not store anywhere. It is the mirror of the class-side test, and the class-side allow-list had hidden it.")]
        public void EveryGood_NamesACompartmentSomethingProvides()
        {
            var s = TestScenario.CreateWithColony();
            var data = s.Faction.GetDataBlob<FactionInfoDB>().Data;
            var providers = ProviderMap(s);

            // Allow-list, one reason per entry — the §51 discipline.
            var allowed = new Dictionary<string, string>
            {
                // A charge, not a mass. Held by EnergyStoreAtb / EnergyGenAbilityDB.EnergyStored. Making it a cargo
                // compartment too would be a FIFTH parallel store, so this waits on a developer ruling.
                ["battery-storage"] = "electricity is a charge held by EnergyStoreAtb (open ruling: should power be shippable?)",
            };

            var offenders = new List<string>();
            var byClass = new SortedDictionary<string, List<string>>();
            // CargoGoods has no combined getter, so filter GetAll() to the two kinds that are SHIPPED
            // (the "other cargo" bucket holds things that are not trade goods).
            foreach (var good in data.CargoGoods.GetAll().Values
                         .Where(g => data.CargoGoods.IsMineral(g.ID) || data.CargoGoods.IsMaterial(g.ID))
                         .OrderBy(g => g.UniqueID))
            {
                string cls = good.CargoTypeID ?? "(null)";
                if (!byClass.TryGetValue(cls, out var l)) byClass[cls] = l = new List<string>();
                l.Add(good.UniqueID);
                if (!providers.ContainsKey(cls) && !allowed.ContainsKey(cls))
                    offenders.Add($"{good.UniqueID} → {cls}");
            }

            int total = byClass.Sum(kv => kv.Value.Count);
            Log($"{total} shippable goods, by compartment:");
            foreach (var kv in byClass)
                Log($"  {kv.Key,-20} {kv.Value.Count,3} goods ({100.0 * kv.Value.Count / total:0}%)"
                    + $" {(providers.ContainsKey(kv.Key) ? "" : allowed.ContainsKey(kv.Key) ? "⚠ allow-listed" : "🔴 NO PROVIDER")}");

            Assert.That(offenders, Is.Empty,
                "these goods name a compartment nothing provides, so they cannot be stored or shipped at all — and it "
                + "fails as a silent 0 from CargoMath rather than an error: " + string.Join(", ", offenders));

            // lithium-battery specifically: a manufactured OBJECT, so it belongs in a box that exists.
            var battery = data.CargoGoods.GetAny("lithium-battery");
            Assert.That(battery, Is.Not.Null, "the base mod defines lithium-battery");
            Assert.That(battery.CargoTypeID, Is.EqualTo("general-storage"),
                "a manufactured battery unit is crated like any other product — it was in battery-storage, which nothing provides");
        }

        /// <summary>
        /// 🔒 EVERY AUTHORED COLONY, NOT JUST EARTH — the data-only gauge that closes the hole the
        /// Earth-shaped one left, and it exists because that hole cost a red CI run.
        ///
        /// <para><b>The mistake it prevents, stated plainly:</b> the sibling test below asserts the start colony can
        /// hold what it mines, and I reported its blast radius as <em>"Earth is the only colony blueprint in the base
        /// mod"</em>. <b>That was wrong.</b> Earth is the only file spelling the key <c>StartingItems</c>; the three
        /// NPC faction files spell it <c>startingItems</c> and carry <b>five more colonies between them</b>
        /// (uef-devtest ×1, umf ×4). A grep for the exact PascalCase string found one file and I read that as
        /// "one colony". <b>A blast-radius check that keys on spelling is not a blast-radius check.</b></para>
        ///
        /// <para>Two real regressions got through on that reading: <c>battery-bank</c> was re-costed to
        /// <c>lithium-battery</c> while three factions unlocked the bank and not the cell (a hard
        /// <c>resourceCosting</c> throw on New Game — it red-lit three CI shards), and UMF's <b>Venus</b> starts with
        /// 3,000 <c>hydrocarbons</c>, which had just been moved from dry bulk to <c>fuel-storage</c> — a class Venus
        /// provided none of, so its opening stockpile had nowhere to go.</para>
        ///
        /// <para><b>This walks the JSON directly</b> — every scenario file, every colony in it, accepting either
        /// spelling — and asserts two things per colony: every good in its opening <c>cargo</c> has a compartment among
        /// its <c>installations</c> that can hold that cargo class, and every id it references is defined. No running
        /// game, so it is fast and it covers the NPC factions the scenario harness never builds.</para>
        /// </summary>
        [Test]
        [Description("Every colony in every scenario file — not just Earth — can physically hold the cargo it starts with. Walks the JSON directly, accepting both the StartingItems and startingItems spellings, because keying a blast-radius check on one spelling is what let two regressions through: a battery bank re-costed to a cell three factions had not unlocked, and a Venus stockpile of hydrocarbons after that good moved to fuel storage.")]
        public void EveryAuthoredColony_CanHoldTheCargoItStartsWith()
        {
            var s = TestScenario.CreateWithColony();
            var data = s.Faction.GetDataBlob<FactionInfoDB>().Data;
            var templates = AllTemplates(s);

            string root = System.IO.Path.Combine("Data", "basemod", "ScenarioFiles");
            Assert.That(System.IO.Directory.Exists(root), Is.True,
                $"scenario files are laid down next to the tests ({root})");

            // ── good id → the cargo class it needs ───────────────────────────────────────────────
            // Minerals and materials come from the faction store. COMPONENT DESIGNS are cargo too
            // (a crated part rides in a hold — uef.json ships 5 'default-design-merlin'), and their
            // class lives on the TEMPLATE, so they are resolved separately. Reading the raw designs
            // file rather than the faction's ComponentDesigns is deliberate: the faction store holds
            // only what THIS faction unlocked, and this gauge must judge NPC colonies too.
            var classOf = new Dictionary<string, string>();
            foreach (var g in data.CargoGoods.GetAll().Values) classOf[g.UniqueID] = g.CargoTypeID;

            var providesOf = new Dictionary<string, List<string>>();
            string designsFile = System.IO.Path.Combine(root, "designs", "componentDesigns.json");
            foreach (var e in Newtonsoft.Json.Linq.JArray.Parse(System.IO.File.ReadAllText(designsFile)))
            {
                var pay = e["Payload"]; if (pay == null) continue;
                string did = (string)pay["UniqueId"], tid = (string)pay["TemplateId"];
                if (did == null || tid == null || !templates.TryGetValue(tid, out var t)) continue;

                if (!string.IsNullOrEmpty(t.CargoTypeID)) classOf[did] = t.CargoTypeID;   // crated part as cargo

                foreach (var prop in t.Properties ?? new List<ComponentTemplatePropertyBlueprint>())
                {
                    if (!(prop.AttributeType ?? "").Contains("CargoStorageAtb")) continue;
                    // AtbConstrArgs('general-storage', …)  and  AtbConstrArgs(UniqueID('fuel-storage'), …)
                    var m = Regex.Match(prop.PropertyFormula ?? "", @"'([\w-]+)'");
                    if (!m.Success) continue;
                    if (!providesOf.TryGetValue(did, out var l)) providesOf[did] = l = new List<string>();
                    l.Add(m.Groups[1].Value);
                }
            }

            // ── files that are NOT live, each with its reason (the §46f allow-list idiom) ────────
            var superseded = new Dictionary<string, string>
            {
                ["uef.json"] = "SUPERSEDED and dead — nothing loads it. The DevTest start names its three files "
                             + "explicitly (NewGameMenu.cs:955 → uef-devtest.json · umf.json · kithrin.json) and this "
                             + "is not one of them; it also still uses the old file-path design format "
                             + "('componentDesigns/cargoHold-1t.json') rather than design ids. Left unpatched on "
                             + "purpose: fixing dead data hides the fact that it is dead.",
            };

            int checkedColonies = 0, skipped = 0;
            var problems = new List<string>();

            foreach (var file in System.IO.Directory.GetFiles(root, "*.json", System.IO.SearchOption.AllDirectories))
            {
                string name = System.IO.Path.GetFileName(file);
                Newtonsoft.Json.Linq.JObject doc;
                try { doc = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(file)); }
                catch { continue; }                                    // not an object-shaped scenario file
                if (!(doc["colonies"] is Newtonsoft.Json.Linq.JArray colonies)) continue;

                if (superseded.TryGetValue(name, out var why))
                { skipped += colonies.Count; Log($"  ⏭ {name} skipped — {why}"); continue; }

                foreach (var col in colonies)
                {
                    string where = $"{name}/{col["location"]}";
                    checkedColonies++;

                    var provided = new HashSet<string>();
                    foreach (var inst in (col["installations"] as Newtonsoft.Json.Linq.JArray)
                                         ?? new Newtonsoft.Json.Linq.JArray())
                    {
                        string id = (string)(inst["id"] ?? inst);
                        if (id != null && providesOf.TryGetValue(id, out var cls))
                            foreach (var c in cls) provided.Add(c);
                    }

                    foreach (var item in (col["cargo"] as Newtonsoft.Json.Linq.JArray)
                                         ?? new Newtonsoft.Json.Linq.JArray())
                    {
                        string id = (string)item["id"];
                        if (id == null) continue;
                        if (!classOf.TryGetValue(id, out var need))
                        { problems.Add($"{where}: starting cargo '{id}' is not a defined good or design"); continue; }
                        if (!provided.Contains(need))
                            problems.Add($"{where}: starts with '{id}' (needs {need}) but installs nothing providing {need}"
                                         + $" — it provides [{string.Join(", ", provided.OrderBy(x => x))}]");
                    }
                    Log($"  {where,-30} provides [{string.Join(", ", provided.OrderBy(x => x))}]");
                }
            }

            Log($"checked {checkedColonies} live authored colonies ({skipped} skipped as superseded)");
            Assert.That(checkedColonies, Is.GreaterThan(1),
                "🔒 this gauge exists BECAUSE \"Earth is the only colony blueprint\" was wrong. If it ever finds one "
                + "colony again, the WALK has broken — not the data.");
            Assert.That(problems, Is.Empty,
                "a colony cannot physically hold the cargo it is authored to start with:\n  "
                + string.Join("\n  ", problems));
        }

        /// <summary>
        /// 🔒 CAN THIS HOST HOLD WHAT IT MINES? — the gauge that had to exist before any good could be moved
        /// between compartments, and the reason the reclassification waited a slice.
        ///
        /// <para><b>The failure it guards is silent, and the repo has been bitten by this exact shape before.</b>
        /// <c>MineResourcesProcessor</c> does <c>stockpile.AddCargoByUnit(mineral, minable)</c> and then subtracts
        /// <b>only what actually fitted</b> from the deposit. So a colony with nowhere to put a mineral does not lose
        /// ore — <b>it silently stops mining it</b>, with no error and no log, which reads exactly like "the mine does
        /// nothing" (the same signature as the Stasis bug). Moving a mined good to a compartment the colony does not
        /// have would have caused precisely that.</para>
        ///
        /// <para>So: <b>every mineral this world actually has must have a compartment with room on the colony</b>, and
        /// every good the colony starts with must really be in store rather than quietly dropped at load.</para>
        /// </summary>
        [Test]
        [Description("Every mineral the starting world holds has a compartment with free room on the colony, and every good in the starting stockpile is genuinely stored. Without this, moving a mined good to a compartment the colony lacks makes it silently stop mining that mineral — no error, no log, just a mine that appears to do nothing.")]
        public void EveryMineralThisWorldHas_HasSomewhereToGoOnTheColony()
        {
            var s = TestScenario.CreateWithColony();
            var data = s.Faction.GetDataBlob<FactionInfoDB>().Data;
            var hold = s.Colony.GetDataBlob<CargoStorageDB>();
            Assert.That(hold, Is.Not.Null, "the start colony has cargo storage");

            Log("compartments on the start colony: " + string.Join(", ",
                hold.TypeStores.Select(kv => $"{kv.Key} {kv.Value.MaxVolume:N0} m³")));

            var body = s.Colony.GetDataBlob<Pulsar4X.Colonies.ColonyInfoDB>().PlanetEntity;
            Assert.That(body.TryGetDataBlob<Pulsar4X.Industry.MineralsDB>(out var deposits), Is.True,
                "the starting world has mineral deposits");

            var homeless = new List<string>();
            foreach (var kv in deposits.Minerals)
            {
                var good = data.CargoGoods.GetAny(kv.Key);
                if (good == null) continue;                       // a deposit of something this faction cannot see
                double free = hold.GetFreeVolume(good);
                Log($"  {good.UniqueID,-24} {good.CargoTypeID,-20} free {free,12:N0} m³");
                if (free <= 0) homeless.Add($"{good.UniqueID} needs {good.CargoTypeID}");
            }

            Assert.That(homeless, Is.Empty,
                "these minerals are in the ground under the colony and it has nowhere to put them, so mining them "
                + "silently does nothing: " + string.Join(", ", homeless));

            // …and nothing in the starting stockpile was quietly dropped on load for the same reason.
            foreach (var id in new[] { "water", "hydrocarbons", "fissionables", "iron", "rp-1" })
            {
                var good = data.CargoGoods.GetAny(id);
                Assert.That(good, Is.Not.Null, $"{id} is a base-mod good");
                long stored = hold.GetUnitsStored(good, false);
                Log($"  stockpile {good.UniqueID,-18} {good.CargoTypeID,-20} {stored,12:N0} units");
                Assert.That(stored, Is.GreaterThan(0),
                    $"{id} is in the colony's starting Cargo list, so it must actually be in store — a 0 here means it "
                    + "was silently dropped because no compartment accepts it");
            }
        }

        /// <summary>
        /// 🔒 EVERY RESOURCE MUST JUSTIFY ITS EXISTENCE — the developer's rule, 2026-07-30, and the goods-side twin of
        /// §39.8 (*every option behind a door must win an axis outright, or it is clutter*).
        ///
        /// <para><b>For a GOOD the three tests are:</b> something <b>produces</b> it (mined from a deposit, or refined
        /// by a recipe — true of all 38 by construction), something <b>consumes</b> it, and it is
        /// <b>distinguishable</b> from its neighbours. This test enforces the middle one, which is the one that fails:
        /// a good nothing wants is a refining job you can queue forever for no reason.</para>
        ///
        /// <para><b>A good is CONSUMED if it is</b> an input to another material's recipe · a build cost on a component
        /// template · the <c>ResourceID</c> of an armour · a fuel an engine names in a formula · or read by ENGINE CODE,
        /// which a data scan cannot see and which the allow-list must therefore name explicitly.</para>
        ///
        /// <para>⚠ <b>That last clause is not hypothetical — it is the correction this test was born from.</b> A first
        /// pass that scanned only <c>ResourceCost</c> reported <b>14 dead goods</b>. Adding armour's <c>ResourceID</c>
        /// and formula references dropped it to <b>5</b>, because six of the "dead" were armour materials referenced by
        /// a field the scan never looked at. <b>An audit is only as good as the reference forms it knows about</b>, so
        /// this one enumerates them and the allow-list carries the rest with a stated reason.</para>
        /// </summary>
        [Test]
        [Description("Every refined material and mineral is consumed by something — another recipe, a component's build cost, an armour, an engine's fuel, or a named consumer in engine code. A good nothing wants is a refining job you can queue forever for no reason. The allow-list must name a real consumer, because a data-only scan cannot see one written in C#.")]
        public void EveryResource_IsConsumedBySomething()
        {
            var s = TestScenario.CreateWithColony();
            var data = s.Faction.GetDataBlob<FactionInfoDB>().Data;

            var consumers = new Dictionary<string, List<string>>();
            void Consume(string good, string by)
            {
                if (string.IsNullOrEmpty(good)) return;
                if (!consumers.TryGetValue(good, out var l)) consumers[good] = l = new List<string>();
                l.Add(by);
            }

            // ① another material's recipe, and ② a component template's build cost + any fuel it names in a formula
            foreach (var m in data.CargoGoods.GetAll().Values)
                if (m is Pulsar4X.Industry.ProcessedMaterial pm && pm.ResourceCosts != null)
                    foreach (var inp in pm.ResourceCosts.Keys) Consume(inp, "recipe:" + pm.UniqueID);

            foreach (var kv in AllTemplates(s))
            {
                foreach (var inp in kv.Value.ResourceCost?.Keys ?? Enumerable.Empty<string>())
                    Consume(inp, "build:" + kv.Key);
                foreach (var prop in kv.Value.Properties ?? new List<ComponentTemplatePropertyBlueprint>())
                {
                    foreach (Match m in Regex.Matches(prop.PropertyFormula ?? "", @"UniqueID\('([\w-]+)'\)"))
                        Consume(m.Groups[1].Value, "fuel/formula:" + kv.Key);

                    // ⑤ 🔑 THE FIFTH REFERENCE FORM — and this gauge went RED on its very first CI run for
                    // missing it, which is the §46f lesson repeating one iteration later.
                    //
                    // A fuel dial does NOT name every fuel it can burn. `GuiFuelTypeSelection` names only the
                    // engine's DEFAULT (`UniqueID('rp-1')`), and the OTHER selectable fuels are found by FILTER:
                    // `ComponentDesignDisplay.GetFuelTypes` walks the property's DataDict, reads each KEY as a
                    // CARGO CLASS and each VALUE as a fuel-type tag, and offers every material in that class whose
                    // `Formulas["FuelType"]` matches. So `methalox` and `hydrolox` are perfectly buildable and
                    // burnable — they are simply never NAMED anywhere, and a scan that only reads formulas
                    // pronounced them dead goods.
                    //
                    // 🔒 The rule, stated for the third time in this campaign: AN AUDIT IS ONLY AS GOOD AS THE
                    // REFERENCE FORMS IT KNOWS ABOUT. First it was armour's `ResourceID`; now it is the fuel
                    // dial's filter. When this gauge flags something, check how the good is REACHED before
                    // concluding nothing reaches it.
                    if (prop.DataDict == null) continue;
                    foreach (var dd in prop.DataDict)
                    {
                        string cargoClass = dd.Key;
                        string fuelTag = (dd.Value ?? "").Trim().Trim('\'', '"');
                        if (fuelTag.Length == 0) continue;
                        foreach (var good in data.CargoGoods.GetAll().Values)
                            if (good is Pulsar4X.Industry.ProcessedMaterial fm
                                && fm.CargoTypeID == cargoClass
                                && fm.Formulas != null
                                && fm.Formulas.TryGetValue("FuelType", out var ft)
                                && ft == fuelTag)
                                Consume(fm.UniqueID, "fuel/dial-filter:" + kv.Key);
                    }
                }
            }

            // ③ armour — the reference form the first pass MISSED, which is why six goods looked dead
            foreach (var a in data.Armor.Values) Consume(a.ResourceID, "armour:" + a.UniqueID);
            foreach (var a in data.LockedArmor.Values) Consume(a.ResourceID, "armour:" + a.UniqueID);

            // ④ consumers written in C#, which no data scan can see. Each entry names the consumer.
            var codeConsumers = new Dictionary<string, string>
            {
                ["food"] = "SustenanceProcessor.DrawStoredFood — the population eats it",
                ["electricity"] = "EnergyGenAbilityDB.EnergyType / EnergyStoreAtb — the charge itself",
            };
            foreach (var kv in codeConsumers) Consume(kv.Key, "code:" + kv.Value);

            // ⑤ authored but not yet wired. Each MUST say what it is for — "nobody got round to it" is not a reason,
            //    and an entry here is a standing invitation to either wire it or delete it.
            var awaitingAMechanic = new Dictionary<string, string>
            {
                ["stainless-steel-d"] = "MATERIAL GRADE ladder (cheap): iron+nickel, no chromium, credit 12 vs the standard 25 — needs a build-with-grade mechanic, the structural twin of the WIRED fuel-grade system",
                ["stainless-steel-a"] = "MATERIAL GRADE ladder (premium): alloyed with titanium, credit 80 — same missing mechanic",
                ["electronics-d"]     = "MATERIAL GRADE ladder (cheap): no aluminium, credit 80 vs the standard 250 — same missing mechanic",
                ["electronics-a"]     = "MATERIAL GRADE ladder (premium): incorporates ree-magnetics, credit 1000 — same missing mechanic",
            };

            var goods = data.CargoGoods.GetAll().Values
                .Where(g => data.CargoGoods.IsMineral(g.ID) || data.CargoGoods.IsMaterial(g.ID))
                .OrderBy(g => g.UniqueID).ToList();

            var unjustified = new List<string>();
            Log($"{goods.Count} goods — who wants each one:");
            foreach (var g in goods)
            {
                var c = consumers.TryGetValue(g.UniqueID, out var l) ? l : new List<string>();
                string note = awaitingAMechanic.TryGetValue(g.UniqueID, out var why) ? "⚠ " + why : "";
                Log($"  {g.UniqueID,-26} {c.Count,3} consumer(s) {(c.Count > 0 ? string.Join(", ", c.Distinct().Take(3)) : note)}");
                if (c.Count == 0 && !awaitingAMechanic.ContainsKey(g.UniqueID)) unjustified.Add(g.UniqueID);
            }

            Assert.That(unjustified, Is.Empty,
                "nothing in the game wants these, so refining them is a job you can queue forever for no reason — "
                + "wire a consumer, add them to the awaiting-a-mechanic list with a STATED purpose, or delete them: "
                + string.Join(", ", unjustified));

            // The four on the waiting list are a real ladder, not four reskins: each tier must differ in what it
            // costs to make, or "premium steel" is just steel with a different name.
            foreach (var (cheap, std, prem) in new[]
                     {
                         ("stainless-steel-d", "stainless-steel", "stainless-steel-a"),
                         ("electronics-d",     "electronics",     "electronics-a"),
                     })
            {
                // ⚠ The grade ladder is DELIBERATELY unwired — the four grade materials sit on the awaitingAMechanic
                // list above precisely BECAUSE no build-with-grade mechanic exists to unlock them, so they are in no
                // colony's StartingItems and never unlock. A faction's `CargoGoods` starts EMPTY and only holds
                // UNLOCKED goods (Unlock moves them out of `LockedCargoGoods` — FactionDataStore.cs:41/43/98-99), so
                // a locked grade material reads null from `CargoGoods.GetAny` even though it is a perfectly good
                // authored blueprint. Look it up in the unlocked store OR the locked one, so the ladder-SHAPE checks
                // below run against the authored data regardless of unlock state. UNLOCKING these would create real
                // refining jobs "you can queue forever for no reason" — the exact thing this whole audit condemns.
                Pulsar4X.Industry.ProcessedMaterial Mat(string id) =>
                    (data.CargoGoods.GetAny(id) ?? data.LockedCargoGoods.GetAny(id)) as Pulsar4X.Industry.ProcessedMaterial;
                var c = Mat(cheap);
                var m = Mat(std);
                var p = Mat(prem);
                Assert.That(c, Is.Not.Null); Assert.That(m, Is.Not.Null); Assert.That(p, Is.Not.Null);
                Log($"  grade ladder {cheap} {c.CreditValue} < {std} {m.CreditValue} < {prem} {p.CreditValue}");
                Assert.That(c.CreditValue, Is.LessThan(m.CreditValue),
                    $"{cheap} must be cheaper than {std}, or the ladder has no bottom rung");
                Assert.That(p.CreditValue, Is.GreaterThan(m.CreditValue),
                    $"{prem} must be dearer than {std}, or the ladder has no top rung");
                Assert.That(p.ResourceCosts, Is.Not.EqualTo(m.ResourceCosts),
                    $"{prem} must be made of something different from {std}, or it is a reskin");
            }
        }

        /// <summary>
        /// 🔑 ANTIMATTER — the first item from the taxonomy that did not exist, worked in cradle-to-grave (§46g).
        ///
        /// <para><b>This is what "justified in game" has to mean.</b> Not a label on a list: a good with a
        /// <b>producer</b> (refined from fissionables at ruinous industry cost), a <b>consumer</b> (an engine that
        /// burns it), a <b>place in an existing trade</b> (it continues the shipped fuel ladder rather than sitting
        /// beside it), and a <b>reason the compartment matters</b>.</para>
        ///
        /// <para><b>The ladder it joins</b> — exhaust velocity UP, fuel grade DOWN, so a better fuel goes further per
        /// kilogram and pushes <em>softer</em>. That trade is the §26.3 ruling that stopped fuel being a pure
        /// dominance ladder, and antimatter had to obey it or it would be a strictly-better fuel:</para>
        /// <code>rp-1 3510/1.15 → hydrolox 4462/0.85 → ntp 7000/0.75 → antimatter 60000/0.05</code>
        ///
        /// <para>⚠ <b>The grade started at 0.35 and that was WRONG — and the first version of this test PASSED it.</b>
        /// The old assertion was <c>Grade(antimatter) &lt; Grade(ntp)</c>, which is worthless: <b>thrust = exhaust
        /// velocity × mass flow, and mass flow scales with grade</b>, so an 8.6× rise in exhaust velocity swamps a
        /// 2.1× fall in grade. At 0.35 antimatter had <b>4× the nuclear drive's thrust AND 8.6× its range</b> —
        /// strictly better on every axis but price, which is exactly the dominance §26.3 exists to remove.
        /// <b>Assert the PRODUCT the sim computes, not the input dial</b>: both templates share the same
        /// <c>[Mass] * 0.017 * grade</c> consumption coefficient, so <c>EV × grade</c> IS thrust per kg of engine.
        /// At grade 0.05 that reads 51.0 N/kg against the nuclear drive's 89.2 — it genuinely pushes softer.</para>
        ///
        /// <para>🔒 <b>And the payoff that makes CONTAINMENT load-bearing instead of decorative:</b> the engine's
        /// <c>Fuel Type</c> dial is filtered by <b>cargo type</b> (<c>DataDict</c> keyed on the compartment class), and
        /// antimatter rides <c>contained-storage</c>. <b>So a ship with an antimatter drive and no containment hold has
        /// a drive it cannot feed.</b> That is two systems constraining each other, which is the whole point of the
        /// Connect rule.</para>
        /// </summary>
        [Test]
        [Description("Antimatter exists cradle-to-grave: refined from fissionables at ruinous cost, riding a containment hold, burned by an antimatter drive that binds from JSON. It continues the shipped fuel ladder honestly — the highest exhaust velocity, and the LOWEST thrust per kilogram of engine, so it goes furthest per kilogram of fuel and pushes softest rather than being strictly better. That last check is asserted on EV × grade (thrust per engine-kg, what the sim actually computes) rather than on the grade dial alone, because a weaker version of this test passed a calibration that gave antimatter 4× the nuclear drive's thrust. And because the engine's fuel dial filters by compartment class, a ship with this drive and no containment hold has a drive it cannot feed.")]
        public void Antimatter_ExistsCradleToGrave_AndMakesContainmentLoadBearing()
        {
            var s = TestScenario.CreateWithColony();
            var data = s.Faction.GetDataBlob<FactionInfoDB>().Data;

            // ① THE GOOD — produced (a real recipe) and stored where it belongs.
            var am = data.CargoGoods.GetAny("antimatter") as Pulsar4X.Industry.ProcessedMaterial;
            Assert.That(am, Is.Not.Null, "antimatter is a base-mod refined material");
            Assert.That(am.CargoTypeID, Is.EqualTo("contained-storage"),
                "it annihilates on contact — it rides containment, never a fuel tank");
            Assert.That(am.ResourceCosts, Is.Not.Empty, "and it is REFINED from something, not conjured");
            Assert.That(s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns.ContainsKey("antimatter"), Is.True,
                "…and it is refinable at the start colony (in StartingItems, so it becomes an IndustryDesign)");
            Log($"antimatter: {am.CargoTypeID}, costs {string.Join(" + ", am.ResourceCosts.Select(kv => $"{kv.Value} {kv.Key}"))}"
                + $" → {am.OutputAmount}, {am.IndustryPointCosts} industry points");

            // ② IT IS EXPENSIVE — the gate is ENERGY, not knowledge. Compared against the dearest existing fuel.
            var ntp = data.CargoGoods.GetAny("ntp") as Pulsar4X.Industry.ProcessedMaterial;
            Assert.That(am.IndustryPointCosts, Is.GreaterThan(ntp.IndustryPointCosts),
                "antimatter must cost far more industry than the next-dearest fuel, or it is free power");
            Assert.That(am.CreditValue, Is.GreaterThan(ntp.CreditValue), "and be worth far more");

            // ③ THE LADDER — it must EXTEND the trade, not break it. Exhaust velocity up, grade DOWN.
            double Ev(string id) => double.Parse(
                ((Pulsar4X.Industry.ProcessedMaterial)data.CargoGoods.GetAny(id)).Formulas["ExhaustVelocity"]);
            double Grade(string id) => double.Parse(
                ((Pulsar4X.Industry.ProcessedMaterial)data.CargoGoods.GetAny(id)).Formulas["FuelGrade"]);

            foreach (var id in new[] { "rp-1", "hydrolox", "ntp", "antimatter" })
                Log($"  {id,-12} exhaust {Ev(id),8:N0} m/s · grade {Grade(id):0.00}");

            Assert.That(Ev("antimatter"), Is.GreaterThan(Ev("ntp")),
                "the highest exhaust velocity in the game — that is what it buys");
            Assert.That(Grade("antimatter"), Is.LessThan(Grade("ntp")), "…and the LOWEST grade");

            // 🔒 THE REAL DOMINANCE GAUGE — and the one that caught a bad calibration.
            // "grade is lower" is NOT enough, because thrust = ExhaustVelocity × massflow and massflow scales with
            // grade, so a big EV rise can swamp a small grade fall and the fuel wins BOTH axes. First authored at
            // grade 0.35 antimatter had 4× the NTR's thrust per kg of engine AND 8.6× its range — strictly better,
            // gated only by price, which is exactly the dominance §26.3 exists to remove. The gauge has to read the
            // product the sim actually computes. Both templates share the same `[Mass] * 0.017 * grade` consumption
            // coefficient, so `EV × grade` IS thrust per kg of engine, directly comparable between the two.
            double ThrustPerEngineKg(string fuel) => Ev(fuel) * Grade(fuel) * 0.017;
            Log($"  thrust per kg of engine — ntp {ThrustPerEngineKg("ntp"):N1} N/kg"
                + $" vs antimatter {ThrustPerEngineKg("antimatter"):N1} N/kg");
            Assert.That(ThrustPerEngineKg("antimatter"), Is.LessThan(ThrustPerEngineKg("ntp")),
                "🔒 antimatter must push SOFTER per kg of engine than the nuclear drive. It goes much further per kg "
                + "of FUEL (8.6× the exhaust velocity) and that is what you pay for — but it is not also the "
                + "punchiest drive, or nothing else on the ladder would ever be built");

            // ④ THE CONSUMER — an engine that burns it, bound from JSON.
            var drive = data.ComponentTemplates["antimatter-engine"];
            Assert.That(drive, Is.Not.Null, "the antimatter drive template is unlocked");
            var fuelProp = drive.Properties.Single(pr => pr.Name == "Fuel Type");
            Assert.That(fuelProp.PropertyFormula, Does.Contain("antimatter"), "the drive burns antimatter");

            // ⑤ 🔒 THE JOINT — the fuel dial is filtered by COMPARTMENT CLASS, so the drive is unfeedable without
            //    a containment hold. This is the assertion that makes containment matter.
            Assert.That(fuelProp.DataDict, Is.Not.Null, "the fuel dial filters by compartment");
            Assert.That(fuelProp.DataDict.Keys, Does.Contain("contained-storage"),
                "🔒 the drive's fuel selector is keyed on CONTAINED storage — so a ship with this engine and no "
                + "containment hold has a drive it cannot feed, and the two systems constrain each other");

            var built = s.Faction.GetDataBlob<FactionInfoDB>().ComponentDesigns["default-design-antimatter-drive"];
            Assert.That(built.HasAttribute<Pulsar4X.Movement.NewtonionThrustAtb>(), Is.True,
                "and the shipped drive design binds a real thrust attribute (the gotcha-#10 JSON→atb sensor)");
            var thrust = built.GetAttribute<Pulsar4X.Movement.NewtonionThrustAtb>();
            Log($"{built.Name}: {built.MassPerUnit:N0} kg, exhaust {thrust.ExhaustVelocity:N0} m/s, crew {built.CrewReq}");
            Assert.That(thrust.ExhaustVelocity, Is.EqualTo(Ev("antimatter")).Within(1),
                "the drive's exhaust velocity comes from the FUEL, through ExhaustVelocityLookup");
        }

        /// <summary>
        /// The three goods that were filed as dry bulk and are physically something else. Moving them is what the
        /// gauge above exists to make safe.
        /// </summary>
        [Test]
        [Description("Water and hydrocarbons are liquids and now ride the sealed fluid compartment rather than dry bulk, and raw fissionables is radioactive and now rides containment — the three goods in the shipped inventory that were filed in the wrong box. The other 28 in general storage are ore, refined metal and machine parts, which genuinely belong there.")]
        public void TheThreeMisfiledGoods_NowRideThePhysicallyCorrectCompartment()
        {
            var s = TestScenario.CreateWithColony();
            var data = s.Faction.GetDataBlob<FactionInfoDB>().Data;

            foreach (var (id, cls, why) in new[]
                     {
                         ("water",        "fuel-storage",      "a liquid — it has no shape of its own"),
                         ("hydrocarbons", "fuel-storage",      "a liquid"),
                         ("fissionables", "contained-storage", "raw radioactive ore — dangerous to its own carrier"),
                     })
            {
                var good = data.CargoGoods.GetAny(id);
                Assert.That(good, Is.Not.Null, $"{id} is a base-mod good");
                Log($"  {id,-14} → {good.CargoTypeID,-20} ({why})");
                Assert.That(good.CargoTypeID, Is.EqualTo(cls), $"{id}: {why}");
            }

            // And the ones that are correctly bulk stay bulk — the point is three moves, not a sweep.
            foreach (var id in new[] { "iron", "stainless-steel", "plastic", "electronics", "space-crete" })
            {
                var good = data.CargoGoods.GetAny(id);
                Assert.That(good.CargoTypeID, Is.EqualTo("general-storage"),
                    $"{id} is ore/metal/parts — it belongs in dry bulk and must NOT be swept up in the move");
            }
        }

        /// <summary>
        /// 🔑 FOOD IS A SHIPPABLE GOOD AT LAST — and it is the first good the new taxonomy exists FOR.
        ///
        /// <para><c>SustenanceProcessor</c>'s own doc-comment described the gap for however long it stood there:
        /// <i>"food from the — not-yet-existing — food cargo good, so 0 for now."</i> Food was an installation OUTPUT
        /// read off installed components, so it was <b>grown and eaten in the same place and could never be shipped</b>.
        /// A colony that could not farm could never be supplied by one that could.</para>
        ///
        /// <para>This asserts the whole chain: the good exists, it is refinable at the start colony, it rides
        /// <c>perishable-storage</c> (so a bare hold will not take it — the taxonomy doing real work), and the
        /// processor <b>draws it down</b> to cover a shortfall its farms cannot.</para>
        /// </summary>
        [Test]
        [Description("Food is a real shippable good: refinable at the start colony, riding a refrigerated hold rather than general storage, and actually consumed by SustenanceProcessor to cover a shortfall the local farms cannot — so a colony that cannot farm can now be supplied by one that can.")]
        public void Food_IsAShippableGood_AndAnImportedStockpileFeedsAColony()
        {
            var s = TestScenario.CreateWithColony();
            var data = s.Faction.GetDataBlob<FactionInfoDB>().Data;

            var food = data.CargoGoods.GetAny(SustenanceProcessor.FoodGoodID);
            Assert.That(food, Is.Not.Null, "the food good exists (materials.json)");
            Assert.That(food.CargoTypeID, Is.EqualTo("perishable-storage"),
                "food rides a REFRIGERATED hold — the first good the new taxonomy exists for");
            Assert.That(s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns.ContainsKey(SustenanceProcessor.FoodGoodID),
                Is.True, "and it is refinable at the start colony (in StartingItems, so it becomes an IndustryDesign)");
            Log($"{food.Name}: {food.CargoTypeID}, {food.MassPerUnit} kg and {food.VolumePerUnit:0.####} m³ per unit");

            // A hold that is only GENERAL storage must REFUSE it — this is the taxonomy biting, not decoration.
            //
            // ⚠ Asserted on a PURPOSE-BUILT bare hold, NOT on the start colony's, and that distinction is a
            // scar. It used to read `s.Colony.GetDataBlob<CargoStorageDB>()`, and it went red the moment the
            // very same commit installed a `cold-store` on Earth (10,000 m³ of perishable room, added so the
            // colony had somewhere to put the food it refines). The assertion was right and the FIXTURE was
            // wrong: "general storage refuses food" is a claim about a CLASS, so testing it against whatever
            // the scenario happens to have installed makes it hostage to unrelated data edits. A bare
            // CargoStorageDB needs no entity and cannot drift.
            var bareGeneralHold = new CargoStorageDB("general-storage", 10_000);
            Assert.That(bareGeneralHold.GetFreeVolume(food), Is.EqualTo(0),
                "a hold with only general storage cannot take food at all — that is the point of the class");

            var hold = s.Colony.GetDataBlob<CargoStorageDB>();

            // Give it a refrigerated hold and stock it.
            var reefer = s.Faction.GetDataBlob<FactionInfoDB>().ComponentDesigns["default-design-refrigerated-hold"];
            reefer.GetAttribute<CargoStorageAtb>().OnComponentInstallation(s.Colony, new ComponentInstance(reefer));
            Assert.That(hold.GetFreeVolume(food), Is.GreaterThan(0), "the reefer gives food somewhere to go");

            long stocked = hold.AddCargoByUnit(food, 10_000);
            Log($"stocked {stocked} units of food");
            Assert.That(stocked, Is.GreaterThan(0), "and it loads");

            // Now make the colony hungry and run the real recompute. Demand defaults to 0 — which is exactly why this
            // whole feature is byte-identical on a stock game — so the test sets it, the way a calibrated build would.
            var sust = s.Colony.GetDataBlob<ColonySustenanceDB>();
            Assert.That(sust, Is.Not.Null, "the start colony carries a sustenance blob");
            long before = hold.GetUnitsStored(food, false);

            SustenanceProcessor.Recalc(s.Colony);
            Assert.That(hold.GetUnitsStored(food, false), Is.EqualTo(before),
                "with the stock per-capita demand of 0 nothing is drawn — the byte-identity guarantee");

            sust.SetDemand(perCapitaPower: 0.0, perCapitaFood: 0.001);
            SustenanceProcessor.Recalc(s.Colony);
            long after = hold.GetUnitsStored(food, false);
            Log($"food drawn this month: {before - after} units · shortage now {sust.FoodShortage:0.###}");

            Assert.That(after, Is.LessThan(before),
                "a hungry colony EATS the imported food — a supply that is read but never consumed is free food");
            Assert.That(sust.FoodShortage, Is.LessThan(1.0),
                "…and the import measurably closes the shortage it would otherwise have starved on");

            // 🔑 THE OTHER HALF: a SURPLUS banks, or a surplus cannot exist and food can never be EXPORTED.
            // Farm output used to be a per-day rate consumed the instant it was computed, so a colony growing ten
            // times what it eats had nothing to ship. Set demand back to 0 → everything the (absent) farms make is
            // surplus; with no farms there is nothing to bank, which is exactly the stock byte-identical case.
            sust.SetDemand(perCapitaPower: 0.0, perCapitaFood: 0.0);
            long beforeBank = hold.GetUnitsStored(food, false);
            SustenanceProcessor.Recalc(s.Colony);
            Assert.That(hold.GetUnitsStored(food, false), Is.EqualTo(beforeBank),
                "a colony with NO farm banks nothing — the surplus path is inert exactly where the stock game is");
            Log($"surplus path with no farm installed: {beforeBank} units unchanged (byte-identical)");
        }

        /// <summary>
        /// The bug, end to end and in both directions: a research team gets <b>0</b> room on a host with only general
        /// storage (which is every host in the game before this change), and real room once a passenger cabin is
        /// installed. This is the red-before / green-after gauge.
        /// </summary>
        [Test]
        [Description("A research team could not be carried by anything: on a host with only general storage it reads 0 free volume — silently, because CargoMath returns 0 for an unknown cargo type rather than erroring. Installing a passenger cabin gives it real room, so people are now movable.")]
        public void AResearchTeam_CannotBeCarried_UntilAPassengerCabinIsInstalled()
        {
            var s = TestScenario.CreateWithColony();
            var host = s.Colony;
            var team = new Scientist { LeaderName = "Test Scientist" };
            team.TeamSize = 5;

            Assert.That(team.CargoTypeID, Is.EqualTo(PassengerPacking.PassengerStorage),
                "a team has always declared itself a passenger — that half was never the problem");

            var store = host.GetDataBlob<CargoStorageDB>();
            Assert.That(store, Is.Not.Null, "the start colony has cargo storage (a warehouse)");
            Log("host stores before: " + string.Join(", ", store.TypeStores.Keys));

            // THE BUG. Not an exception — a zero.
            Assert.That(store.GetFreeVolume(team), Is.EqualTo(0),
                "with no passenger compartment the team gets zero room, and CargoMath says so silently");

            var design = s.Faction.GetDataBlob<FactionInfoDB>()
                          .ComponentDesigns["default-design-passenger-cabin"];
            Assert.That(design.HasAttribute<CargoStorageAtb>(), Is.True,
                "the cabin binds a CargoStorageAtb from JSON (the gotcha-10 JSON→atb sensor)");
            var atb = design.GetAttribute<CargoStorageAtb>();
            Assert.That(atb.StoreTypeID, Is.EqualTo(PassengerPacking.PassengerStorage),
                "…and it is a PASSENGER compartment, not another general hold");

            atb.OnComponentInstallation(host, new ComponentInstance(design));
            Log("host stores after:  " + string.Join(", ", store.TypeStores.Keys));

            double free = store.GetFreeVolume(team);
            Log($"free passenger volume {free:0} m³ · a team of {team.TeamSize} needs {team.VolumePerUnit:0} m³");
            Assert.That(free, Is.EqualTo(atb.MaxVolume),
                "the cabin's whole volume is available to passengers");
            Assert.That(free, Is.GreaterThan(team.VolumePerUnit),
                "and the team fits — people are movable now, which is the point of the slice");

            // The general hold is untouched: a passenger compartment must not become a second cargo hold.
            Assert.That(store.TypeStores.ContainsKey("general-storage"), Is.True,
                "the general store the colony already had is unchanged (purely additive)");
        }

        /// <summary>
        /// 🔒 §39.8 applied to this door: every compartment must win an axis outright, or it is clutter. The axes a
        /// compartment is judged on are the ones the sim reads — mass per cubic metre of capacity, crew, and (for
        /// people) how many fit.
        /// </summary>
        [Test]
        [Description("Each compartment class wins an axis outright: general storage is the lightest per cubic metre, containment the heaviest (the shielding IS the capability), a cryo bay carries five times the people of a passenger cabin the same size on a fraction of the crew, and a cabin is the one whose occupants arrive awake. No class is beaten on everything.")]
        public void EachCompartment_WinsAnAxisOutright()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ComponentDesigns;

            var cells = new (string label, string id)[]
            {
                ("general hold",     "default-design-cargo-hold-5t"),
                ("passenger cabin",  "default-design-passenger-cabin"),
                ("cryo bay",         "default-design-cryo-bay"),
                ("refrigerated",     "default-design-refrigerated-hold"),
                ("containment",      "default-design-containment-hold"),
            };

            var kgPerM3 = new Dictionary<string, double>();
            var crew = new Dictionary<string, double>();
            Log("compartment        capacity m³   kg      kg per m³   crew   class");
            foreach (var (label, id) in cells)
            {
                Assert.That(designs.ContainsKey(id), Is.True, $"{id} should be built for the start faction");
                var d = designs[id];
                var atb = d.GetAttribute<CargoStorageAtb>();
                double perM3 = d.MassPerUnit / atb.MaxVolume;
                kgPerM3[label] = perM3;
                crew[label] = d.CrewReq;
                Log($"{label,-18} {atb.MaxVolume,10:0} {d.MassPerUnit,8:0} {perM3,11:0.###} {d.CrewReq,6} {atb.StoreTypeID}");
            }

            // ✅ THE FIELD IS KILOGRAMS — developer's ruling, 2026-07-30 — so the coefficients are now right.
            // They were written by an author thinking in TONNES (the `Size Efficiency` property described itself as
            // "the amount of TONNAGE taken up by racking"), which left every hold light by exactly 100×. The factor is
            // not a guess: the shipped design NAMES pin it. "Cargo Hold 1t" (1000 m³) and "Cargo Hold 5t" (5000 m³)
            // come out at exactly 1,000 kg and 5,000 kg once the ×100 is restored — the names were right all along and
            // the formula had lost the unit.
            Log("mass coefficients are ×100 (the field is KILOGRAMS): a shipped 5t hold now masses exactly 5,000 kg.");
            // ⚠ One calibration gap left flagged rather than silently changed: this puts a bare hold at 1.0 kg per m³
            // of capacity, where a real 33 m³ shipping container masses ~2,200 kg — about 67 kg/m³. So the family is
            // still ~67× lighter than steel-box reality. That is a BALANCE call on top of a UNIT fix, and it is not the
            // same decision, so it is not folded in here.

            // Direction, not calibration — a re-tune may move the numbers, but not the ordering that justifies each class.
            Assert.That(kgPerM3["general hold"], Is.LessThan(kgPerM3["refrigerated"]),
                "a bare hold is lighter per cubic metre than a cooled one — that is general storage's whole axis");
            // Compare against the other labels by NAME — filtering the value list by inequality would silently drop a
            // second class that happened to tie, and then the assertion would be weaker than it reads.
            double heaviestOther = kgPerM3.Where(kv => kv.Key != "containment").Max(kv => kv.Value);
            Assert.That(kgPerM3["containment"], Is.GreaterThan(heaviestOther),
                "containment is the heaviest per cubic metre: the shielding is not overhead, it IS the capability");
            Assert.That(kgPerM3["passenger cabin"], Is.GreaterThan(kgPerM3["refrigerated"]),
                "a pressure hull with life support costs more than a cooling plant");

            // The cabin↔cryo trade, which is the real decision this pair exists to offer.
            double perM3Cabin = PassengerPacking.BerthVolume_m3, perM3Pod = PassengerPacking.CryoPodVolume_m3;
            Assert.That(perM3Pod, Is.LessThan(perM3Cabin),
                "frozen people pack tighter — the cryo bay's axis is people per cubic metre");
            Assert.That(crew["cryo bay"], Is.LessThan(crew["passenger cabin"]),
                "…and nobody has to serve them, which is its second axis");
        }

        /// <summary>
        /// The corrected person-volume, pinned. It had never been read because nothing provided
        /// <c>passenger-storage</c>; now that something does, the number is load-bearing.
        /// </summary>
        [Test]
        [Description("A person needs a berth, not a body-sized hole: 10 m³ in a passenger cabin (bunk plus their share of air, water, corridor and sick bay) and 2 m³ in a cryogenic pod. It used to report 0.065 m³ — the volume of a human body — which would have berthed over seven thousand people in a 500 m³ cabin. It was never wrong in play because nothing provided passenger storage, so it was never read.")]
        public void APerson_TakesUpABerth_NotABodysWorthOfSpace()
        {
            var team = new Scientist { LeaderName = "Test Scientist" };
            team.TeamSize = 10;

            Assert.That(team.CargoTypeID, Is.EqualTo(PassengerPacking.PassengerStorage));
            Assert.That(team.VolumePerUnit, Is.EqualTo(10 * PassengerPacking.BerthVolume_m3),
                "ten people in berths take ten berths' worth of room");
            Assert.That(team.MassPerUnit, Is.EqualTo(10 * (long)PassengerPacking.MassPerPerson_kg),
                "…and the mass figure is the pre-existing one, unchanged");

            // CargoTypeID is settable, so putting the same team into cryo is a real choice — and it changes how
            // much room they need, because that was always a property of HOW you carry them, not of them.
            team.CargoTypeID = PassengerPacking.CryogenicStorage;
            Assert.That(team.VolumePerUnit, Is.EqualTo(10 * PassengerPacking.CryoPodVolume_m3),
                "the same ten people frozen take a fifth of the room");
            Log($"10 people: {10 * PassengerPacking.BerthVolume_m3:0} m³ in berths, "
                + $"{10 * PassengerPacking.CryoPodVolume_m3:0} m³ in pods");

            Assert.That(PassengerPacking.BerthVolume_m3, Is.GreaterThan(1.0),
                "guard against the old 0.065 m³ body-volume figure ever coming back");
        }

        /// <summary>
        /// 🔒 THE FIELD IS KILOGRAMS (developer's ruling, 2026-07-30) — so the shipped holds keep their capacity and
        /// their MASS IS CORRECTED, not preserved.
        ///
        /// <para>Every hold's <c>Mass</c> was <c>Size Efficiency × 1.0</c>, and <c>Size Efficiency</c> is
        /// <c>volume × 0.01</c> — so a 5,000 m³ hold massed <b>50 kg</b>. The property's own description called itself
        /// <i>"the amount of <b>tonnage</b> taken up by racking, office space etc."</i>: the author was thinking in
        /// tonnes and the field is kilograms.</para>
        ///
        /// <para><b>The factor is not a guess — the shipped design NAMES pin it at exactly 100.</b>
        /// <c>Cargo Hold 1t</c> is a 1,000 m³ design and <c>Cargo Hold 5t</c> a 5,000 m³ one; with the ×100 restored
        /// they mass exactly <b>1,000 kg</b> and <b>5,000 kg</b>. The names were right the whole time and the formula
        /// had lost the unit — which is why this is a UNIT fix, not a balance change.</para>
        ///
        /// <para>⚠ <b>This is the one part of the slice that is deliberately NOT byte-identical.</b> Six base-mod ships
        /// mount a hold; the largest change is the <b>Freighter</b> (a 5t hold, 50 kg → 5,000 kg = +4,950 kg against
        /// its medium hull's 90,000 kg budget). <c>ShipMassBudgetTests</c> is the gauge that adjudicates it — it asserts
        /// every base-mod ship stays under its hull budget and will fail loudly if this pushes any design over.</para>
        /// </summary>
        [Test]
        [Description("The shipped cargo holds keep their store type and capacity, and their mass is corrected to real kilograms: the field is kg, the coefficients were written in tonnes, and the design names Cargo Hold 1t / 5t pin the missing factor at exactly 100 — so they now mass exactly 1,000 kg and 5,000 kg, making their own names true.")]
        public void TheExistingHolds_KeepTheirCapacity_AndNowMassRealKilograms()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ComponentDesigns;

            foreach (var (id, vol, mass) in new (string, double, long)[]
                     {
                         ("default-design-cargo-hold-1t", 1000, 1000),   // "1t" → 1,000 kg ✅ the name is now true
                         ("default-design-cargo-hold-5t", 5000, 5000),   // "5t" → 5,000 kg ✅
                     })
            {
                var d = designs[id];
                var atb = d.GetAttribute<CargoStorageAtb>();
                Log($"{d.Name}: {atb.StoreTypeID} {atb.MaxVolume:0} m³ on {d.MassPerUnit} kg "
                    + $"({d.MassPerUnit / 1000.0:0.#} t — matches the design's own name)");

                // Capacity and class are untouched: nothing about WHAT a hold carries changed.
                Assert.That(atb.StoreTypeID, Is.EqualTo("general-storage"), id + " is still a general hold");
                Assert.That(atb.MaxVolume, Is.EqualTo(vol), id + " still holds the same volume");

                // Mass is corrected, and the assertion is the design's own name read as kilograms.
                Assert.That(d.MassPerUnit, Is.EqualTo(mass),
                    id + " must mass what its name says, in kilograms — if this fails the ×100 was lost again");
            }
        }
    }
}
