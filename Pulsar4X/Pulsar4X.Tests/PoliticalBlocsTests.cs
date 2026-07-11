using NUnit.Framework;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// F-C2a gauge (docs/GOVERNMENT-AND-POLITICS-DESIGN.md — popular-demands): the bloc substrate. Proves a neutral
    /// (all-Mid) regime makes every bloc equally loud, and each government dial amplifies exactly its favoured bloc
    /// (Militarism→Militarists, Openness→Liberty, Authority→Order) while the economically-driven blocs (Labor,
    /// Merchants) stay dial-neutral. Pure/derived → byte-identical.
    /// </summary>
    [TestFixture]
    public class PoliticalBlocsTests
    {
        [Test]
        [Description("Bloc loudness is 1.0 under a neutral regime; each dial at High/Low amplifies/damps its own bloc.")]
        public void Loudness_TracksTheGovernmentDials()
        {
            var gov = new GovernmentDB(); // all Mid → every bloc baseline

            foreach (var bloc in PoliticalBlocs.All)
                Assert.That(PoliticalBlocs.Loudness(gov, bloc), Is.EqualTo(1.0).Within(1e-9),
                    $"a neutral regime makes {bloc} baseline-loud");

            gov.Militarism = GovNotch.High;
            Assert.That(PoliticalBlocs.Loudness(gov, PoliticalBloc.Militarists), Is.EqualTo(1.5).Within(1e-9),
                "a militarist regime amplifies the Militarists");

            gov.Authority = GovNotch.High;
            Assert.That(PoliticalBlocs.Loudness(gov, PoliticalBloc.Order), Is.EqualTo(1.5).Within(1e-9),
                "an authoritarian regime amplifies Order");

            gov.Openness = GovNotch.Low;
            Assert.That(PoliticalBlocs.Loudness(gov, PoliticalBloc.Liberty), Is.EqualTo(0.5).Within(1e-9),
                "a closed regime damps Liberty");

            // The economically-driven blocs don't move with these dials (they read economic state instead).
            Assert.That(PoliticalBlocs.Loudness(gov, PoliticalBloc.Labor), Is.EqualTo(1.0).Within(1e-9));
            Assert.That(PoliticalBlocs.Loudness(gov, PoliticalBloc.Merchants), Is.EqualTo(1.0).Within(1e-9));

            // Null-safe (a faction with no government blob reads baseline).
            Assert.That(PoliticalBlocs.Loudness(null, PoliticalBloc.Order), Is.EqualTo(1.0).Within(1e-9));
        }
    }
}
