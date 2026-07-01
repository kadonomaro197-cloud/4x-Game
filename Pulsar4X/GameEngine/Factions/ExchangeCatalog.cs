using System.Collections.Generic;
using System.Linq;

namespace Pulsar4X.Factions
{
    /// <summary>The seven families of things two factions can exchange (docs/DIPLOMACY-DESIGN.md exchange catalog).</summary>
    public enum ExchangeCategory { Economic, Military, Information, Territorial, Political, People, Coercive }

    /// <summary>How an exchange lands: a one-off transfer, a standing commitment that emits orders each cycle, a
    /// one-shot event, or a persistent state change.</summary>
    public enum ExchangeKind { Instant, Standing, Event, State }

    /// <summary>The existing engine system an exchange routes INTO — so the catalog IS the connection map: every
    /// row is a wire into a system that already exists (or is named in the blast radius). The commitment model
    /// (#35) uses this to translate an accepted exchange into a real order/transfer in that system.</summary>
    public enum ExchangeRoute
    {
        Ledger,        // cross-faction money transfer
        Logistics,     // cargo order / standing supply route
        Fleets,        // fleet move / transfer of ownership
        CombatIFF,     // stance/pact affecting who shoots whom
        Sensors,       // shared contacts / fog
        Research,      // tech transfer
        Movement,      // transit/access rights
        GroundCombat,  // loaned troops / invasion support
        People,        // commanders/specialists/ambassadors/prisoners
        Espionage,     // intel goods / spy pacts
        Galaxy,        // colony claims / ownership transfer
        DiplomacyDB    // pure relationship state (recognition, apology, ultimatum)
    }

    /// <summary>One row of the exchange catalog: what it is, its family, whether it's instant/standing, and which
    /// system it drives. Deliberately data (not behavior) — the "build it BROAD and data-driven" lock.</summary>
    public readonly struct ExchangeDef
    {
        public readonly string Key;
        public readonly ExchangeCategory Category;
        public readonly ExchangeKind Kind;
        public readonly ExchangeRoute Route;
        public readonly string Description;

        public ExchangeDef(string key, ExchangeCategory cat, ExchangeKind kind, ExchangeRoute route, string desc)
        {
            Key = key; Category = cat; Kind = kind; Route = route; Description = desc;
        }
    }

