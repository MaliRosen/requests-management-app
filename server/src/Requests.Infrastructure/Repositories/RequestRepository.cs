using Microsoft.EntityFrameworkCore;
using Requests.Application.Requests;
using Requests.Domain.Entities;
using Requests.Infrastructure.Persistence;

namespace Requests.Infrastructure.Repositories;

public sealed class RequestRepository : IRequestRepository
{
    private readonly RequestsDbContext _db;

    public RequestRepository(RequestsDbContext db)
    {
        _db = db;
    }

    public Task<List<Request>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _db.Requests.ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<Request>> SearchAsync(
        SearchRequestsQuery query,
        int currentUserId,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        // 1. Start with full table as IQueryable
        IQueryable<Request> q = _db.Requests.AsQueryable();

        // 2. Ownership filter — non-admins see only their own or assigned requests
        if (!isAdministrator)
        {
            q = q.Where(r => r.OwnerId == currentUserId || r.AssignedToUserId == currentUserId);
        }

        // 3. RequestNumber partial match (EF Core → SQL LIKE %value%)
        if (!string.IsNullOrEmpty(query.RequestNumber))
        {
            q = q.Where(r => r.RequestNumber.Contains(query.RequestNumber));
        }

        // 4. Status filter (IN clause)
        if (query.Status is { Length: > 0 })
        {
            q = q.Where(r => query.Status.Contains(r.Status));
        }

        // 5. RequestType filter (IN clause)
        if (query.RequestType is { Length: > 0 })
        {
            q = q.Where(r => query.RequestType.Contains(r.RequestType));
        }

        // 6. CreatedFrom — inclusive lower bound
        if (query.CreatedFrom.HasValue)
        {
            q = q.Where(r => r.CreatedAt >= query.CreatedFrom.Value);
        }

        // 7. CreatedTo — inclusive upper bound
        if (query.CreatedTo.HasValue)
        {
            q = q.Where(r => r.CreatedAt <= query.CreatedTo.Value);
        }

        // 8. Total count before pagination
        int totalCount = await q.CountAsync(cancellationToken);

        // 9. Sorting
        bool ascending = string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        q = query.SortBy.ToLowerInvariant() switch
        {
            "id"            => ascending ? q.OrderBy(r => r.Id)            : q.OrderByDescending(r => r.Id),
            "requestnumber" => ascending ? q.OrderBy(r => r.RequestNumber) : q.OrderByDescending(r => r.RequestNumber),
            "status"        => ascending ? q.OrderBy(r => r.Status)        : q.OrderByDescending(r => r.Status),
            "requesttype"   => ascending ? q.OrderBy(r => r.RequestType)   : q.OrderByDescending(r => r.RequestType),
            "createdat"     => ascending ? q.OrderBy(r => r.CreatedAt)     : q.OrderByDescending(r => r.CreatedAt),
            "ownerid"       => ascending ? q.OrderBy(r => r.OwnerId)       : q.OrderByDescending(r => r.OwnerId),
            // Unknown sort field falls through to CreatedAt desc (safe default)
            _               => q.OrderByDescending(r => r.CreatedAt),
        };

        // 10. Pagination
        q = q.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize);

        // 11. Execute
        List<Request> items = await q.ToListAsync(cancellationToken);

        // 12. Return paged result
        return new PagedResult<Request>(items, query.Page, query.PageSize, totalCount);
    }
}
