using Requests.Application.Requests;
using Requests.Domain.Entities;
using Xunit;

namespace Requests.Tests;

public class RequestServiceTests
{
    // ────────────────────────────────────────────────────────────────────────
    // Existing tests — must not be broken
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Administrator_CanSeeAllRequests()
    {
        var repository = new FakeRequestRepository(
        [
            Create(1, ownerId: 1, assignedTo: 2),
            Create(2, ownerId: 3, assignedTo: 4)
        ]);

        var service = new RequestService(repository);

        var result = await service.GetRequestsAsync(1, true);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task RegularUser_CanSeeOwnedOrAssignedRequests()
    {
        var repository = new FakeRequestRepository(
        [
            Create(1, ownerId: 1, assignedTo: 5),
            Create(2, ownerId: 3, assignedTo: 1),
            Create(3, ownerId: 3, assignedTo: 5)
        ]);

        var service = new RequestService(repository);

        var result = await service.GetRequestsAsync(1, false);

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, x => x.Id == 3);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Task 8 — SearchAsync unit tests
    // ────────────────────────────────────────────────────────────────────────

    // 8.1 Admin sees all requests regardless of ownership
    [Fact]
    public async Task Search_Admin_SeesAllRequests()
    {
        var repository = new FakeRequestRepository(
        [
            Create(1, ownerId: 10, assignedTo: 20),
            Create(2, ownerId: 30, assignedTo: 40)
        ]);

        var service = new RequestService(repository);
        var result = await service.SearchAsync(new SearchRequestsQuery(), currentUserId: 99, isAdministrator: true);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
    }

    // 8.2 RegularUser sees only owned or assigned requests
    [Fact]
    public async Task Search_RegularUser_SeesOnlyOwnedOrAssigned()
    {
        var repository = new FakeRequestRepository(
        [
            Create(1, ownerId: 1,  assignedTo: 99),  // owned
            Create(2, ownerId: 99, assignedTo: 1),   // assigned
            Create(3, ownerId: 99, assignedTo: 99)   // unrelated
        ]);

        var service = new RequestService(repository);
        var result = await service.SearchAsync(new SearchRequestsQuery(), currentUserId: 1, isAdministrator: false);

        Assert.Equal(2, result.TotalCount);
        Assert.DoesNotContain(result.Items, x => x.Id == 3);
    }

    // 8.3 Filter by RequestNumber partial match (case-insensitive)
    [Fact]
    public async Task Search_FilterByRequestNumber_PartialCaseInsensitive()
    {
        var r1 = Create(1, ownerId: 1, assignedTo: 0); r1.RequestNumber = "REQ-001";
        var r2 = Create(2, ownerId: 1, assignedTo: 0); r2.RequestNumber = "REQ-002";
        var r3 = Create(3, ownerId: 1, assignedTo: 0); r3.RequestNumber = "OTHER-003";

        var repository = new FakeRequestRepository([r1, r2, r3]);
        var service = new RequestService(repository);

        // "req-00" should match REQ-001 and REQ-002 (case-insensitive partial)
        var result = await service.SearchAsync(
            new SearchRequestsQuery(RequestNumber: "req-00"),
            currentUserId: 1, isAdministrator: true);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, item => Assert.Contains("REQ-00", item.RequestNumber, StringComparison.OrdinalIgnoreCase));
    }

    // 8.4 Filter by single Status
    [Fact]
    public async Task Search_FilterBySingleStatus()
    {
        var repository = new FakeRequestRepository(
        [
            CreateWithStatus(1, RequestStatus.New),
            CreateWithStatus(2, RequestStatus.InProgress),
            CreateWithStatus(3, RequestStatus.Completed)
        ]);

        var service = new RequestService(repository);
        var result = await service.SearchAsync(
            new SearchRequestsQuery(Status: [RequestStatus.InProgress]),
            currentUserId: 1, isAdministrator: true);

        Assert.Equal(1, result.TotalCount);
        Assert.All(result.Items, item => Assert.Equal(RequestStatus.InProgress, item.Status));
    }

