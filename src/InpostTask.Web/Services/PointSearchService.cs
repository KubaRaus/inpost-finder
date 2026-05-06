using InpostTask.Web.Models;

namespace InpostTask.Web.Services;

public sealed class PointSearchService(IInpostApiClient apiClient)
{
    public async Task<PointSearchResultViewModel> SearchAsync(PointSearchRequest request, CancellationToken cancellationToken)
    {
        var normalizedCountry = request.CountryCode?.Trim().ToUpperInvariant();
        var normalizedCity = request.City?.Trim();
        var requiredFunctions = ParseFunctions(request.RequiredFunctionsCsv);

        var all = await apiClient.GetPointsAsync(request, cancellationToken);

        var filtered = all.Where(point =>
                MatchesCountry(point, normalizedCountry)
                && MatchesCity(point, normalizedCity)
                && MatchesPayment(point, request.RequirePayment)
                && Matches247(point, request.Require247)
                && MatchesFunctions(point, requiredFunctions))
            .Select(point =>
            {
                var (score, breakdown) = Score(point, requiredFunctions, request.Require247, request.RequirePayment);
                return new RankedPointViewModel
                {
                    Point = point,
                    Score = score,
                    ScoreBreakdown = breakdown
                };
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Point.City)
            .ThenBy(x => x.Point.Name)
            .Take(20)
            .ToArray();

        return new PointSearchResultViewModel
        {
            Request = request,
            Points = filtered,
            TotalFetchedPoints = all.Count
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
        bool requirePayment)
    {
        var score = 0;
        var breakdown = new List<string>();

        foreach (var required in requiredFunctions)
        {
            if (point.Functions.Any(x => x.Contains(required, StringComparison.OrdinalIgnoreCase)))
            {
                score += 3;
                breakdown.Add($"+3 function: {required}");
            }
        }

        if (require247 && point.IsOpen247)
        {
            score += 2;
            breakdown.Add("+2 open 24/7");
        }

        if (requirePayment && point.PaymentAvailable)
        {
            score += 1;
            breakdown.Add("+1 payment available");
        }

        return (score, breakdown);
    }
}
