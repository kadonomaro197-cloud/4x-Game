using Newtonsoft.Json;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Weapons
{
    public class ProjectileInfoDB : BaseDataBlob
    {
        public int LaunchedBy { get; set; } = -1;
        public int Count = 1;

        [JsonConstructor]
        private ProjectileInfoDB()
        {
        }

        public ProjectileInfoDB(int launchedBy, int count)
        {
            LaunchedBy = launchedBy;
            Count = count;
        }

        public override object Clone()
        {
            throw new System.NotImplementedException();
        }
    }
}