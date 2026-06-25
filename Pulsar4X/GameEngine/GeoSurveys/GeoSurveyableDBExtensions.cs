namespace Pulsar4X.GeoSurveys;

public static class GeoSurveyableDBExtensions
{
    public static bool IsSurveyComplete(this GeoSurveyableDB geoSurveyableDB, int factionId)
    {
        return geoSurveyableDB.GeoSurveyStatus.ContainsKey(factionId)
            && geoSurveyableDB.GeoSurveyStatus[factionId] == 0;
    }

    public static bool HasSurveyStarted(this GeoSurveyableDB geoSurveyableDB, int factionId)
    {
        return geoSurveyableDB.GeoSurveyStatus.ContainsKey(factionId);
    }
}