using InpostTask.Web.Models;

namespace InpostTask.Web.Services;

public sealed class PointSearchService(IInpostApiClient apiClient)
{
    public const int DefaultPageSize = 20;

    public async Task<PointSearchResultViewModel> SearchAsync(
        PointSearchRequest request,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var normalizedCountry = request.CountryCode?.Trim().ToUpperInvariant();
        var normalizedCity = request.City?.Trim();
        var preferredType = request.PreferredType?.Trim();
        var preferredLocationType = request.PreferredLocationType?.Trim();
        var requiredFunctions = ParseFunctions(request.RequiredFunctionsCsv);

        var all = await apiClient.GetPointsAsync(request, cancellationToken);

        var ranked = all.Where(point =>
                MatchesCountry(point, normalizedCountry)
                && MatchesCity(point, normalizedCity)
                && MatchesPayment(point, request.RequirePayment)
                && Matches247(point, request.Require247)
                && MatchesFunctions(point, requiredFunctions))
            .Select(point =>
            {
                var (score, breakdown) = Score(
                    point,
                    requiredFunctions,
                    request.Require247,
                    request.RequirePayment,
                    preferredType,
                    preferredLocationType);
                return new RankedPointViewModel
                {
                    Point = point,
                    Score = score,
                    ScoreBreakdown = breakdown
                };
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Point.DistanceMeters ?? int.MaxValue)
            .ThenBy(x => x.Point.City)
            .ThenBy(x => x.Point.Name)
            .ToArray();

        var effectivePageSize = pageSize <= 0 ? DefaultPageSize : pageSize;
        var safePage = page <= 0 ? 1 : page;
        var totalMatchingPoints = ranked.Length;
        var totalPages = totalMatchingPoints == 0
            ? 1
            : (int)Math.Ceiling((double)totalMatchingPoints / effectivePageSize);
        if (safePage > totalPages)
        {
            safePage = totalPages;
        }

        var paged = ranked
            .Skip((safePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .ToArray();

        return new PointSearchResultViewModel
        {
            Request = request,
            Points = paged,
            TotalFetchedPoints = all.Count,
            TotalMatchingPoints = totalMatchingPoints,
            CurrentPage = safePage,
            PageSize = effectivePageSize
        };
    }

    private static string[] ParseFunctions(string? csv)
    {
        return string.IsNullOrWhiteSpace(csv)
            ? []
            : csv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }

    private static bool MatchesCountry(InpostPointDto point, string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return true;
        }

        return string.Equals(point.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesCity(InpostPointDto point, string? city)
    {
        if (string.IsNullOrWhiteSpace(city))
        {
            return true;
        }

        return point.City?.Contains(city, StringComparison.OrdinalIgnoreCase) ?? false;
    }

    private static bool Matches247(InpostPointDto point, bool require247)
    {
        return !require247 || point.IsOpen247;
    }

    private static bool MatchesPayment(InpostPointDto point, bool requirePayment)
    {
        return !requirePayment || point.PaymentAvailable;
    }

    private static bool MatchesFunctions(InpostPointDto point, IReadOnlyCollection<string> requiredFunctions)
    {
        if (requiredFunctions.Count == 0)
        {
            return true;
        }

        return requiredFunctions.All(required =>
            point.Functions.Any(function => function.Contains(required, StringComparison.OrdinalIgnoreCase)));
    }

    private static (int score, IReadOnlyList<string> breakdown) Score(
        InpostPointDto point,
        IReadOnlyCollection<string> requiredFunctions,
        bool require247,
        bool requirePayment,
        string? preferredType,
        string? preferredLocationType)
    {
        var score = 0;
        var breakdown = new List<string>();

        if (string.Equals(point.Status, "Operating", StringComparison.OrdinalIgnoreCase))
        {
            score += 60;
            breakdown.Add("+60 status: Operating");
        }
        else
        {
            score -= 40;
            breakdown.Add("-40 status: not Operating");
        }

        if (point.DistanceMeters is not null)
        {
            var distanceKm = point.DistanceMeters.Value / 1000.0;
            var distanceScore = Math.Clamp((int)Math.Round(100 - (distanceKm * 8)), 0, 100);
            score += distanceScore;
            breakdown.Add($"+{distanceScore} distance: {distanceKm:0.0} km");
        }

        if (!string.IsNullOrWhiteSpace(preferredType))
        {
            if (point.Type?.Contains(preferredType, StringComparison.OrdinalIgnoreCase) ?? false)
            {
                score += 20;
                breakdown.Add($"+20 preferred type: {preferredType}");
            }
            else
            {
                score -= 5;
                breakdown.Add($"-5 different type than preferred: {preferredType}");
            }
        }

        if (!string.IsNullOrWhiteSpace(preferredLocationType)
            && point.LocationType?.Contains(preferredLocationType, StringComparison.OrdinalIgnoreCase) == true)
        {
            score += 8;
            breakdown.Add($"+8 location type: {preferredLocationType}");
        }

        foreach (var required in requiredFunctions)
        {
            if (point.Functions.Any(x => x.Contains(required, StringComparison.OrdinalIgnoreCase)))
            {
                score += 5;
                breakdown.Add($"+5 function: {required}");
            }
        }

        if (require247 && point.IsOpen247)
        {
            score += 10;
            breakdown.Add("+10 required 24/7");
        }
        else if (!require247 && point.IsOpen247)
        {
            score += 2;
            breakdown.Add("+2 bonus 24/7");
        }

        if (requirePayment && point.PaymentAvailable)
        {
            score += 5;
            breakdown.Add("+5 required payment available");
        }

        return (score, breakdown);
    }
}
