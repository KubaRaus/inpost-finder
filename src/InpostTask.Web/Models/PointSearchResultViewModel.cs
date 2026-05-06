namespace InpostTask.Web.Models;

public sealed class PointSearchResultViewModel
{
    public required PointSearchRequest Request { get; init; }
    public required IReadOnlyList<RankedPointViewModel> Points { get; init; }
    public int TotalFetchedPoints { get; init; }
}

public sealed class RankedPointViewModel
{
    public required InpostPointDto Point { get; init; }
    public int Score { get; init; }
    public required IReadOnlyList<string> ScoreBreakdown { get; init; }
}
