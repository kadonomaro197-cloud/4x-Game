using System.Collections.Generic;
using Newtonsoft.Json;
using Pulsar4X.Datablobs;

namespace Pulsar4X.People;

public enum BonusCategory
{
    None,
    ResearchPoints,
    ResearchCosts,
    Mining,
    // Combat competence categories — a commander's skill folded into the fleet auto-resolver
    // (the "a person's skill modifies an outcome" wire, rung 4). Read via CommanderBonuses.CombatMultiplier.
    Firepower,
    Toughness
}

public enum BonusType
{
    Number,
    Perentage,
}
public class Bonus
{
    [JsonProperty]
    public string Name { get; internal set; } = string.Empty;
    [JsonProperty]
    public double Value { get; internal set; } = 0;
    [JsonProperty]
    public BonusType Type { get; internal set; } = BonusType.Number;
    [JsonProperty]
    public BonusCategory Category { get; internal set; } = BonusCategory.None;
    [JsonProperty]
    public string FilterId { get; internal set; } = string.Empty;

    public Bonus(string name, double value, BonusType type, BonusCategory category, string filterId = "")
    {
        Name = name;
        Value = value;
        Type = type;
        Category = category;
        FilterId = filterId;
    }
}

public class BonusesDB : BaseDataBlob
{
    [JsonProperty]
    public List<Bonus> Bonuses { get; internal set; } = new List<Bonus>();
}