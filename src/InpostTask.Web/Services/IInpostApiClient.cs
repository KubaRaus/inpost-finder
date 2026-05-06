using InpostTask.Web.Models;

namespace InpostTask.Web.Services;

public interface IInpostApiClient
{
    Task<IReadOnlyList<InpostPointDto>> GetPointsAsync(PointSearchRequest request, CancellationToken cancellationToken);
}
