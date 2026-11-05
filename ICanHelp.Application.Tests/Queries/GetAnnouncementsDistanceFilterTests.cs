using ICanHelp.Application.Exceptions;
using ICanHelp.Application.Queries;

namespace ICanHelp.Application.Tests.Queries;

/// <summary>
/// Krok 7: walidacja parametrów wyszukiwania po odległości. Samo zapytanie SQL (geography) sprawdzą
/// testy integracyjne z prawdziwym SQL Serverem (Krok 8).
/// </summary>
public class GetAnnouncementsDistanceFilterTests
{
    [Fact]
    public void No_location_means_no_filter()
    {
        new GetAnnouncements().GetDistanceFilter().ShouldBeNull();
    }

    [Fact]
    public void Location_without_radius_uses_default_radius()
    {
        var filter = new GetAnnouncements { Latitude = 52.23, Longitude = 21.01 }.GetDistanceFilter();

        filter.ShouldNotBeNull();
        filter.Latitude.ShouldBe(52.23);
        filter.Longitude.ShouldBe(21.01);
        filter.RadiusKm.ShouldBe(GetAnnouncements.DefaultRadiusKm);
        filter.RadiusMeters.ShouldBe(GetAnnouncements.DefaultRadiusKm * 1000);
    }

    [Fact]
    public void Location_with_radius_is_kept()
    {
        var filter = new GetAnnouncements { Latitude = 50.06, Longitude = 19.94, RadiusKm = 2.5 }.GetDistanceFilter();

        filter.ShouldNotBeNull();
        filter.RadiusKm.ShouldBe(2.5);
        filter.RadiusMeters.ShouldBe(2500);
    }

    [Fact]
    public void Max_radius_is_allowed()
    {
        var filter = new GetAnnouncements { Latitude = 0, Longitude = 0, RadiusKm = GetAnnouncements.MaxRadiusKm }
            .GetDistanceFilter();

        filter.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(52.23, null, null)]
    [InlineData(null, 21.01, null)]
    [InlineData(null, null, 5.0)]
    [InlineData(91.0, 21.01, null)]
    [InlineData(-91.0, 21.01, null)]
    [InlineData(52.23, 181.0, null)]
    [InlineData(52.23, -181.0, null)]
    [InlineData(52.23, 21.01, 0.0)]
    [InlineData(52.23, 21.01, -1.0)]
    [InlineData(52.23, 21.01, 100.1)]
    [InlineData(double.NaN, 21.01, null)]
    public void Invalid_parameters_throw(double? latitude, double? longitude, double? radiusKm)
    {
        var query = new GetAnnouncements { Latitude = latitude, Longitude = longitude, RadiusKm = radiusKm };

        Should.Throw<InvalidDistanceFilterException>(() => query.GetDistanceFilter());
    }
}
