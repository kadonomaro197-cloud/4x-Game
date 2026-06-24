using Pulsar4X.Modding;
using NUnit.Framework;
using System.Data;

namespace Pulsar4X.Tests
{
    [TestFixture]
    [Description("Tests the ModLoader class")]
    internal class ModLoaderTests
    {
        ModLoader _modLoader;
        ModDataStore _modDataStore;

        [SetUp]
        public void Setup()
        {
            _modLoader = new ModLoader();
            _modDataStore = new ModDataStore();
        }

        [Test]
        [Description("Tests loading a mod")]
        public void TestModLoader()
        {
            _modLoader.LoadModManifest("Data/basemod/modInfo.json", _modDataStore);

            Assert.AreEqual(1, _modLoader.LoadedMods.Count);
        }

        [Test]
        public void TestDuplicateMods()
        {
            _modLoader.LoadModManifest("Data/basemod/modInfo.json", _modDataStore);

            var ex = Assert.Throws<DuplicateNameException>(() => {
                // Load the same mod again
                _modLoader.LoadModManifest("Data/basemod/modInfo.json", _modDataStore);
            });
        }

        [Test]
        [Description("A collection operation (Add/Remove/Overwrite) on a base blueprint whose target " +
                     "list/dictionary is null must not crash the mod load. Regression for the New Game " +
                     "'Unable to resolve Dictionary'/'Unable to resolve List' NullReferenceException.")]
        public void TestCollectionOperationOnNullBaseCollectionDoesNotThrow()
        {
            // The fixture defines a theme with no collections, then Adds to a null list,
            // Overwrites a null dictionary, and Removes from a null dictionary.
            Assert.DoesNotThrow(() =>
                _modLoader.LoadModManifest("Data/collectionopmod/modInfo.json", _modDataStore));

            var theme = _modDataStore.Themes["collectionop-theme"];

            // Add onto a null list becomes the mod's list.
            Assert.AreEqual(2, theme.FirstNames.Count);
            // Overwrite onto a null dictionary sets it.
            Assert.AreEqual(1, theme.NavyRanks.Count);
            // Remove from a null dictionary is a no-op (stays null), not a crash.
            Assert.IsNull(theme.NavyRanksAbbreviations);
        }
    }
}