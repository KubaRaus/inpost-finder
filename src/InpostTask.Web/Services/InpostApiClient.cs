using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using InpostTask.Web.Models;

namespace InpostTask.Web.Services;

public sealed class InpostApiClient(
    HttpClient httpClient,
    IMemoryCache cache,
    ILogger<InpostApiClient> logger) : IInpostApiClient
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);
    private const int MaxPages = 50;
    private const int PerPage = 5000;

    public async Task<IReadOnlyList<InpostPointDto>> GetPointsAsync(PointSearchRequest request, CancellationToken cancellationToken)
    {
        var cacheKey = BuildCacheKey(request);
        if (cache.TryGetValue<IReadOnlyList<InpostPointDto>>(cacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var allPoints = new List<InpostPointDto>();
        string? previousPageSignature = null;
        int? totalPagesFromApi = null;

        for (var page = 1; page <= MaxPages; page++)
        {
            var pageResult = await FetchPageAsync(page, request, cancellationToken);
            var pagePoints = pageResult.Points;
            if (pagePoints.Count == 0)
            {
                break;
            }

            totalPagesFromApi ??= pageResult.TotalPages;
            var currentSignature = BuildPageSignature(pagePoints);
            if (previousPageSignature is not null && string.Equals(previousPageSignature, currentSignature, StringComparison.Ordinal))
            {
                logger.LogInformation(
                    "Stopping pagination at page {Page} due to repeated page signature. API may ignore paging params.",
                    page);
                break;
            }

            previousPageSignature = currentSignature;
            allPoints.AddRange(pagePoints);

            if (pagePoints.Count < PerPage)
            {
                break;
            }

            if (totalPagesFromApi is not null && page >= totalPagesFromApi.Value)
            {
                break;
            }
        }

        cache.Set(cacheKey, allPoints, CacheDuration);
        return allPoints;
    }

    private static string BuildPageSignature(IReadOnlyList<InpostPointDto> points)
    {
        var first = points[0];
        var last = points[^1];
        return $"{points.Count}|{first.Name}|{first.CountryCode}|{last.Name}|{last.CountryCode}";
    }

    private async Task<PageResult> FetchPageAsync(int page, PointSearchRequest request, CancellationToken cancellationToken)
    {
        var path = BuildPath(page, request);
        using var response = await httpClient.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var pointsElement = FindPointsArray(json.RootElement);
        if (pointsElement is null)
        {
            logger.LogWarning("Could not locate points array in API response for page {Page}", page);
            return new PageResult([], null);
        }

        var parsed = new List<InpostPointDto>();
        foreach (var pointEl in pointsElement.Value.EnumerateArray())
        {
            parsed.Add(MapPoint(pointEl));
        }

        var totalPages = GetInt(json.RootElement, "total_pages");
        return new PageResult(parsed, totalPages);
    }

    private static string BuildCacheKey(PointSearchRequest request)
    {
        var country = request.CountryCode?.Trim().ToUpperInvariant() ?? string.Empty;
        var city = request.City?.Trim().ToLowerInvariant() ?? string.Empty;
        var lat = request.ReferenceLatitude?.ToString("0.0000", CultureInfo.InvariantCulture) ?? string.Empty;
        var lng = request.ReferenceLongitude?.ToString("0.0000", CultureInfo.InvariantCulture) ?? string.Empty;
        return $"inpost-api-points::{country}::{city}::{lat}::{lng}";
    }

    private static string BuildPath(int page, PointSearchRequest request)
    {
        var parameters = new List<string>
        {
            $"page={page}",
            $"per_page={PerPage}"
        };

        if (!string.IsNullOrWhiteSpace(request.CountryCode))
        {
            parameters.Add($"country={Uri.EscapeDataString(request.CountryCode.Trim().ToUpperInvariant())}");
        }

        if (!string.IsNullOrWhiteSpace(request.City))
        {
            parameters.Add($"city={Uri.EscapeDataString(request.City.Trim())}");
        }

        if (request.ReferenceLatitude is not null && request.ReferenceLongitude is not null)
        {
            var lat = request.ReferenceLatitude.Value.ToString("0.######", CultureInfo.InvariantCulture);
            var lng = request.ReferenceLongitude.Value.ToString("0.######", CultureInfo.InvariantCulture);
            parameters.Add($"relative_point={Uri.EscapeDataString($"{lat},{lng}")}");
        }

        return $"v1/points?{string.Join("&", parameters)}";
    }

    private static JsonElement? FindPointsArray(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (TryGetPropertyIgnoreCase(root, "items", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            return items;
        }

        if (TryGetPropertyIgnoreCase(root, "points", out var points) && points.ValueKind == JsonValueKind.Array)
        {
            return points;
        }

        if (TryGetPropertyIgnoreCase(root, "_embedded", out var embedded)
            && embedded.ValueKind == JsonValueKind.Object
            && TryGetPropertyIgnoreCase(embedded, "points", out var embeddedPoints)
            && embeddedPoints.ValueKind == JsonValueKind.Array)
        {
            return embeddedPoints;
        }

        return null;
    }

    private static InpostPointDto MapPoint(JsonElement point)
    {
        var name = GetString(point, "name") ?? GetString(point, "id") ?? "unknown-point";
        var addressDetails = TryGetNested(point, "address_details");
        var address = TryGetNested(point, "address");
        var location = TryGetNested(point, "location");
        var functions = ExtractFunctions(point);

        var openingHours = GetString(point, "opening_hours")
                           ?? GetString(point, "openinghours")
                           ?? GetString(point, "opening_hours_info");

        var is247 = (openingHours?.Contains("24/7", StringComparison.OrdinalIgnoreCase) ?? false)
                    || (GetBool(point, "is_open_24_7") ?? false);

        var paymentAvailable = (GetBool(point, "payment_available") ?? false)
                               || functions.Any(x =>
                                   x.Contains("payment", StringComparison.OrdinalIgnoreCase)
                                   || x.Contains("cash", StringComparison.OrdinalIgnoreCase)
                                   || x.Contains("card", StringComparison.OrdinalIgnoreCase));

        return new InpostPointDto
        {
            Name = name,
            Type = GetPrimaryType(point),
            Status = GetString(point, "status"),
            LocationType = GetString(point, "location_type"),
            AddressLine1 = BuildAddressLine1(addressDetails, address, point),
            AddressLine2 = BuildAddressLine2(addressDetails, address, point),
            City = GetString(addressDetails, "city") ?? GetString(point, "city"),
            PostCode = GetString(addressDetails, "post_code") ?? GetString(point, "post_code"),
            CountryCode = (GetString(addressDetails, "country")
                           ?? GetString(point, "country")
                           ?? GetString(point, "country_code"))?.ToUpperInvariant(),
            Latitude = GetDouble(location, "latitude") ?? GetDouble(point, "latitude"),
            Longitude = GetDouble(location, "longitude") ?? GetDouble(point, "longitude"),
            DistanceMeters = GetInt(point, "distance"),
            IsOpen247 = is247,
            PaymentAvailable = paymentAvailable,
            Functions = functions
        };
    }

    private static string? GetPrimaryType(JsonElement point)
    {
        if (!TryGetPropertyIgnoreCase(point, "type", out var typeValue))
        {
            return null;
        }

        if (typeValue.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in typeValue.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var value = item.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }
            return null;
        }

        return typeValue.ValueKind == JsonValueKind.String ? typeValue.GetString() : typeValue.ToString();
    }

    private static string? BuildAddressLine1(JsonElement addressDetails, JsonElement address, JsonElement point)
    {
        var line1 = GetString(address, "line1")
                    ?? GetString(addressDetails, "line1")
                    ?? GetString(point, "address1");
        if (!string.IsNullOrWhiteSpace(line1))
        {
            return line1;
        }

        var street = GetString(addressDetails, "street");
        var building = GetString(addressDetails, "building_number");
        var flat = GetString(addressDetails, "flat_number");
        var parts = new[] { street, building, flat }.Where(x => !string.IsNullOrWhiteSpace(x));
        var composed = string.Join(" ", parts);
        return string.IsNullOrWhiteSpace(composed) ? null : composed;
    }

    private static string? BuildAddressLine2(JsonElement addressDetails, JsonElement address, JsonElement point)
    {
        var line2 = GetString(address, "line2")
                    ?? GetString(addressDetails, "line2")
                    ?? GetString(point, "address2");
        if (!string.IsNullOrWhiteSpace(line2))
        {
            return line2;
        }

        var postCode = GetString(addressDetails, "post_code") ?? GetString(point, "post_code");
        var city = GetString(addressDetails, "city") ?? GetString(point, "city");
        var parts = new[] { postCode, city }.Where(x => !string.IsNullOrWhiteSpace(x));
        var composed = string.Join(" ", parts);
        return string.IsNullOrWhiteSpace(composed) ? null : composed;
    }

    private static IReadOnlyCollection<string> ExtractFunctions(JsonElement point)
    {
        if (!TryGetPropertyIgnoreCase(point, "functions", out var functionsEl)
            || functionsEl.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in functionsEl.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    result.Add(value.Trim());
                }
                continue;
            }

            if (item.ValueKind == JsonValueKind.Object)
            {
                var value = GetString(item, "name") ?? GetString(item, "type") ?? GetString(item, "id");
                if (!string.IsNullOrWhiteSpace(value))
                {
                    result.Add(value.Trim());
                }
            }
        }

        return result.ToArray();
    }

    private static JsonElement TryGetNested(JsonElement element, string property)
    {
        return TryGetPropertyIgnoreCase(element, property, out var nested) ? nested : default;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(element, propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static bool? GetBool(JsonElement element, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(element, propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    private static double? GetDouble(JsonElement element, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(element, propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDouble(out var number) => number,
            JsonValueKind.String when double.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };
    }

    private static int? GetInt(JsonElement element, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(element, propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    private sealed record PageResult(IReadOnlyList<InpostPointDto> Points, int? TotalPages);
}
