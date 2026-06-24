namespace Pulsar4X.JumpPoints;

public static class JPSurveyableDBExtensions
{
    public static bool IsSurveyComplete(this JPSurveyableDB geoSurveyableDB, int factionId)
    {
        return geoSurveyableDB.SurveyPointsRemaining.ContainsKey(factionId)
            && geoSurveyableDB.SurveyPointsRemaining[factionId] == 0;
    }

    public static bool HasSurveyStarted(this JPSurveyableDB geoSurveyableDB, int factionId)
    {
        return geoSurveyableDB.SurveyPointsRemaining.ContainsKey(factionId);
    }
}