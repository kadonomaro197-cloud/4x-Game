using System.Linq;
using NUnit.Framework;
using Pulsar4X.Components;
using Pulsar4X.DataStructures;
using Pulsar4X.Energy;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// EVERY POWER OPTION MUST HAVE A JUSTIFICATION OVER THE OTHERS — the developer's rule, 2026-07-30
    /// (docs/economy/DESIGNER-NORTH-STAR.md §39.8).
    ///
    /// <para><b>The rule, stated as a test.</b> An option a player would never choose is not an option, it is clutter.
    /// So <b>no power source may be beaten on EVERY axis the simulation reads</b> — each must win at least one outright,
    /// and that win is its reason to exist. The axes are the ones the sim actually consumes: power per kilogram (the
    /// chassis budget), power per cubic metre (volume), crew (a real scarce pool via <c>ManpowerTools</c>), signature
    /// (detection decides who shoots first), fuel dependence, and where it can be mounted.</para>
    ///
    /// <para><b>Two dominance bugs were blocking it, both fixed in the same change:</b></para>
    /// <list type="number">
    /// <item>The reactor's <c>CrewReq</c> was <c>[Mass]</c> — <b>crew equal to kilograms</b>, so a 1500 kg reactor
    /// demanded 1500 crew. Component crew sums into a ship's <c>CrewReq</c> (and into
    /// <c>InfrastructureProcessor</c>'s capacity demand), so the highest-power-density generator in the game was
    /// unusable in practice. Now <c>Max(2, [Mass] / 500)</c> — three operators at the stock 1500 kg.</item>
    /// <item>The RTG had <b>no axis it won</b>: ~22,000× worse per kilogram than the steam turbine, to save two crew.
    /// A strictly dominated recipe, exactly like RP-1 before the fuel-grade fix. Its crew is now <b>0</b>, which makes
    /// it the only <em>fuelled</em> generator needing nobody aboard — the probe / deep-space / unmanned-outpost
    /// option. ⚠ Its power density gap is flagged, not silently tuned (§39.8).</item>
    /// </list>
    ///
    /// <para>And the finding that needed no fix at all: <b>solar is the only silent power source in the game.</b> The
    /// three fuelled generators all carry a <c>SensorSignatureAtb</c> at 1700 K — the reactor being the loudest thing
    /// aboard a ship — while a solar array emits nothing. That is a complete, already-wired justification that nothing
    /// in the game states.</para>
    /// </summary>
    [TestFixture]
    public class PowerJustificationTests
    {
        private const string Reactor = "default-design-fission-reactor";
        private const string Turbine = "default-design-reactor-2t";
        private const string Solar   = "default-design_solarpanel";
        private const string Battery = "default-design-battery-2t";

        private static void Log(string m) => TestContext.Progress.WriteLine("[justify] " + m);

        private static ComponentDesign Design(TestScenario s, string id)
        {
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ComponentDesigns;
            Assert.That(designs.ContainsKey(id), Is.True, $"base-mod design '{id}' should be built for the start faction");
            return designs[id];
        }

        /// <summary>
        /// The reactor's axis: power per kilogram. It must WIN that outright, or it has no reason to exist beside a
        /// turbine that is cheaper in crew and vastly better in volume.
        /// </summary>
        [Test]
        [Description("The reactor wins POWER PER KILOGRAM — its whole justification — and its crew bill is now a plausible operator count rather than a kilogram count (it demanded 1500 crew for 1500 kg, which made the densest generator in the game unusable).")]
        public void TheReactor_WinsPowerPerKilogram_AndItsCrewBillIsSane()
        {
            var s = TestScenario.CreateWithColony();
            var reactor = Design(s, Reactor);
            var turbine = Design(s, Turbine);

            reactor.TryGetAttribute<EnergyGenerationAtb>(out var rAtb);
            turbine.TryGetAttribute<EnergyGenerationAtb>(out var tAtb);
            double rPerKg = rAtb.PowerOutputMax / reactor.MassPerUnit;
            double tPerKg = tAtb.PowerOutputMax / turbine.MassPerUnit;
            Log($"kW per kg — reactor {rPerKg:0.##} · turbine {tPerKg:0.##}");
            Log($"crew — reactor {reactor.CrewReq} · turbine {turbine.CrewReq}");

            Assert.That(rPerKg, Is.GreaterThan(tPerKg),
                "the reactor must be the densest generator — that is the only axis it wins, so it is its justification");

            // Crew is a real scarce pool (ManpowerTools.ResolveBuild), and component crew sums into a ship's CrewReq.
            // A crew count in the same order as the mass in kilograms is the bug this pins.
            Assert.That(reactor.CrewReq, Is.LessThan(reactor.MassPerUnit / 100),
                "crew must not scale like kilograms — 1500 crew for a 1500 kg reactor made it unbuildable in practice");
            Assert.That(reactor.CrewReq, Is.GreaterThanOrEqualTo(2),
                "…but a fission plant is not unmanned either; that is the RTG's job");
        }

        /// <summary>
        /// The turbine's axis: power per cubic metre. Already true in the data (<c>Volume = Mass / 1000</c>) and never
        /// stated anywhere.
        /// </summary>
        [Test]
        [Description("The steam turbine wins POWER PER CUBIC METRE by three orders of magnitude — its Volume formula divides by 1000 where the reactor's does not. That is its justification: the station and colony plant, where volume binds and mass does not.")]
        public void TheTurbine_WinsPowerPerCubicMetre()
        {
            var s = TestScenario.CreateWithColony();
            var reactor = Design(s, Reactor);
            var turbine = Design(s, Turbine);

            reactor.TryGetAttribute<EnergyGenerationAtb>(out var rAtb);
            turbine.TryGetAttribute<EnergyGenerationAtb>(out var tAtb);
            double rPerM3 = rAtb.PowerOutputMax / reactor.VolumePerUnit;
            double tPerM3 = tAtb.PowerOutputMax / turbine.VolumePerUnit;
            Log($"kW per m³ — reactor {rPerM3:0.##} · turbine {tPerM3:0.##}  (ratio {tPerM3 / rPerM3:0.#})");

            Assert.That(tPerM3, Is.GreaterThan(rPerM3 * 100),
                "the turbine's volume edge is its justification, and it should be a large one");
        }

        /// <summary>
        /// 🔑 The finding that needed no fix: solar is the only silent power source, and the fuelled generators are the
        /// loudest things aboard. Detection decides who shoots first, so this is a first-class justification.
        /// </summary>
        [Test]
        [Description("Solar is the ONLY silent power source: the three fuel-burning generators each emit a 1700 K sensor signature (the reactor being the loudest thing on a ship), while a solar array emits nothing at all. Detection decides who shoots first, so silence is a real reason to choose it.")]
        public void Solar_IsTheOnlySilentPowerSource()
        {
            var s = TestScenario.CreateWithColony();
            var reactor = Design(s, Reactor);
            var turbine = Design(s, Turbine);
            var solar   = Design(s, Solar);

            bool rLoud = reactor.TryGetAttribute<Pulsar4X.Sensors.SensorSignatureAtb>(out var rSig);
            bool tLoud = turbine.TryGetAttribute<Pulsar4X.Sensors.SensorSignatureAtb>(out _);
            bool sLoud = solar.TryGetAttribute<Pulsar4X.Sensors.SensorSignatureAtb>(out _);
            Log($"emits a signature? reactor {rLoud} · turbine {tLoud} · solar {sLoud}");
            if (rLoud) Log($"the reactor's magnitude: {rSig.PartWaveFormMag:0}");

            Assert.That(rLoud, Is.True, "a fission plant is loud");
            Assert.That(tLoud, Is.True, "so is a steam turbine");
            Assert.That(sLoud, Is.False,
                "and a solar array is SILENT — the one justification in this door that was already complete");

            // …and it needs nobody aboard, which compounds it: silent AND unmanned AND fuel-free.
            Assert.That(solar.CrewReq, Is.EqualTo(0), "a panel needs no operators");
            Assert.That(solar.ResourceCosts.ContainsKey("fissile-fuels"), Is.False,
                "and no fissile fuel — its cost is density and distance from the star, not logistics");
        }

        /// <summary>
        /// The rule itself, as one assertion over the door: <b>no TYPE of power source may be beaten on every axis.</b>
        /// One representative per type — the developer asked about the <i>types</i>, and a de-rated reactor is the same
        /// type at a different dial setting (its own justification is fuel economy, pinned by
        /// <c>PowerFuelGateTests</c>). This is the test that catches the next dead recipe.
        /// </summary>
        [Test]
        [Description("No TYPE of power source is beaten on every axis the sim reads. The reactor wins power-per-kilogram, the turbine wins power-per-cubic-metre, and solar wins crew AND silence — so each has a reason a player would pick it, which is the developer's rule made executable.")]
        public void NoPowerType_IsDominatedOnEveryAxis()
        {
            var s = TestScenario.CreateWithColony();

            // One representative per TYPE. (The rtg template has no shipped design, so it cannot be measured here —
            // its zero-crew justification is asserted structurally in TheRtg_IsTheUnattendedOption below.)
            var types = new[] { Reactor, Turbine, Solar }.Select(id => Design(s, id)).ToList();

            double Kw(ComponentDesign d) =>
                  d.TryGetAttribute<EnergyGenerationAtb>(out var g) ? g.PowerOutputMax
                : d.TryGetAttribute<EnergySolarGenerationAtb>(out var sol) ? sol.Area_m2 * sol.BestEfficiency * 1.361
                : 0;
            double PerKg(ComponentDesign d) => Kw(d) / d.MassPerUnit;
            double PerM3(ComponentDesign d) => Kw(d) / System.Math.Max(1e-9, d.VolumePerUnit);
            bool Silent(ComponentDesign d) => !d.TryGetAttribute<Pulsar4X.Sensors.SensorSignatureAtb>(out _);

            Log("type                                 kW/kg      kW/m3   crew  silent");
            foreach (var d in types)
                Log($"{d.Name,-34} {PerKg(d),9:0.####} {PerM3(d),10:0.##} {d.CrewReq,5} {Silent(d),7}");

            foreach (var d in types)
            {
                bool winsKg    = types.All(o => ReferenceEquals(o, d) || PerKg(o) <= PerKg(d));
                bool winsM3    = types.All(o => ReferenceEquals(o, d) || PerM3(o) <= PerM3(d));
                bool winsCrew  = types.All(o => ReferenceEquals(o, d) || o.CrewReq >= d.CrewReq);
                bool winsQuiet = Silent(d) && types.Any(o => !ReferenceEquals(o, d) && !Silent(o));

                Log($"  {d.Name}: kg={winsKg} m3={winsM3} crew={winsCrew} quiet={winsQuiet}");
                Assert.That(winsKg || winsM3 || winsCrew || winsQuiet, Is.True,
                    $"'{d.Name}' is beaten on every axis — an option nobody would ever choose is clutter, not a choice");
            }
        }

        /// <summary>
        /// The RTG ships no design, so its justification is asserted on the TEMPLATE: it is the only <em>fuelled</em>
        /// generator that needs nobody aboard. That is the axis it wins, and it is why its crew went to zero — before
        /// that it was ~22,000x worse per kilogram than a turbine to save two crew, a strictly dominated recipe.
        /// </summary>
        [Test]
        [Description("The RTG is the UNATTENDED option: the only fuel-burning generator whose crew requirement is zero, so it is the probe / deep-space / unmanned-outpost choice. Its power-density gap is a flagged calibration, not a silent tune.")]
        public void TheRtg_IsTheUnattendedOption()
        {
            var s = TestScenario.CreateWithColony();
            var templates = s.Faction.GetDataBlob<FactionInfoDB>().Data.ComponentTemplates;
            Assert.That(templates.ContainsKey("rtg"), Is.True, "the rtg template should be unlocked");

            string rtgCrew = templates["rtg"].Formulas["CrewReq"];
            string reactorCrew = templates["reactor"].Formulas["CrewReq"];
            string turbineCrew = templates["steam-turbine-reactor"].Formulas["CrewReq"];
            Log($"crew formulas — rtg '{rtgCrew}' | reactor '{reactorCrew}' | turbine '{turbineCrew}'");

            Assert.That(rtgCrew.Trim(), Is.EqualTo("0"),
                "zero crew is the RTG's whole justification — without it, it wins nothing");
            Assert.That(turbineCrew.Trim(), Is.Not.EqualTo("0"),
                "…and it must be the ONLY fuelled generator that is unmanned, or the axis is not its own");
            Assert.That(reactorCrew, Is.Not.EqualTo("[Mass]"),
                "the reactor's crew must not be a kilogram count (1500 crew for 1500 kg made it unbuildable)");
        }

        /// <summary>
        /// Solar's justification was real and UNREACHABLE — the reverse of the reactor's. It was the obvious colony
        /// power plant and it could only be mounted on a ship, because <c>MountType</c> was authored as the raw
        /// integer <c>1</c> (which happens to equal <c>ComponentMountType.ShipComponent</c>) where every other
        /// template in the game names its mounts. <c>SustenanceProcessor:51</c> already reads a colony's
        /// <c>EnergyGenAbilityDB.TotalOutputMax</c> into the power-shortage term that drives morale, so the consumer
        /// was built and the producer was unbuildable.
        /// </summary>
        [Test]
        [Description("A colony can now build a power plant: the solar array names its mounts and includes PlanetInstallation and Station, where before it was the raw integer 1 (ship only) — while the ship mount it always had is preserved, so every existing ship design is unchanged.")]
        public void Solar_CanNowBeBuiltOnAColony_AndStillOnAShip()
        {
            var s = TestScenario.CreateWithColony();
            var templates = s.Faction.GetDataBlob<FactionInfoDB>().Data.ComponentTemplates;
            Assert.That(templates.ContainsKey("solarArray"), Is.True, "the solarArray template should be unlocked");

            var mounts = templates["solarArray"].MountType;
            Log($"solarArray mounts: {mounts}");

            Assert.That(mounts.HasFlag(ComponentMountType.PlanetInstallation), Is.True,
                "the one generator needing no fuel and no crew must be buildable on a colony — SustenanceProcessor already reads one");
            Assert.That(mounts.HasFlag(ComponentMountType.Station), Is.True,
                "and on a station, which is the same argument");

            // Byte-safety: five base-mod ship designs mount a panel. Adding flags must not take the ship mount away.
            Assert.That(mounts.HasFlag(ComponentMountType.ShipComponent), Is.True,
                "the ship mount it always had (the raw '1') must survive being spelled out");

            var solar = Design(s, Solar);
            Assert.That(solar.TryGetAttribute<EnergySolarGenerationAtb>(out var atb), Is.True,
                "and the shipped panel still binds its generation attribute unchanged");
            Log($"{solar.Name}: {atb.Area_m2:0} m² at {atb.BestEfficiency:0.###} best efficiency, crew {solar.CrewReq}");
        }

        /// <summary>
        /// The battery is not a generator and must not be judged as one — its justification is a different job
        /// entirely: buffering the burst demand a warp departure makes.
        /// </summary>
        [Test]
        [Description("The battery bank's justification is a different JOB, not a better number: it generates nothing, needs no crew and no fuel, and exists to hold the lump a warp departure demands all at once.")]
        public void TheBattery_IsADifferentJob_NotAWorseGenerator()
        {
            var s = TestScenario.CreateWithColony();
            var battery = Design(s, Battery);

            Assert.That(battery.TryGetAttribute<EnergyStoreAtb>(out var store), Is.True,
                "a battery bank stores energy");
            Assert.That(battery.TryGetAttribute<EnergyGenerationAtb>(out _), Is.False,
                "…and generates none, so it is never in competition with a reactor");
            Log($"{battery.Name}: {store.MaxStore:0} kJ on {battery.MassPerUnit:0} kg, crew {battery.CrewReq}");

            Assert.That(store.MaxStore, Is.GreaterThan(0));
            Assert.That(battery.CrewReq, Is.EqualTo(0), "nobody mans a battery");
        }
    }
}
