using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Requests.Application.Requests;
using Requests.Domain.Entities;

namespace Requests.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // כל endpoint דורש JWT תקין
public class RequestsController : ControllerBase
{
    private static readonly string[] AllowedSortFields =
        ["Id", "RequestNumber", "Status", "RequestType", "CreatedAt", "OwnerId"];

    private readonly IRequestService _service;

    public RequestsController(IRequestService service)
    {
        _service = service;
    }

    // זהות המשתמש מגיעה מה-JWT token — לא מ-headers שהקליינט שולח.
    // token נחתם בשרת, הקליינט לא יכול לזייף את תוכנו.
    private (int userId, bool isAdmin) GetCurrentUser()
    {
        var userIdClaim = User.FindFirst("userId")?.Value;
        var isAdminClaim = User.FindFirst("isAdmin")?.Value;

        if (!int.TryParse(userIdClaim, out var userId))
            throw new InvalidOperationException("userId claim is missing from token.");

        var isAdmin = string.Equals(isAdminClaim, "true", StringComparison.OrdinalIgnoreCase);
        return (userId, isAdmin);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RequestDto>>> Get(
        CancellationToken cancellationToken)
    {
        var (userId, isAdmin) = GetCurrentUser();
        var result = await _service.GetRequestsAsync(userId, isAdmin, cancellationToken);
        return Ok(result);
    }

    [HttpGet("search")]
    public async Task<ActionResult<PagedResult<RequestDto>>> Search(
        [FromQuery] SearchRequestsQuery query,
        CancellationToken cancellationToken)
    {
        var (userId, isAdmin) = GetCurrentUser();

        if (!ModelState.IsValid)
        {
            var allowedStatuses = string.Join(", ", Enum.GetNames<RequestStatus>());
            var allowedTypes = string.Join(", ", Enum.GetNames<RequestType>());
            return BadRequest(
                $"ערך enum לא חוקי. ערכי Status מותרים: {allowedStatuses}. " +
                $"ערכי RequestType מותרים: {allowedTypes}.");
        }

        if (query.Page < 1 || query.PageSize < 1 || query.PageSize > 200)
            return BadRequest("page חייב להיות >= 1, pageSize חייב להיות בין 1 ל-200.");

        if (query.CreatedFrom.HasValue && query.CreatedTo.HasValue
            && query.CreatedFrom.Value > query.CreatedTo.Value)
            return BadRequest("\"מתאריך\" חייב להיות לפני \"עד תאריך\".");

        if (!AllowedSortFields.Contains(query.SortBy, StringComparer.OrdinalIgnoreCase))
            return BadRequest(
                $"שדה מיון לא חוקי: '{query.SortBy}'. שדות מותרים: {string.Join(", ", AllowedSortFields)}.");

        if (!string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(query.SortDirection, "desc", StringComparison.OrdinalIgnoreCase))
            return BadRequest("כיוון מיון חייב להיות 'asc' או 'desc'.");

        var result = await _service.SearchAsync(query, userId, isAdmin, cancellationToken);
        return Ok(result);
    }
}
