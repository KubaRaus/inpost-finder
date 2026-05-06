using InpostTask.Web.Models;
using InpostTask.Web.Services;

namespace InpostTask.Tests;

public class PointSearchServiceTests
{
    [Fact]
    public async Task SearchAsync_AppliesCountryAndCityFilters()
    {
        var service = BuildService([
            MakePoint("P1", "PL", "Krakow", is247: true, payment: true, ["parcel_collect"]),
            MakePoint("P2", "FR", "Paris", is247: true, payment: true, ["parcel_collect"])
        ]);

        var result = await service.SearchAsync(new PointSearchRequest
        {
            CountryCode = "PL",
            City = "kra"
        }, CancellationToken.None);

        Assert.Single(result.Points);
        Assert.Equal("P1", result.Points[0].Point.Name);
    }

    [Fact]
    public async Task SearchAsync_ScoresRequiredPreferences()
    {
        var service = BuildService([
            MakePoint("Top", "PL", "Krakow", is247: true, payment: true, ["parcel_collect", "parcel_send"]),
            MakePoint("Low", "PL", "Krakow", is247: true, payment: false, ["parcel_collect"], status: "Disabled")
        ]);

        var result = await service.SearchAsync(new PointSearchRequest
        {
            CountryCode = "PL",
            RequiredFunctionsCsv = "parcel_collect,parcel_send",
            Require247 = true,
            RequirePayment = true
        }, CancellationToken.None);

        Assert.Single(result.Points);
        Assert.Equal("Top", result.Points[0].Point.Name);
        Assert.True(result.Points[0].Score > 0);
    }

    [Fact]
    public async Task SearchAsync_PrefersCloserPointWhenReferenceCoordinatesAreUsed()
    {
        var service = BuildService([
            MakePoint("Near", "PL", "Warsaw", is247: true, payment: true, ["parcel_collect"], distanceMeters: 300),
            MakePoint("Far", "PL", "Warsaw", is247: true, payment: true, ["parcel_collect"], distanceMeters: 7000)
        ]);

        var result = await service.SearchAsync(new PointSearchRequest
        {
            CountryCode = "PL",
            ReferenceLatitude = 52.2297,
            ReferenceLongitude = 21.0122
        }, CancellationToken.None);

        Assert.Equal("Near", result.Points[0].Point.Name);
        Assert.True(result.Points[0].Score > result.Points[1].Score);
    }

    private static PointSearchService BuildService(IReadOnlyList<InpostPointDto> points)
    {
        var api = new FakeApiClient(points);
        return new PointSearchService(api);
    }

    private static InpostPointDto MakePoint(
        string name,
        string country,
        string city,
        bool is247,
        bool payment,
        IReadOnlyCollection<string> functions,
        string status = "Operating",
        int? distanceMeters = null)
    {
        return new InpostPointDto
        {
            Name = name,
            CountryCode = country,
            City = city,
            Status = status,
            Type = "parcel_locker",
            LocationType = "Outdoor",
            IsOpen247 = is247,
            PaymentAvailable = payment,
            DistanceMeters = distanceMeters,
            Functions = functions
        };
    }

    private sealed class FakeApiClient(IReadOnlyList<InpostPointDto> points) : IInpostApiClient
    {
        public Task<IReadOnlyList<InpostPointDto>> GetPointsAsync(PointSearchRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(points);
        }
    }
}
