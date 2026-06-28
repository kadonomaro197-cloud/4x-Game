using System;
using System.Collections.Generic;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;

namespace Pulsar4X.Hazards
{
    /// <summary>
    /// Gives stars "weather". Keyed to <see cref="StarFlareSourceDB"/>: once a game-hour it checks each
    /// flare-capable star and, when the clock reaches that star's scheduled flare time, erupts a transient
    /// solar-flare hazard near it (handed off to <c>SpaceHazardProcessor</c> for its grow/fade/expire life)
    /// and rolls the next flare time. This is what makes the home system feel alive instead of static.
    /// </summary>
    public class SolarFlareProcessor : IHotloopProcessor
    {
        public void Init(Game game) { }

        public TimeSpan RunFrequency => TimeSpan.FromHours(1);
        public TimeSpan FirstRunOffset => TimeSpan.FromHours(0);
        public Type GetParameterType => typeof(StarFlareSourceDB);

        public void ProcessEntity(Entity entity, int deltaSeconds) => ProcessOne(entity);

        public int ProcessManager(EntityManager manager, int deltaSeconds)
        {
            var list = new List<StarFlareSourceDB>(manager.GetAllDataBlobsOfType<StarFlareSourceDB>());
            foreach (var src in list)
            {
                var star = src.OwningEntity;
                if (star == null || !star.IsValid)
                    continue;
                ProcessOne(star);
            }
            return list.Count;
        }

        private static void ProcessOne(Entity star)
        {
            var src = star.GetDataBlob<StarFlareSourceDB>();
            if (star.Manager is not StarSystem system)
                return;

            DateTime now = star.StarSysDateTime;
            if (now < src.NextFlareTime)
                return;

            var duration = TimeSpan.FromHours(src.FlareDurationHours);
            SpaceHazardFactory.CreateSolarFlare(system, star, now, duration, src.FlareMaxRadius_m);

            // Roll the next flare 0.5x .. 1.5x the mean gap, deterministically off the system RNG.
            double spread = 0.5 + system.RNGNextDouble();
            src.NextFlareTime = now + TimeSpan.FromDays(src.MeanDaysBetweenFlares * spread);
        }
    }
}
