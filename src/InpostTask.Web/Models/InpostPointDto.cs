namespace InpostTask.Web.Models;

public sealed class InpostPointDto
{
    public required string Name { get; init; }
    public string? Type { get; init; }
    public string? AddressLine1 { get; init; }
    public string? City { get; init; }
    public string? PostCode { get; init; }
    public string? CountryCode { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public bool IsOpen247 { get; init; }
    public bool PaymentAvailable { get; init; }
    public IReadOnlyCollection<string> Functions { get; init; } = [];
}
