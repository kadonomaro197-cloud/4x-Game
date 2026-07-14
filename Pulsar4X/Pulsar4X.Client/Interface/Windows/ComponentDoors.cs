using System.Collections.Generic;

namespace Pulsar4X.Client
{
    /// <summary>
    /// The Component Designer's ORGANIZATION — the 11 categories × 37 "doors" from the design-locked
    /// <c>docs/economy/COMPONENT-DESIGNER-CATEGORIES.md</c> (§2). The engine's component templates still carry a messy
    /// raw <c>ComponentType</c> ("Weapon" / "Facility" / "Augment" / "Energy Generator" / …); this maps each template
    /// to its intended <b>Category ▸ Door</b> so the "Make New" panel reads as the designed taxonomy — click
    /// <b>Weapons</b> → five doors (Energy / Ballistic / Melee / Guided / Exotic); a cargo hold and a fuel tank both
    /// live behind <b>Logistical ▸ Storage</b>; a hull is <b>Chassis ▸ Hull</b>.
    ///
    /// This is the WINDOW's view of the design; the parametric "laser/railgun are just dial settings" collapse is a
    /// later engine change. Anything not mapped falls back to an "Other" category keyed by its raw ComponentType, so a
    /// new/unmapped template still shows up (never disappears).
    /// </summary>
    internal static class ComponentDoors
    {
        /// <summary>The 11 categories, in display order.</summary>
        public static readonly string[] CategoryOrder =
        {
            "Weapons", "Propulsion", "Sensors", "Power", "Defense", "Enhancers",
            "Industrial", "Logistical", "Civic", "Command", "Chassis", "Other"
        };

        /// <summary>Door display order within each category (doors not listed sort after, alphabetically).</summary>
        public static readonly Dictionary<string, string[]> DoorOrder = new()
        {
            ["Weapons"]    = new[] { "Energy", "Ballistic", "Melee", "Guided", "Exotic" },
            ["Propulsion"] = new[] { "Reaction", "Traction", "Fluid", "Warp", "Exotic" },
            ["Sensors"]    = new[] { "Detection", "Survey", "Fire Control", "Electronic Warfare" },
            ["Power"]      = new[] { "Generation", "Storage" },
            ["Defense"]    = new[] { "Armor", "Shields", "Hardening", "Fortification" },
            ["Enhancers"]  = new[] { "Bio-augmentation", "Training/Doctrine", "Systems" },
            ["Industrial"] = new[] { "Extraction", "Fabrication" },
            ["Logistical"] = new[] { "Storage", "Transfer" },
            ["Civic"]      = new[] { "Habitation", "Development" },
            ["Command"]    = new[] { "Command" },
            ["Chassis"]    = new[] { "Personnel", "Vehicle", "Hull", "Structure", "Mega", "Prebuilt Units" },
        };

