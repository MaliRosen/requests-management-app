using Requests.Domain.Entities;

namespace Requests.Application.Requests;

public sealed record SearchRequestsQuery(
    string? RequestNumber = null,
    RequestStatus[]? Status = null,
    RequestType[]? RequestType = null,
    DateTime? CreatedFrom = null,
    DateTime? CreatedTo = null,
    string SortBy = "CreatedAt",
    string SortDirection = "desc",
    int Page = 1,
    int PageSize = 20
);
