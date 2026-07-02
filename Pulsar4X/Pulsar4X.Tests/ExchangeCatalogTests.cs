using System;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge for the EXCHANGE CATALOG data model (docs/DIPLOMACY-DESIGN.md exchange catalog, task #35): everything
    /// two factions can trade, tagged with its family, instant-vs-standing, and which existing system it routes
    /// into — so the catalog IS the connection map. This tests the DATA integrity (broad coverage, unique keys,
    /// every row wired to a route), not behavior; the commitment model that executes a chosen exchange is the next
    /// step.
    /// </summary>
    [TestFixture]
    public class ExchangeCatalogTests
    {
        [Test]
        [Description("The catalog is broad: non-empty, and every one of the seven categories has at least one entry.")]
        public void Catalog_CoversAllCategories()
        {
            Assert.That(ExchangeCatalog.All, Is.Not.Empty);
            foreach (ExchangeCategory cat in Enum.GetValues(typeof(ExchangeCategory)))
                Assert.That(ExchangeCatalog.ByCategory(cat).Any(), Is.True, $"category {cat} has no entries");
        }

        [Test]
        [Description("Data integrity: every entry has a key + description and routes into a system; keys are unique.")]
        public void Catalog_EntriesAreWellFormedAndUnique()
        {
            foreach (var e in ExchangeCatalog.All)
            {
                Assert.That(e.Key, Is.Not.Null.And.Not.Empty, "every exchange needs a stable key");
                Assert.That(e.Description, Is.Not.Null.And.Not.Empty, $"{e.Key} needs a description");
                Assert.That(Enum.IsDefined(typeof(ExchangeRoute), e.Route), Is.True, $"{e.Key} routes into a real system");
            }
            var keys = ExchangeCatalog.All.Select(e => e.Key).ToList();
            Assert.That(keys.Distinct().Count(), Is.EqualTo(keys.Count), "exchange keys must be unique");
        }

        [Test]
        [Description("Lookup by key resolves the right row; an unknown key returns null.")]
        public void ByKey_ResolvesAndMisses()
        {
            var trade = ExchangeCatalog.ByKey("trade-agreement");
            Assert.That(trade, Is.Not.Null);
            Assert.That(trade.Value.Category, Is.EqualTo(ExchangeCategory.Economic));
            Assert.That(trade.Value.Route, Is.EqualTo(ExchangeRoute.Logistics));
            Assert.That(trade.Value.Kind, Is.EqualTo(ExchangeKind.Standing));

            Assert.That(ExchangeCatalog.ByKey("does-not-exist"), Is.Null);
        }

        [Test]
        [Description("The catalog exercises both instant transfers and standing commitments, and reaches the money, logistics, fleets, and combat/IFF systems.")]
        public void Catalog_SpansKindsAndKeyRoutes()
        {
            Assert.That(ExchangeCatalog.All.Any(e => e.Kind == ExchangeKind.Instant), Is.True);
            Assert.That(ExchangeCatalog.All.Any(e => e.Kind == ExchangeKind.Standing), Is.True);

            foreach (var route in new[] { ExchangeRoute.Ledger, ExchangeRoute.Logistics, ExchangeRoute.Fleets, ExchangeRoute.CombatIFF })
                Assert.That(ExchangeCatalog.All.Any(e => e.Route == route), Is.True, $"no exchange routes into {route}");
        }
    }
}
