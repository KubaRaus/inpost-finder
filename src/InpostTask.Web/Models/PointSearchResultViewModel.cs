namespace InpostTask.Web.Models;

public sealed class PointSearchResultViewModel
{
    public required PointSearchRequest Request { get; init; }
    public required IReadOnlyList<RankedPointViewModel> Points { get; init; }
    public int TotalFetchedPoints { get; init; }
    public int TotalMatchingPoints { get; init; }
    public int CurrentPage { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling((double)TotalMatchingPoints / PageSize);
    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;
}

public sealed class RankedPointViewModel
{
    public required InpostPointDto Point { get; init; }
    public int Score { get; init; }
    public required IReadOnlyList<string> ScoreBreakdown { get; init; }
}
