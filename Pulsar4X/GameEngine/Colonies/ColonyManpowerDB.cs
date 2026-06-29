using Newtonsoft.Json;
using System;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Colonies
{
    /// <summary>
    /// People as a finite, HARD-drawn resource (M3, docs/MORALE-AND-POPULATION-DESIGN.md). Tracks how much of a
    /// colony's population is committed to ships/posts so the engine can tell what is still AVAILABLE to draw.
    ///
    /// Two pools, both derived from population (not separately grown):
    ///   • BULK manpower  = population × WorkforceFraction  — crew, workers, rank-and-file (plentiful).
    ///   • TALENT         = population × TalentFraction      — officers, scientists, governors (scarce, trained).
    /// A draw COMMITS from a pool; available = pool − committed. Returns release the commitment; casualties
    /// subtract from population itself (handled where the loss happens). All fractions are NAMED COEFFICIENTS so
    /// a future GovernmentDB can re-skin them.
    ///
    /// Sub-slice 1 (this) is the foundation + math only — nothing enforces it yet; the construction/officer
    /// gates are sub-slice 2. It also fixes M2's employment denominator (jobs are measured against the
    /// workforce, not raw population).
    /// </summary>
    public class ColonyManpowerDB : BaseDataBlob
    {
        /// <summary>Fraction of population that is drawable workforce (working-age/able). Government-ready.</summary>
        public const double WorkforceFraction = 0.5;
        /// <summary>Fraction of population that is officer/scientist-caliber talent. Government-ready.</summary>
        public const double TalentFraction = 0.005;

        /// <summary>Bulk manpower currently committed to ships/posts (crew, workers).</summary>
        [JsonProperty] public long CommittedBulk { get; internal set; }
        /// <summary>Talent currently committed (officers, scientists, governors).</summary>
        [JsonProperty] public long CommittedTalent { get; internal set; }

        public ColonyManpowerDB() { }

        public ColonyManpowerDB(ColonyManpowerDB other)
        {
            CommittedBulk = other.CommittedBulk;
            CommittedTalent = other.CommittedTalent;
        }

        public override object Clone() => new ColonyManpowerDB(this);

        /// <summary>Total drawable bulk manpower for a given population.</summary>
        public static long Workforce(long population) => (long)(Math.Max(0L, population) * WorkforceFraction);

        /// <summary>Total talent (officer/scientist-caliber) for a given population.</summary>
        public static long TalentPool(long population) => (long)(Math.Max(0L, population) * TalentFraction);

        /// <summary>Bulk manpower still free to commit (workforce − committed, floored at 0).</summary>
        public long AvailableBulk(long population) => Math.Max(0L, Workforce(population) - CommittedBulk);

        /// <summary>Talent still free to commit (pool − committed, floored at 0).</summary>
        public long AvailableTalent(long population) => Math.Max(0L, TalentPool(population) - CommittedTalent);

        public bool CanCommitBulk(long population, long n) => n >= 0 && AvailableBulk(population) >= n;
        public bool CanCommitTalent(long population, long n) => n >= 0 && AvailableTalent(population) >= n;

        public void CommitBulk(long n) { if (n > 0) CommittedBulk += n; }
        public void ReleaseBulk(long n) { CommittedBulk -= n; if (CommittedBulk < 0) CommittedBulk = 0; }
        public void CommitTalent(long n) { if (n > 0) CommittedTalent += n; }
        public void ReleaseTalent(long n) { CommittedTalent -= n; if (CommittedTalent < 0) CommittedTalent = 0; }
    }
}
