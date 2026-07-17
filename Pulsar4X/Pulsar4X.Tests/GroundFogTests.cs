using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Galaxy;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Ground fog of war — slice 1 (`docs/ground/SURFACE-FOG-AND-RECON-DESIGN.md`, path A: build the per-faction ground
    /// fog FIRST, as the shared foundation under both the two-tier survey and an honest "easiest landing" score).
    ///
    /// This gauges the per-faction REGION reveal model + API on `PlanetRegionsDB`:
    ///   • a region revealed to one faction is NOT revealed to another (real fog — the thing that did not exist:
    ///     `Region.Surveyed` is one world-level, faction-agnostic bool),
    ///   • the world-level `Surveyed` flag is left untouched (ADDITIVE / byte-identical for the old consumers), and
    ///   • the reveal survives a `Clone()` as an INDEPENDENT deep copy (the save-safety proxy — the copy ctor is exactly
    ///     what save/load uses).
    /// Nothing consumes this layer yet; later slices wire the space survey (reveal geography + deposit location/type),
    /// the scout's reveal-on-move (unmask the assay), and the fog-limited enemy-garrison read.
    /// </summary>
    [TestFixture]
    public class GroundFogTests
    {
        private static PlanetRegionsDB FourRegions()
        {
            var regions = new List<Region>();
            for (int i = 0; i < 4; i++) regions.Add(new Region { Index = i });
            return new PlanetRegionsDB(regions);
        }

        [Test]
        [Description("Per-faction region reveal is fogged per-faction, doesn't touch the world-level Surveyed flag, is bounds-safe, and survives a clone as an independent copy.")]
        public void PerFactionReveal_IsFoggedPerFaction_AdditiveAndCloneSafe()
        {
            var db = FourRegions();
            const int A = 10, B = 20;

            // Full fog by default — nobody has revealed anything.
            Assert.That(db.IsRegionRevealedFor(A, 1), Is.False, "an un-scouted region must read fogged");

            // Reveal region 1 to faction A ONLY.
            Assert.That(db.RevealRegionFor(A, 1), Is.True, "the first reveal is a change");
            Assert.That(db.RevealRegionFor(A, 1), Is.False, "re-revealing is idempotent (no change)");
            Assert.That(db.IsRegionRevealedFor(A, 1), Is.True, "A revealed region 1");
            Assert.That(db.IsRegionRevealedFor(B, 1), Is.False, "B has NOT — fog is PER-FACTION (the whole point)");
            Assert.That(db.IsRegionRevealedFor(A, 0), Is.False, "A hasn't revealed region 0");

            // ADDITIVE: the world-level flag is untouched, so the old world-level consumers are byte-identical.
            Assert.That(db.Regions[1].Surveyed, Is.False, "per-faction reveal must NOT flip the world-level Surveyed flag");

            // Bounds-safe.
            Assert.That(db.RevealRegionFor(A, 99), Is.False, "out-of-range reveal is a no-op");
            Assert.That(db.IsRegionRevealedFor(A, 99), Is.False, "out-of-range read is false, not a throw");

            // Reveal-all-for-one-faction (a completed space survey reveals the world to the surveying faction only).
            Assert.That(db.RevealAllRegionsFor(B), Is.True);
            for (int i = 0; i < 4; i++)
                Assert.That(db.IsRegionRevealedFor(B, i), Is.True, "B sees every region after RevealAllRegionsFor");
            Assert.That(db.IsRegionRevealedFor(A, 2), Is.False, "A is still fogged where it never scouted");

            // Survives a clone (the save/load proxy) as a DEEP, INDEPENDENT copy.
            var clone = (PlanetRegionsDB)db.Clone();
            Assert.That(clone.IsRegionRevealedFor(A, 1), Is.True, "the clone preserves A's reveal");
            Assert.That(clone.IsRegionRevealedFor(B, 3), Is.True, "the clone preserves B's reveal");
            clone.RevealRegionFor(A, 0);
            Assert.That(db.IsRegionRevealedFor(A, 0), Is.False,
                "the clone is INDEPENDENT — mutating it must not touch the original (else save/load would alias)");
        }
    }
}
