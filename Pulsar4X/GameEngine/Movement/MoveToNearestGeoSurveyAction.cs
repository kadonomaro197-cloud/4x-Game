using Pulsar4X.Engine;
using Pulsar4X.GeoSurveys;

namespace Pulsar4X.Movement
{
    public class MoveToNearestGeoSurveyAction : MoveToNearestAction
    {
        public override string Name => "Geo Survey Nearest";
        public override string Details => "Moves the fleet to the nearest system body that can be geo surveyed.";
        private bool GeoSurveyFilter(Entity entity)
        {
            return entity.HasDataBlob<GeoSurveyableDB>()
                && !entity.GetDataBlob<GeoSurveyableDB>().IsSurveyComplete(RequestingFactionGuid);
        }

        public static MoveToNearestGeoSurveyAction CreateCommand(int factionId, Entity commandingEntity)
        {
            var command = new MoveToNearestGeoSurveyAction()
            {
                _entityCommanding = commandingEntity,
                UseActionLanes = true,
                RequestingFactionGuid = factionId,
                EntityCommandingGuid = commandingEntity.Id,
                EntityFactionFilter = DataStructures.EntityFilter.Friendly | DataStructures.EntityFilter.Neutral
            };
            command.Filter = command.GeoSurveyFilter;
            return command;
        }
    }
}