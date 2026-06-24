using Newtonsoft.Json;
using Pulsar4X.Datablobs;

namespace Pulsar4X.JumpPoints;

public class JPSurveyAbilityDB : BaseDataBlob
{
    [JsonProperty]
    public uint Speed { get; set; }
}