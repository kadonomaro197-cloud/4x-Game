using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Pulsar4X.Blueprints;
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
