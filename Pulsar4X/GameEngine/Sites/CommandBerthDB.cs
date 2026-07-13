using System.Collections.Generic;
using Newtonsoft.Json;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Sites
{
    /// <summary>
    /// Site Engine SE-2a — one installed Command Berth's CAPABILITY (the "gear", not the occupant). A berth is the
    /// seat where a leader works a field-site; this record is what a <see cref="CommandBerthAtb"/> stamps onto its host
    /// when built. Occupancy (which commander is seated) is deliberately NOT here — that is the seat mechanism's job
    /// (SE-2b), reusing the existing admin-seat plumbing rather than a parallel one. The dials (design §5):
    ///   • <see cref="Role"/> — which <see cref="SiteRole"/> this berth can work (a Science berth works a Science site).
    ///   • <see cref="Grade"/> — the berth's quality tier; scales how fast the site is worked (SE-2b).
    ///   • <see cref="Support"/> — a flat competence boost to the seated leader (SE-2b).
    ///   • <see cref="Survivability"/> — reduces the seated leader's incident/death risk at a dangerous site (SE-2c).
    ///   • <see cref="Span"/> — the force size the berth can command (SE-3+, the old ConsoleSpace idea with teeth).
    /// </summary>
    public class CommandBerth
    {
        [JsonProperty] public SiteRole Role { get; set; } = SiteRole.Science;
        [JsonProperty] public int Grade { get; set; }
        [JsonProperty] public int Support { get; set; }
        [JsonProperty] public int Survivability { get; set; }
        [JsonProperty] public int Span { get; set; }

        /// <summary>The installing component's name — the identity key used to remove the right berth on uninstall
        /// (the same key the admin seat reconcile uses), so it survives save/load.</summary>
        [JsonProperty] public string ComponentName { get; set; } = "";

        /// <summary>The entity id of the leader seated in this berth (SE-2b), or -1 if empty. A seated leader is what
        /// makes the berth work a site faster; the leader's own back-reference (`CommanderDB.AssignedTo`) points at the
        /// host, the same shared convention the admin/scientist assignments use.</summary>
        [JsonProperty] public int CommanderID { get; set; } = -1;

        public bool IsOccupied => CommanderID >= 0;

        public CommandBerth() { }

        public CommandBerth(CommandBerth other)
        {
            Role = other.Role;
            Grade = other.Grade;
            Support = other.Support;
            Survivability = other.Survivability;
            Span = other.Span;
            ComponentName = other.ComponentName;
            CommanderID = other.CommanderID;
        }
    }

    /// <summary>
    /// Site Engine SE-2a — the roster of Command Berths installed on a host (a ship, station, or colony). The ability
    /// blob a <see cref="CommandBerthAtb"/> seeds/grows on install and withdraws on uninstall (the geo-survey /
    /// research-academy / intel-directorate ability-blob pattern — here holding a LIST, like
    /// <c>ResearchAcademyDB.Academies</c>, because one host can carry several berths of different Roles, e.g. an
    /// Enterprise-D). This is the "build the gear" rung of the berth's cradle-to-grave; nothing READS it yet, so it is
    /// byte-identical / additive (no host carries the blob until a berth is built). SE-2b seats a leader and sources the
    /// site work rate from the best matching berth's Grade + the leader's competence.
    /// </summary>
    public class CommandBerthDB : BaseDataBlob
    {
        [JsonProperty] public List<CommandBerth> Berths { get; private set; } = new List<CommandBerth>();

        public CommandBerthDB() { }

        public CommandBerthDB(CommandBerthDB other)
        {
            Berths = new List<CommandBerth>();
            foreach (var berth in other.Berths)
                Berths.Add(new CommandBerth(berth));
        }

        /// <summary>The best (highest-Grade) berth on this host that can work <paramref name="role"/>, or null if none —
        /// the read SE-2b's presence/work-rate logic uses. Pure.</summary>
        public CommandBerth BestBerthFor(SiteRole role) => Best(role, requireOccupied: false, requireEmpty: false);

        /// <summary>The best (highest-Grade) OCCUPIED berth matching <paramref name="role"/> — the one whose seated
        /// leader actually works the site (SE-2b), or null if none is manned. Pure.</summary>
        public CommandBerth BestOccupiedBerthFor(SiteRole role) => Best(role, requireOccupied: true, requireEmpty: false);

        /// <summary>The best (highest-Grade) EMPTY berth matching <paramref name="role"/> — where a new leader is seated
        /// (SE-2b), or null if every matching berth is already manned. Pure.</summary>
        public CommandBerth BestEmptyBerthFor(SiteRole role) => Best(role, requireOccupied: false, requireEmpty: true);

        /// <summary>The berth currently holding <paramref name="commanderId"/>, or null — used to vacate on reassignment
        /// or death. Pure.</summary>
        public CommandBerth FindBerthOfCommander(int commanderId)
        {
            if (commanderId < 0) return null;
            foreach (var berth in Berths)
                if (berth.CommanderID == commanderId) return berth;
            return null;
        }

        private CommandBerth Best(SiteRole role, bool requireOccupied, bool requireEmpty)
        {
            CommandBerth best = null;
            foreach (var berth in Berths)
            {
                if (berth.Role != role) continue;
                if (requireOccupied && !berth.IsOccupied) continue;
                if (requireEmpty && berth.IsOccupied) continue;
                if (best == null || berth.Grade > best.Grade)
                    best = berth;
            }
            return best;
        }

        public override object Clone() => new CommandBerthDB(this);
    }
}
