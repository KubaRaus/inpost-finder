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
    private const string CacheKey = "inpost-api-all-points";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);
    private const int MaxPages = 200;
    private const int PerPage = 500;

    public async Task<IReadOnlyList<InpostPointDto>> GetAllPointsAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue<IReadOnlyList<InpostPointDto>>(CacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var allPoints = new List<InpostPointDto>();

        for (var page = 1; page <= MaxPages; page++)
        {
            var pagePoints = await FetchPageAsync(page, cancellationToken);
            if (pagePoints.Count == 0)
            {
                break;
            }

            allPoints.AddRange(pagePoints);

            if (pagePoints.Count < PerPage)
            {
                break;
            }
        }

        cache.Set(CacheKey, allPoints, CacheDuration);
        return allPoints;
    }

    private async Task<IReadOnlyList<InpostPointDto>> FetchPageAsync(int page, CancellationToken cancellationToken)
    {
        var path = $"v1/points?page={page}&per_page={PerPage}";
        using var response = await httpClient.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var pointsElement = FindPointsArray(json.RootElement);
        if (pointsElement is null)
        {
            logger.LogWarning("Could not locate points array in API response for page {Page}", page);
            return [];
        }

        var parsed = new List<InpostPointDto>();
        foreach (var pointEl in pointsElement.Value.EnumerateArray())
        {
            parsed.Add(MapPoint(pointEl));
        }

        return parsed;
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
            Type = GetString(point, "type"),
            AddressLine1 = GetString(addressDetails, "line1") ?? GetString(point, "address1"),
            City = GetString(addressDetails, "city") ?? GetString(point, "city"),
            PostCode = GetString(addressDetails, "post_code") ?? GetString(point, "post_code"),
            CountryCode = (GetString(addressDetails, "country")
                           ?? GetString(point, "country")
                           ?? GetString(point, "country_code"))?.ToUpperInvariant(),
            Latitude = GetDouble(location, "latitude") ?? GetDouble(point, "latitude"),
            Longitude = GetDouble(location, "longitude") ?? GetDouble(point, "longitude"),
            IsOpen247 = is247,
            PaymentAvailable = paymentAvailable,
            Functions = functions
        };
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
}
