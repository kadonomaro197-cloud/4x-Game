using Newtonsoft.Json;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;

namespace Pulsar4X.Weapons
{
    public class ProjectileInfoDB : BaseDataBlob
    {
        public int LaunchedBy { get; set; } = -1;
        public int Count = 1;
        public Entity TargetEntity;

        [JsonConstructor]
        private ProjectileInfoDB()
        {
        }

        public ProjectileInfoDB(int launchedBy, int count, Entity targetEntity = null)
        {
            LaunchedBy = launchedBy;
            Count = count;
            TargetEntity = targetEntity;
        }

        public override object Clone()
        {
            throw new System.NotImplementedException();
        }
    }
}