    /// <summary>
    /// The exchange catalog (task #35) — everything two factions can trade, and which system each routes into. The
    /// developer's depth principle: the more that can be exchanged, the deeper diplomacy gets. This is a broad,
    /// representative in-code catalog covering all seven categories and every routing system; it is meant to GROW
    /// (a new row is cheap — each is a transfer or a standing commitment emitting an order into a system that
    /// already exists), and can move to JSON later like the combat-doctrine catalog did. This slice is the DATA;
    /// the commitment model that executes a chosen exchange (Instant → transfer now, Standing → emit orders each
    /// cycle, with promised-vs-delivered tracked) is the next step. Nothing consumes it yet.
    /// </summary>
    public static class ExchangeCatalog
    {
        public static readonly IReadOnlyList<ExchangeDef> All = new List<ExchangeDef>
        {
            // Economic
            new("gift-payment",        ExchangeCategory.Economic,    ExchangeKind.Instant,  ExchangeRoute.Ledger,     "Lump-sum payment / gift — buy goodwill, seal a deal"),
            new("subsidy",             ExchangeCategory.Economic,    ExchangeKind.Standing, ExchangeRoute.Ledger,     "Recurring subsidy — prop up a client"),
            new("tribute",             ExchangeCategory.Economic,    ExchangeKind.Standing, ExchangeRoute.Ledger,     "Tribute / vassal payment — the strong bleed the weak"),
            new("trade-agreement",     ExchangeCategory.Economic,    ExchangeKind.Standing, ExchangeRoute.Logistics,  "Open commerce + tariff terms — mutual profit"),
            new("one-time-cargo",      ExchangeCategory.Economic,    ExchangeKind.Instant,  ExchangeRoute.Logistics,  "One-time cargo (minerals/fuel/ordnance) — cover a shortage"),
            new("standing-supply",     ExchangeCategory.Economic,    ExchangeKind.Standing, ExchangeRoute.Logistics,  "Standing supply line — sustained shortage"),
            new("sell-fleet",          ExchangeCategory.Economic,    ExchangeKind.Instant,  ExchangeRoute.Fleets,     "Sell/gift a ship or fleet — offload hulls / arm a proxy"),

            // Military
            new("station-defense",     ExchangeCategory.Military,    ExchangeKind.Standing, ExchangeRoute.Fleets,     "Station a defense fleet at their world — answer a threat"),
            new("mercenary-fleet",     ExchangeCategory.Military,    ExchangeKind.Standing, ExchangeRoute.Fleets,     "Hire a mercenary fleet — force without a pact"),
            new("loan-troops",         ExchangeCategory.Military,    ExchangeKind.Standing, ExchangeRoute.GroundCombat,"Loan ground troops — help take or hold a planet"),
            new("defensive-pact",      ExchangeCategory.Military,    ExchangeKind.State,    ExchangeRoute.CombatIFF,  "Defensive pact — auto-join if attacked"),
            new("non-aggression",      ExchangeCategory.Military,    ExchangeKind.State,    ExchangeRoute.CombatIFF,  "Non-aggression pact — buy a quiet border"),
            new("peace",               ExchangeCategory.Military,    ExchangeKind.State,    ExchangeRoute.CombatIFF,  "Ceasefire / peace — end a war"),
            new("military-access",     ExchangeCategory.Military,    ExchangeKind.Standing, ExchangeRoute.Movement,   "Military transit rights — move through their space"),

            // Information
            new("share-sensors",       ExchangeCategory.Information,  ExchangeKind.Standing, ExchangeRoute.Sensors,    "Sensor/contact-data sharing — shared fog"),
            new("star-charts",         ExchangeCategory.Information,  ExchangeKind.Instant,  ExchangeRoute.Sensors,    "Star charts / jump maps — reveal the map"),
            new("tech-exchange",       ExchangeCategory.Information,  ExchangeKind.Instant,  ExchangeRoute.Research,   "Technology gift/sale — leap a research gap"),
            new("sell-intel",          ExchangeCategory.Information,  ExchangeKind.Instant,  ExchangeRoute.Espionage,  "Sell a third party's secret — intel as a good"),

            // Territorial
            new("logi-base-access",    ExchangeCategory.Territorial, ExchangeKind.Standing, ExchangeRoute.Logistics,  "Logistics-base access — use each other's depots"),
            new("gate-transit",        ExchangeCategory.Territorial, ExchangeKind.Standing, ExchangeRoute.Movement,   "Jump-gate transit grant — control the chokepoint"),
            new("cede-colony",         ExchangeCategory.Territorial, ExchangeKind.Instant,  ExchangeRoute.Galaxy,     "Cede a colony or station — spoils, or a desperate trade"),

            // Political
            new("recognition",         ExchangeCategory.Political,   ExchangeKind.State,    ExchangeRoute.DiplomacyDB,"Open relations / recognition"),
            new("ultimatum",           ExchangeCategory.Political,   ExchangeKind.Event,    ExchangeRoute.DiplomacyDB,"Ultimatum (give me X or war) — coercion"),
            new("apology",             ExchangeCategory.Political,   ExchangeKind.Instant,  ExchangeRoute.DiplomacyDB,"Apology / reparation — cool a grudge"),

            // People
            new("extradite",           ExchangeCategory.People,      ExchangeKind.Instant,  ExchangeRoute.People,     "Extradite a defector / hand over a prisoner"),
            new("loan-specialist",     ExchangeCategory.People,      ExchangeKind.Standing, ExchangeRoute.People,     "Loan a scientist/admiral — rent talent"),

            // Coercive
            new("sanctions",           ExchangeCategory.Coercive,    ExchangeKind.State,    ExchangeRoute.Logistics,  "Sanctions — cut trade, pressure without war"),
        };

        /// <summary>All catalog entries in a family.</summary>
        public static IEnumerable<ExchangeDef> ByCategory(ExchangeCategory cat) => All.Where(e => e.Category == cat);

        /// <summary>Look up one entry by its stable key (null if absent).</summary>
        public static ExchangeDef? ByKey(string key) => All.Where(e => e.Key == key).Cast<ExchangeDef?>().FirstOrDefault();
    }
}
