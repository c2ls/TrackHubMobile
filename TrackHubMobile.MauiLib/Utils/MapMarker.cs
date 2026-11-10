using TrackHubMobile.Models;

namespace TrackHubMobile.Utils;

public static class MapMarker
{
    public static object From(PositionVm unit, UnitStatusRules rules) => new
    {
        lat = unit.Latitude,
        lng = unit.Longitude,
        speed = unit.Speed,
        course = unit.Course ?? 0,
        status = rules.DisplayStatusOf(unit, DateTimeOffset.UtcNow).ToString().ToLowerInvariant(),
        transporterId = unit.TransporterId.ToString()
    };
}
