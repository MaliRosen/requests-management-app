namespace Requests.Application.Requests;

public interface IRequestService
{
    Task<IReadOnlyList<RequestDto>> GetRequestsAsync(
        int currentUserId,
        bool isAdministrator,
        CancellationToken cancellationToken = default);

    Task<PagedResult<RequestDto>> SearchAsync(
        SearchRequestsQuery query,
        int currentUserId,
        bool isAdministrator,
        CancellationToken cancellationToken = default);
}
