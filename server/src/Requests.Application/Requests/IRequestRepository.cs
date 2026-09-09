using Requests.Domain.Entities;

namespace Requests.Application.Requests;

public interface IRequestRepository
{
    Task<List<Request>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<PagedResult<Request>> SearchAsync(
        SearchRequestsQuery query,
        int currentUserId,
        bool isAdministrator,
        CancellationToken cancellationToken = default);
}
