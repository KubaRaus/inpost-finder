using InpostTask.Web.Models;

namespace InpostTask.Web.Services;

public interface IInpostApiClient
{
    Task<IReadOnlyList<InpostPointDto>> GetAllPointsAsync(CancellationToken cancellationToken);
}
