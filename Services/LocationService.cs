using Windows.Devices.Geolocation;

namespace DynamicIsland.Services;

/// <summary>
/// Resolves this device's coordinates through the Windows location service.
/// Unpackaged builds, disabled location settings, and a missing sensor all fail
/// here, so callers fall back to wttr.in's approximate network location.
/// </summary>
public sealed class LocationService
{
    private static readonly TimeSpan MaximumAge = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan FixTimeout = TimeSpan.FromSeconds(6);

    public async Task<(double Latitude, double Longitude)?> TryGetAsync()
    {
        try
        {
            var geolocator = new Geolocator { DesiredAccuracyInMeters = 1000 };
            var position = await geolocator.GetGeopositionAsync(MaximumAge, FixTimeout);
            var coordinate = position.Coordinate.Point.Position;
            if (double.IsNaN(coordinate.Latitude) || double.IsNaN(coordinate.Longitude))
            {
                return null;
            }

            return (coordinate.Latitude, coordinate.Longitude);
        }
        catch (Exception)
        {
            // A denied permission or absent sensor is an expected state, not a crash.
            return null;
        }
    }
}
