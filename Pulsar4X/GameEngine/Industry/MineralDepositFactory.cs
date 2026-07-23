using System;
using System.Collections.Generic;
using Pulsar4X.Blueprints;
using Pulsar4X.DataStructures;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;
using Pulsar4X.Orbital;

namespace Pulsar4X.Industry;

public class MineralDepositFactory
{
    public static MineralsDB? GenerateRandom(SystemGenSettingsBlueprint settings, List<Mineral> minerals, StarSystem system, SystemBodyInfoDB bodyInfoDB, MassVolumeDB massVolumeDB, bool forceGeneration = false)
    {
        double baseChance = settings.BaseMineralChance * 10;
        var typeMass = settings.SystemBodyMassByType[bodyInfoDB.BodyType];
        var avgMass = (typeMass.Max + typeMass.Min) * .5;

        double massRatio = massVolumeDB.MassDry / avgMass;//UniversalConstants.Units.EarthMassInKG;
        double genChance = baseChance * massRatio * system.RNGNextDouble();
        double genChanceThreshold = settings.MineralGenerationChanceByBodyType[bodyInfoDB.BodyType];

        var mineralInfo = new MineralsDB();

        // this body has at least some minerals, lets generate them:
        foreach (var min in minerals)
        {
            // create a MineralDepositInfo
            MineralDeposit mdi = new MineralDeposit();

            // get a genChance:
            double abundance = min.Abundance[bodyInfoDB.BodyType];
            genChance = baseChance * massRatio * system.RNGNextDouble() * abundance;

            if (genChance >= genChanceThreshold)
            {
                mdi.Accessibility = GeneralMath.Clamp(settings.MinMineralAccessibility + genChance, 0, 1);
                mdi.Amount = new Masked<long>((long)Math.Round(settings.MaxMineralAmmountByBodyType[bodyInfoDB.BodyType] * genChance), AccessLevel.None);
                mdi.HalfOriginalAmount = mdi.Amount.Actual / 2;

                if (!mineralInfo.Minerals.ContainsKey(min.ID))
                {
                    mineralInfo.Minerals.Add(min.ID, mdi);
                }
            }
        }

        return mineralInfo.Minerals.Count > 0 ? mineralInfo : null;
    }

    public static MineralsDB? GenerateRandomHW(SystemGenSettingsBlueprint settings, List<Mineral> minerals, StarSystem system, SystemBodyInfoDB bodyInfoDB, MassVolumeDB massVolumeDB, bool forceGeneration = false)
    {
        var mineralInfo = new MineralsDB();
        foreach (var min in minerals)
        {
            // create a MineralDepositInfo
            MineralDeposit mdi = new MineralDeposit();
            mdi.Accessibility = GeneralMath.Clamp(settings.MinHomeworldMineralAccessibility + system.RNGNextDouble() * min.Abundance[bodyInfoDB.BodyType], 0, 1);
            mdi.Amount = new Masked<long>((long)Math.Round(settings.MinHomeworldMineralAmmount + settings.HomeworldMineralAmmount * system.RNGNextDouble() * min.Abundance[bodyInfoDB.BodyType]), AccessLevel.None);
            mdi.HalfOriginalAmount = mdi.Amount.Actual / 2;
            if (!mineralInfo.Minerals.ContainsKey(min.ID))
            {
                mineralInfo.Minerals.Add(min.ID, mdi);
            }
        }
        return mineralInfo.Minerals.Count > 0 ? mineralInfo : null;
    }

    /// <summary>
    /// The RNG-FREE "no body is accidentally barren" safety net. For a MINEABLE body that carries no explicit
    /// mineral spec, builds a deterministic deposit from each mineral's own body-type abundance (accessibility
    /// 1.0), so anything a player would send a mining ship to actually HAS ore in it. Returns null for a body
    /// type with no surface to mine (gas/ice giants) or with no positive abundances — the caller then adds no
    /// MineralsDB and the body stays barren.
    ///
    /// It NEVER draws the shared system RNG (it routes through <see cref="Generate"/>, the same RNG-free path
    /// Luna's explicit array uses), so it cannot perturb galaxy-gen determinism — the RuinsDB lesson: don't
    /// advance the stream when you bolt generated content onto a body other draws depend on.
    ///
    /// Two callers share this one rule: SystemBodyFactory (authored named bodies — rescues the outer Solar
    /// System's moons/dwarfs, which carry a TYPO'd "MineralGeneration" key the loader ignores) and
    /// StarSystemFactory.GenerateAsteroidBelt (the scattered belt rocks that fill Sol's main + Kuiper belts).
    /// </summary>
    public static MineralsDB? GenerateBodyTypeFallback(Game game, BodyType bodyType)
    {
        // No surface to mine → leave it barren (gas/ice giants, gas dwarfs, anything not on this list).
        if(!(bodyType is BodyType.Terrestrial or BodyType.Moon or BodyType.DwarfPlanet
             or BodyType.Asteroid or BodyType.Comet))
            return null;

        var fallbackMinerals = new List<(int, double, double)>();
        foreach(var mineral in game.StartingGameData.Minerals.Values)
        {
            if(mineral.Abundance != null
               && mineral.Abundance.TryGetValue(bodyType, out var abundance)
               && abundance > 0)
            {
                fallbackMinerals.Add((mineral.ID, abundance, 1.0));
            }
        }

        return fallbackMinerals.Count > 0 ? Generate(game, fallbackMinerals, bodyType) : null;
    }

    public static MineralsDB Generate(Game game, List<(int, double, double)> mineralsToGenerate, BodyType bodyType)
    {
        var mineralsDb = new MineralsDB();

        foreach((int id, double abundance, double accessibility) in mineralsToGenerate)
        {
            var mdi = new MineralDeposit()
            {
                Accessibility = GeneralMath.Clamp(accessibility, 0, 1),
                Amount = new Masked<long>((long)Math.Round(game.GalaxyGen.Settings.MaxMineralAmmountByBodyType[bodyType] * abundance), AccessLevel.None),
            };
            mdi.HalfOriginalAmount = mdi.Amount.Actual / 2;
            mineralsDb.Minerals.Add(id, mdi);
        }

        return mineralsDb;
    }
}