        // Template id → (Category, Door). Built from the design doc's §2 "Absorbs (old templates → gone)" column.
        private static readonly Dictionary<string, (string cat, string door)> Map = new()
        {
            // 1. Weapons
            ["laser-weapon"] = ("Weapons", "Energy"),
            ["pulse-laser"] = ("Weapons", "Energy"),
            ["energy-weapon"] = ("Weapons", "Energy"),
            ["plasma-repeater"] = ("Weapons", "Energy"),
            ["ground-rifle"] = ("Weapons", "Ballistic"),
            ["ground-cannon"] = ("Weapons", "Ballistic"),
            ["ground-autocannon"] = ("Weapons", "Ballistic"),
            ["railgun-weapon"] = ("Weapons", "Ballistic"),
            ["siege-railgun"] = ("Weapons", "Ballistic"),
            ["flak-weapon"] = ("Weapons", "Ballistic"),
            ["point-defense-mount"] = ("Weapons", "Ballistic"),
            ["claw-weapon"] = ("Weapons", "Melee"),
            ["missile-launcher"] = ("Weapons", "Guided"),
            ["missile-payload"] = ("Weapons", "Guided"),
            ["disruptor-weapon"] = ("Weapons", "Exotic"),

            // 2. Propulsion
            ["conventional-engine"] = ("Propulsion", "Reaction"),
            ["scntr-engine"] = ("Propulsion", "Reaction"),
            ["missile-srb"] = ("Propulsion", "Reaction"),
            ["ground-locomotion"] = ("Propulsion", "Traction"),
            ["alcubierre-warp-drive"] = ("Propulsion", "Warp"),
            ["reactionless-drive"] = ("Propulsion", "Exotic"),
            ["inertialess-drive"] = ("Propulsion", "Exotic"),

            // 3. Sensors
            ["passive-sensor"] = ("Sensors", "Detection"),
            ["ground-radar"] = ("Sensors", "Detection"),
            ["missile-electronics-suite"] = ("Sensors", "Detection"),
            ["geo-surveyor"] = ("Sensors", "Survey"),
            ["gravitational-surveyor"] = ("Sensors", "Survey"),
            ["beam-fire-control"] = ("Sensors", "Fire Control"),
            ["pd-director"] = ("Sensors", "Fire Control"),
            ["cloak-device"] = ("Sensors", "Electronic Warfare"),
            ["jammer"] = ("Sensors", "Electronic Warfare"),

            // 4. Power
            ["reactor"] = ("Power", "Generation"),
            ["rtg"] = ("Power", "Generation"),
            ["steam-turbine-reactor"] = ("Power", "Generation"),
            ["solarArray"] = ("Power", "Generation"),
            ["battery-bank"] = ("Power", "Storage"),

            // 5. Defense
            ["ground-plating"] = ("Defense", "Armor"),
            ["ablative-plating"] = ("Defense", "Armor"),
            ["reactive-plating"] = ("Defense", "Armor"),
            ["deflector-array"] = ("Defense", "Shields"),
            ["shield-generator"] = ("Defense", "Shields"),
            ["armour-hardening"] = ("Defense", "Hardening"),
            ["sensor-hardening-module"] = ("Defense", "Hardening"),
            ["drive-reinforcement"] = ("Defense", "Hardening"),
            ["warp-stabilizer"] = ("Defense", "Hardening"),
            ["heat-radiator"] = ("Defense", "Hardening"),
            ["bunker"] = ("Defense", "Fortification"),
            ["ward-projector"] = ("Defense", "Fortification"),

            // 6. Enhancers
            ["power-armor"] = ("Enhancers", "Bio-augmentation"),
            ["reflex-booster"] = ("Enhancers", "Bio-augmentation"),
            ["unit-caliber"] = ("Enhancers", "Training/Doctrine"),
            ["crew-automation"] = ("Enhancers", "Systems"),

            // 7. Industrial
            ["mine"] = ("Industrial", "Extraction"),
            ["automine"] = ("Industrial", "Extraction"),
            ["factory"] = ("Industrial", "Fabrication"),
            ["refinery"] = ("Industrial", "Fabrication"),
            ["shipyard"] = ("Industrial", "Fabrication"),
            ["local-construction"] = ("Industrial", "Fabrication"),
            ["launch-complex"] = ("Industrial", "Fabrication"),

            // 8. Logistical
            ["general-cargo-hold"] = ("Logistical", "Storage"),
            ["cargo-Shuttlebay"] = ("Logistical", "Storage"),
            ["warehouse-facility"] = ("Logistical", "Storage"),
            ["fuel-cargo-hold"] = ("Logistical", "Storage"),
            ["ordnance-cargo-hold"] = ("Logistical", "Storage"),
            ["troop-bay"] = ("Logistical", "Storage"),
            ["ground-magazine"] = ("Logistical", "Storage"),
            ["ship-magazine"] = ("Logistical", "Storage"),
            ["spaceport"] = ("Logistical", "Transfer"),
            ["space-port"] = ("Logistical", "Transfer"),
            ["logistics-office"] = ("Logistical", "Transfer"),

            // 9. Civic
            ["infrastructure"] = ("Civic", "Habitation"),
            ["space-habitat"] = ("Civic", "Habitation"),
            ["research-lab"] = ("Civic", "Development"),
            ["research-academy"] = ("Civic", "Development"),
            ["naval-academy"] = ("Civic", "Development"),

            // 10. Command
            ["admin-complex"] = ("Command", "Command"),
            ["ship-command"] = ("Command", "Command"),
            ["command-berth"] = ("Command", "Command"),
            ["intelligence-directorate"] = ("Command", "Command"),

            // 11. Chassis
            ["human-frame"] = ("Chassis", "Personnel"),
            ["swarm-frame"] = ("Chassis", "Personnel"),
            ["vehicle-frame"] = ("Chassis", "Vehicle"),
            ["walker-frame"] = ("Chassis", "Vehicle"),
            ["ship-hull"] = ("Chassis", "Hull"),
            // The whole-unit shortcuts the design doc marks for eventual removal — grouped so they're visible but set apart.
            ["infantry-unit"] = ("Chassis", "Prebuilt Units"),
            ["armor-unit"] = ("Chassis", "Prebuilt Units"),
            ["artillery-unit"] = ("Chassis", "Prebuilt Units"),
        };

        /// <summary>
        /// The (Category, Door) a template belongs in. Falls back to the "Other" category keyed by the template's raw
        /// <paramref name="componentType"/> (or "Misc" when that's empty), so an unmapped template still appears.
        /// </summary>
        public static (string category, string door) Classify(string templateId, string componentType)
        {
            if (templateId != null && Map.TryGetValue(templateId, out var hit))
                return hit;
            return ("Other", string.IsNullOrWhiteSpace(componentType) ? "Misc" : componentType);
        }

        /// <summary>Door sort key within a category — listed doors first (in order), then unlisted doors alphabetically.</summary>
        public static int DoorRank(string category, string door)
        {
            if (DoorOrder.TryGetValue(category, out var doors))
            {
                for (int i = 0; i < doors.Length; i++)
                    if (doors[i] == door) return i;
            }
            return 1000;   // unlisted door → after the named ones
        }
    }
}