    // 8.5 Filter by multiple Status values
    [Fact]
    public async Task Search_FilterByMultipleStatuses()
    {
        var repository = new FakeRequestRepository(
        [
            CreateWithStatus(1, RequestStatus.New),
            CreateWithStatus(2, RequestStatus.InProgress),
            CreateWithStatus(3, RequestStatus.Completed),
            CreateWithStatus(4, RequestStatus.Cancelled)
        ]);

        var service = new RequestService(repository);
        var result = await service.SearchAsync(
            new SearchRequestsQuery(Status: [RequestStatus.New, RequestStatus.Completed]),
            currentUserId: 1, isAdministrator: true);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, item =>
            Assert.True(item.Status == RequestStatus.New || item.Status == RequestStatus.Completed));
    }

    // 8.6 Filter by RequestType
    [Fact]
    public async Task Search_FilterByRequestType()
    {
        var repository = new FakeRequestRepository(
        [
            CreateWithType(1, RequestType.General),
            CreateWithType(2, RequestType.Legal),
            CreateWithType(3, RequestType.Payment)
        ]);

        var service = new RequestService(repository);
        var result = await service.SearchAsync(
            new SearchRequestsQuery(RequestType: [RequestType.Legal]),
            currentUserId: 1, isAdministrator: true);

        Assert.Equal(1, result.TotalCount);
        Assert.All(result.Items, item => Assert.Equal(RequestType.Legal, item.RequestType));
    }

    // 8.7 Filter by CreatedFrom and CreatedTo date range
    [Fact]
    public async Task Search_FilterByDateRange()
    {
        var baseDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var r1 = Create(1, ownerId: 1, assignedTo: 0); r1.CreatedAt = baseDate.AddDays(-1); // before range
        var r2 = Create(2, ownerId: 1, assignedTo: 0); r2.CreatedAt = baseDate.AddDays(5);  // in range
        var r3 = Create(3, ownerId: 1, assignedTo: 0); r3.CreatedAt = baseDate.AddDays(15); // after range

        var repository = new FakeRequestRepository([r1, r2, r3]);
        var service = new RequestService(repository);

        var result = await service.SearchAsync(
            new SearchRequestsQuery(
                CreatedFrom: baseDate,
                CreatedTo: baseDate.AddDays(10)),
            currentUserId: 1, isAdministrator: true);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(2, result.Items[0].Id);
    }

    // 8.8 Combined filters apply AND logic
    [Fact]
    public async Task Search_CombinedFilters_ApplyAndLogic()
    {
        var repository = new FakeRequestRepository(
        [
            CreateFull(1, RequestStatus.New,        RequestType.Legal),    // matches status, wrong type
            CreateFull(2, RequestStatus.InProgress, RequestType.Legal),    // matches both
            CreateFull(3, RequestStatus.InProgress, RequestType.General),  // matches status, wrong type
            CreateFull(4, RequestStatus.Completed,  RequestType.General),  // matches neither
        ]);

        var service = new RequestService(repository);
        var result = await service.SearchAsync(
            new SearchRequestsQuery(
                Status: [RequestStatus.InProgress],
                RequestType: [RequestType.Legal]),
            currentUserId: 1, isAdministrator: true);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(2, result.Items[0].Id);
    }

    // 8.9 TotalCount equals matching count before pagination; Items.Count <= PageSize
    [Fact]
    public async Task Search_Pagination_TotalCountBeforePagination()
    {
        var requests = Enumerable.Range(1, 5)
            .Select(i => CreateWithStatus(i, RequestStatus.New))
            .ToList();

        var repository = new FakeRequestRepository(requests);
        var service = new RequestService(repository);

        var result = await service.SearchAsync(
            new SearchRequestsQuery(Status: [RequestStatus.New], Page: 1, PageSize: 2),
            currentUserId: 1, isAdministrator: true);

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
    }

    // 8.10 Default sort is CreatedAt desc
    [Fact]
    public async Task Search_DefaultSort_IsCreatedAtDesc()
    {
        var baseDate = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        var r1 = Create(1, ownerId: 1, assignedTo: 0); r1.CreatedAt = baseDate;
        var r2 = Create(2, ownerId: 1, assignedTo: 0); r2.CreatedAt = baseDate.AddDays(2);
        var r3 = Create(3, ownerId: 1, assignedTo: 0); r3.CreatedAt = baseDate.AddDays(1);

        // Insert in non-sorted order
        var repository = new FakeRequestRepository([r1, r3, r2]);
        var service = new RequestService(repository);

        // Default query: SortBy=CreatedAt, SortDirection=desc
        var result = await service.SearchAsync(new SearchRequestsQuery(), currentUserId: 1, isAdministrator: true);

        Assert.Equal(3, result.Items.Count);
        Assert.Equal(2, result.Items[0].Id); // newest first
        Assert.Equal(3, result.Items[1].Id);
        Assert.Equal(1, result.Items[2].Id); // oldest last
    }

    // 8.11 Sort asc/desc by supported fields
    [Fact]
    public async Task Search_SortById_AscAndDesc()
    {
        var repository = new FakeRequestRepository(
        [
            Create(3, ownerId: 1, assignedTo: 0),
            Create(1, ownerId: 1, assignedTo: 0),
            Create(2, ownerId: 1, assignedTo: 0)
        ]);

        var service = new RequestService(repository);

        var asc = await service.SearchAsync(
            new SearchRequestsQuery(SortBy: "Id", SortDirection: "asc"),
            currentUserId: 1, isAdministrator: true);

        Assert.Equal(1, asc.Items[0].Id);
        Assert.Equal(2, asc.Items[1].Id);
        Assert.Equal(3, asc.Items[2].Id);

        var desc = await service.SearchAsync(
            new SearchRequestsQuery(SortBy: "Id", SortDirection: "desc"),
            currentUserId: 1, isAdministrator: true);

        Assert.Equal(3, desc.Items[0].Id);
        Assert.Equal(2, desc.Items[1].Id);
        Assert.Equal(1, desc.Items[2].Id);
    }

    // 8.12 Regular user + filter — ownership and filter apply together
    [Fact]
    public async Task Search_RegularUser_WithStatusFilter_SeesOnlyOwnedAndMatchingStatus()
    {
        var repository = new FakeRequestRepository(
        [
            CreateFull(1, RequestStatus.New,        RequestType.General), // owned, matches status
            CreateFull(2, RequestStatus.InProgress, RequestType.General), // owned, wrong status
            CreateFull(3, RequestStatus.New,        RequestType.General), // not owned, matches status
        ]);
        // override ownership
        var requests = new List<Request>
        {
            new() { Id = 1, OwnerId = 1, AssignedToUserId = 0, Status = RequestStatus.New,        RequestType = RequestType.General, RequestNumber = "REQ-001", CustomerId = 1, CreatedAt = DateTime.UtcNow },
            new() { Id = 2, OwnerId = 1, AssignedToUserId = 0, Status = RequestStatus.InProgress, RequestType = RequestType.General, RequestNumber = "REQ-002", CustomerId = 2, CreatedAt = DateTime.UtcNow },
            new() { Id = 3, OwnerId = 9, AssignedToUserId = 0, Status = RequestStatus.New,        RequestType = RequestType.General, RequestNumber = "REQ-003", CustomerId = 3, CreatedAt = DateTime.UtcNow },
        };

        var service = new RequestService(new FakeRequestRepository(requests));
        var result = await service.SearchAsync(
            new SearchRequestsQuery(Status: [RequestStatus.New]),
            currentUserId: 1, isAdministrator: false);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.Items[0].Id);
    }

    // 8.13 Page 2 returns correct items
    [Fact]
    public async Task Search_Page2_ReturnsCorrectItems()
    {
        var requests = Enumerable.Range(1, 5)
            .Select(i => Create(i, ownerId: 1, assignedTo: 0))
            .ToList();

        var service = new RequestService(new FakeRequestRepository(requests));
        var result = await service.SearchAsync(
            new SearchRequestsQuery(SortBy: "Id", SortDirection: "asc", Page: 2, PageSize: 2),
            currentUserId: 1, isAdministrator: true);

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(3, result.Items[0].Id);
        Assert.Equal(4, result.Items[1].Id);
    }

    // 8.14 No results — TotalCount is 0 and Items is empty
    [Fact]
    public async Task Search_NoMatchingResults_ReturnsTotalCountZero()
    {
        var repository = new FakeRequestRepository(
        [
            CreateWithStatus(1, RequestStatus.New),
            CreateWithStatus(2, RequestStatus.New)
        ]);

        var service = new RequestService(repository);
        var result = await service.SearchAsync(
            new SearchRequestsQuery(Status: [RequestStatus.Cancelled]),
            currentUserId: 1, isAdministrator: true);

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────────

    private static Request Create(int id, int ownerId, int assignedTo)
        => new()
        {
            Id = id,
            RequestNumber = $"REQ-{id:000}",
            CustomerId = id,
            OwnerId = ownerId,
            AssignedToUserId = assignedTo,
            Status = RequestStatus.New,
            RequestType = RequestType.General,
            CreatedAt = DateTime.UtcNow
        };

    private static Request CreateWithStatus(int id, RequestStatus status)
        => new()
        {
            Id = id,
            RequestNumber = $"REQ-{id:000}",
            CustomerId = id,
            OwnerId = 1,
            AssignedToUserId = 0,
            Status = status,
            RequestType = RequestType.General,
            CreatedAt = DateTime.UtcNow
        };

    private static Request CreateWithType(int id, RequestType type)
        => new()
        {
            Id = id,
            RequestNumber = $"REQ-{id:000}",
            CustomerId = id,
            OwnerId = 1,
            AssignedToUserId = 0,
            Status = RequestStatus.New,
            RequestType = type,
            CreatedAt = DateTime.UtcNow
        };

    private static Request CreateFull(int id, RequestStatus status, RequestType type)
        => new()
        {
            Id = id,
            RequestNumber = $"REQ-{id:000}",
            CustomerId = id,
            OwnerId = 1,
            AssignedToUserId = 0,
            Status = status,
            RequestType = type,
            CreatedAt = DateTime.UtcNow
        };

    // ────────────────────────────────────────────────────────────────────────
    // Fake repository
    // ────────────────────────────────────────────────────────────────────────

    private sealed class FakeRequestRepository : IRequestRepository
    {
        private readonly List<Request> _requests;

        public FakeRequestRepository(List<Request> requests)
        {
            _requests = requests;
        }

        public Task<List<Request>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_requests);

        public Task<PagedResult<Request>> SearchAsync(
            SearchRequestsQuery query,
            int currentUserId,
            bool isAdministrator,
            CancellationToken cancellationToken = default)
        {
            // 1. Start with all requests
            IEnumerable<Request> q = _requests;

            // 2. Ownership filter
            if (!isAdministrator)
            {
                q = q.Where(r => r.OwnerId == currentUserId || r.AssignedToUserId == currentUserId);
            }

            // 3. RequestNumber partial match (case-insensitive)
            if (!string.IsNullOrEmpty(query.RequestNumber))
            {
                q = q.Where(r => r.RequestNumber.Contains(query.RequestNumber, StringComparison.OrdinalIgnoreCase));
            }

            // 4. Status filter
            if (query.Status is { Length: > 0 })
            {
                q = q.Where(r => query.Status.Contains(r.Status));
            }

            // 5. RequestType filter
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
            var filtered = q.ToList();
            int totalCount = filtered.Count;

            // 9. Sorting
            bool ascending = string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

            IEnumerable<Request> sorted = query.SortBy.ToLowerInvariant() switch
            {
                "id"            => ascending ? filtered.OrderBy(r => r.Id)            : filtered.OrderByDescending(r => r.Id),
                "requestnumber" => ascending ? filtered.OrderBy(r => r.RequestNumber) : filtered.OrderByDescending(r => r.RequestNumber),
                "status"        => ascending ? filtered.OrderBy(r => r.Status)        : filtered.OrderByDescending(r => r.Status),
                "requesttype"   => ascending ? filtered.OrderBy(r => r.RequestType)   : filtered.OrderByDescending(r => r.RequestType),
                "createdat"     => ascending ? filtered.OrderBy(r => r.CreatedAt)     : filtered.OrderByDescending(r => r.CreatedAt),
                "ownerid"       => ascending ? filtered.OrderBy(r => r.OwnerId)       : filtered.OrderByDescending(r => r.OwnerId),
                // Unknown sort field falls through to CreatedAt desc (mirrors real repository)
                _               => filtered.OrderByDescending(r => r.CreatedAt),
            };

            // 10. Pagination
            var items = sorted
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToList();

            // 11. Return paged result
            return Task.FromResult(new PagedResult<Request>(items, query.Page, query.PageSize, totalCount));
        }
    }
}
