using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Sites
{
    /// <summary>
    /// Site Engine SE-4b — the MENACE factory (docs/SITE-ENGINE-DESIGN.md §4, the incident's hostile force). An
    /// incident site (an Europa-style outbreak) is held by a "menace" — a hostile force that ISN'T a normal empire:
    /// its own faction, raised as ground units standing on the site's region. Standing there, it already trips the
    /// SE-3d guardian gate (`SiteWorkProcessor.RegionIsClearFor`), so you must CLEAR it before you can contain the
    /// site — the "hold-while-you-work" coupling. Mirrors <c>GroundCombat.GroundStartGarrison</c> (CreateBasicFaction
    /// + RaiseUnit from a throwaway C# design), but for a menace instead of a home garrison.
    ///
    /// Nothing in the live New-Game path calls this yet, so the engine is byte-identical (no menace faction is created
    /// until an incident-discovery slice, SE-4e, spawns one).
    /// </summary>
    public static class MenaceFactory
    {
        /// <summary>Menace units are a shade tougher than a line garrison — a "monster", not a soldier. Flagged numbers.</summary>
        private static GroundUnitDesign MakeMenaceDesign() => new GroundUnitDesign
        {
            UniqueID = "menace-swarm",
            Name = "Menace",
            UnitType = GroundUnitType.Infantry,
            Attack = 120,
            Defense = 12,
            HitPoints = 600,
        };

        /// <summary>
        /// Stand up a hostile MENACE force at <paramref name="body"/>'s region <paramref name="regionIndex"/>: create a
        /// dedicated menace faction (no funds, no ships) and raise <paramref name="unitCount"/> hostile ground units on
        /// the region. Returns the menace faction entity (its id goes on the incident site's
        /// <see cref="FieldSiteDB.MenaceFactionId"/>). Defensive — a null game/body yields <see cref="Entity.InvalidEntity"/>.
        /// </summary>
        public static Entity RaiseMenaceAt(Game game, Entity body, int regionIndex, string name = "Menace", int unitCount = 3)
        {
            if (game == null || body == null || !body.IsValid) return Entity.InvalidEntity;

            var faction = FactionFactory.CreateBasicFaction(game, name, "MEN", 0);
            var design = MakeMenaceDesign();
            for (int i = 0; i < unitCount; i++)
                GroundForces.RaiseUnit(body, design, faction.Id, regionIndex, $"{name} {i + 1}");

            return faction;
        }
    }
}
