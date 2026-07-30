namespace Pulsar4X.Storage
{
    /// <summary>
    /// HOW MUCH ROOM A PERSON TAKES UP, by the kind of compartment they ride in.
    ///
    /// <para>People are the one cargo whose volume is not a property of the cargo — it is a property of <b>how you
    /// chose to carry them</b>. A berth in a passenger cabin is a bunk plus that person's share of air, water,
    /// corridor, galley and sick bay. A cryogenic pod is a pod. Those are not the same number, and the difference
    /// IS the decision the two compartments exist to offer: a cabin lands people who can walk out and go to work,
    /// a cryo bay lands five times as many who cannot.</para>
    ///
    /// <para>⚠ <b>Why this file exists at all.</b> <see cref="Pulsar4X.Technology.TeamObject"/> — the only thing in
    /// the game that was already declaring itself a passenger — reported
    /// <c>VolumePerUnit = 0.065 × teamSize</c>, i.e. <b>65 litres per person: the volume of a human body</b>, with no
    /// room for anything that keeps one alive. On that figure a 500 m³ cabin would berth over seven thousand people.
    /// Nothing had ever read it, because <b>no component in the game provided <c>passenger-storage</c></b>, so
    /// <c>CargoMath.GetFreeVolume</c> returned a silent 0 for every team and the number was never exercised.</para>
    ///
    /// <para>These are FLAGGED balance values, deliberately round: 10 m³ a berth, 2 m³ a pod, 100 kg a person with
    /// their kit. They are also the figures the descriptions of <c>passenger-cabin</c> and <c>cryo-bay</c> quote to
    /// the player, so a change here must change those two strings in the same commit.</para>
    /// </summary>
    public static class PassengerPacking
    {
        /// <summary>A living berth: bunk + that person's share of air, water, corridor, galley, sick bay. ⚠ FLAGGED.</summary>
        public const double BerthVolume_m3 = 10.0;

        /// <summary>A cryogenic pod + its share of the refrigeration plant — a fifth of a berth. ⚠ FLAGGED.</summary>
        public const double CryoPodVolume_m3 = 2.0;

        /// <summary>A person and their personal kit. ⚠ FLAGGED (this is the pre-existing TeamObject figure, kept).</summary>
        public const double MassPerPerson_kg = 100.0;

        /// <summary>The cargo type id of the frozen-people compartment (<c>cryo-bay</c> provides it).</summary>
        public const string CryogenicStorage = "cryogenic-storage";

        /// <summary>The cargo type id of the living-people compartment (<c>passenger-cabin</c> provides it).</summary>
        public const string PassengerStorage = "passenger-storage";

        /// <summary>
        /// Room one person needs in the given kind of compartment. Frozen people pack five times tighter; anything
        /// else is treated as a living berth, which is the conservative answer — a compartment that is not a cryo
        /// bay cannot be assumed to stack people.
        /// </summary>
        public static double VolumePerPerson(string cargoTypeID)
            => cargoTypeID == CryogenicStorage ? CryoPodVolume_m3 : BerthVolume_m3;
    }
}
