using Newtonsoft.Json;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;

namespace Pulsar4X.Sites
{
    /// <summary>
    /// Site Engine SE-2a — the component that MAKES a Command Berth buildable, the Site-Engine twin of
    /// <see cref="Pulsar4X.People.ResearchAcademyAtb"/> / <c>IntelDirectorateAtb</c>. Installing a berth on a host (ship,
    /// station, or colony) adds a <see cref="CommandBerth"/> capability to its <see cref="CommandBerthDB"/> roster
    /// (seeding the blob if the host has none). This is the reach half of the berth's cradle-to-grave:
    /// mineral → material → THIS component → the build decision → (SE-2b) a leader seated in it working a site; and
    /// uninstalling/destroying it removes that berth (the grave rung — the last berth torn down drops the roster).
    /// </summary>
    public class CommandBerthAtb : IComponentDesignAttribute
    {
        // [JsonProperty] + parameterless ctor: this atb is serialized INSIDE the command-berth ComponentDesign (the
        // design is stored on the host), so Game.Load needs a default ctor to deserialize it — without one it throws
        // "Unable to find a constructor to use" (the ResearchAcademyAtb / IntelDirectorateAtb save/load lesson).
        [JsonProperty] public SiteRole Role { get; internal set; }
        [JsonProperty] public int Grade { get; internal set; }
        [JsonProperty] public int Support { get; internal set; }
        [JsonProperty] public int Survivability { get; internal set; }
        [JsonProperty] public int Span { get; internal set; }

        public CommandBerthAtb() { }

        // roleIndex arrives as an int from the designer's GuiEnumSelectionList (like AdminSpaceAtb's Admin Level); the
        // remaining dials may arrive as doubles or ints from NCalc — provide both overloads so the ComponentDesigner's
        // ctor match never misses (the ResearchAcademyAtb double/int hedge).
        public CommandBerthAtb(int roleIndex, double grade, double support, double survivability, double span)
        {
            Role = (SiteRole)roleIndex;
            Grade = (int)grade;
            Support = (int)support;
            Survivability = (int)survivability;
            Span = (int)span;
        }

        public CommandBerthAtb(int roleIndex, int grade, int support, int survivability, int span)
        {
            Role = (SiteRole)roleIndex;
            Grade = grade;
            Support = support;
            Survivability = survivability;
            Span = span;
        }

        public CommandBerthAtb(CommandBerthAtb other)
        {
            Role = other.Role;
            Grade = other.Grade;
            Support = other.Support;
            Survivability = other.Survivability;
            Span = other.Span;
        }

        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance)
        {
            var berth = new CommandBerth
            {
                Role = Role,
                Grade = Grade,
                Support = Support,
                Survivability = Survivability,
                Span = Span,
                ComponentName = componentInstance?.Name ?? ""
            };

            if (parentEntity.TryGetDataBlob<CommandBerthDB>(out var roster))
            {
                roster.Berths.Add(berth);
            }
            else
            {
                roster = new CommandBerthDB();
                roster.Berths.Add(berth);
                parentEntity.SetDataBlob(roster);
            }
        }

        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance)
        {
            if (!parentEntity.TryGetDataBlob<CommandBerthDB>(out var roster))
                return;

            // Remove THIS component's berth by name (the uninstall hook fires before the component leaves
            // ComponentInstancesDB, and matching by name survives save/load — unlike a held reference).
            string name = componentInstance?.Name ?? "";
            roster.Berths.RemoveAll(b => b.ComponentName == name);

            // The last berth torn down leaves no roster (the grave rung).
            if (roster.Berths.Count == 0)
                parentEntity.RemoveDataBlob<CommandBerthDB>();
        }

        public string AtbName() => "Command Berth";

        public string AtbDescription() =>
            "Role: " + Role +
            "\nGrade: " + Grade +
            "\nSupport: " + Support +
            "\nSurvivability: " + Survivability +
            "\nSpan: " + Span;
    }
}
