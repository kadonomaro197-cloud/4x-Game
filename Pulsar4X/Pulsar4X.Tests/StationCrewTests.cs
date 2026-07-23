using NUnit.Framework;
using Pulsar4X.Colonies;
using Pulsar4X.Stations;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Crew is functional OFF-WORLD — the two factions that exist are a PLANET faction (humans, crew from colony
    /// population) and a STATION faction (the Kithrin Collective, 6M residents on the Titan station). Before this,
    /// the crew gate (<see cref="ManpowerTools"/> + <see cref="ColonyManpowerDB"/>) only knew about
    /// <c>ColonyInfoDB</c>, so a station-dwelling faction built EVERYTHING crew-free (the pool was never attached
    /// and the population was never read). Now a MANNED station draws crew from its own residents exactly like a
    /// colony, while an UNMANNED automated platform stays inert (crew-free, byte-identical — it has no population
    /// to man a warship with). Humans (planet colonies) are untouched: the <c>ColonyInfoDB</c> path is unchanged.
    /// </summary>
    [TestFixture]
    internal class StationCrewTests
    {
        [Test]
        [Description("A manned station gets a manpower pool from its population and the crew gate DRAWS from it " +
                     "(proving it isn't the inert path); an unmanned station has no pool and stays inert.")]
        public void MannedStation_IsCrewGatedFromItsPopulation_UnmannedStaysInert()
        {
            var s = TestScenario.CreateWithColony();

            // MANNED station — the Kithrin Titan case: residents live ON the station, not a planet.
            var manned = StationFactory.CreateStation(s.Faction, s.StartingBody, 6_000_000, s.Species);
            Assert.That(manned.HasDataBlob<ColonyManpowerDB>(), Is.True,
                "a manned station gets a manpower pool (crew works off-world)");

            // ManpowerTools now reads StationInfoDB.Population, so the crew gate is LIVE, not inert:
            var decision = ManpowerTools.ResolveBuild(manned, 100);
            Assert.That(decision.CanBuild, Is.True, "6M residents -> 3M workforce easily covers 100 crew");
            Assert.That(decision.CrewToCommit, Is.EqualTo(100),
                "the gate DRAWS 100 crew from the station's own residents (proves it isn't the inert host path)");

            ManpowerTools.CommitCrew(manned, 100);
            Assert.That(manned.GetDataBlob<ColonyManpowerDB>().CommittedBulk, Is.EqualTo(100),
                "committing crew reduces the station's available pool");

            // UNMANNED automated platform — no residents, no pool => the gate stays inert (byte-identical).
            var automated = StationFactory.CreateStation(s.Faction, s.StartingBody);
            Assert.That(automated.HasDataBlob<ColonyManpowerDB>(), Is.False,
                "an unmanned automated station gets no manpower pool");
            var inert = ManpowerTools.ResolveBuild(automated, 100);
            Assert.That(inert.CanBuild, Is.True, "an unenforced host always builds");
            Assert.That(inert.CrewToCommit, Is.EqualTo(0), "and commits nothing (the inert path)");
        }
    }
}
