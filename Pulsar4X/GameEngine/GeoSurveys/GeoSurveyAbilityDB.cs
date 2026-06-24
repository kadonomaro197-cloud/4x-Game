using Newtonsoft.Json;
using Pulsar4X.Datablobs;

namespace Pulsar4X.GeoSurveys;

public class GeoSurveyAbilityDB : BaseDataBlob
{
    [JsonProperty]
    public uint Speed { get; set; }
}