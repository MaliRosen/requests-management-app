# Implementation Plan: Requests Search & Filter

## Overview

Implement DB-level search, filter, sort and pagination on the existing `GET /api/requests` endpoint, add a full Angular frontend, and write unit tests. The work follows the existing clean-architecture layers (Domain → Application → Infrastructure → Api) and keeps all filtering logic in `IQueryable` chains so nothing is loaded into memory before filtering.

---

## Tasks

- [x] 1. Add shared data-contract types in `Requests.Application`
  - Create `src/Requests.Application/Requests/SearchRequestsQuery.cs` — a `sealed record` with fields: `RequestNumber?`, `RequestStatus[]? Status`, `RequestType[]? RequestType`, `DateTime? CreatedFrom`, `DateTime? CreatedTo`, `string SortBy = "CreatedAt"`, `string SortDirection = "desc"`, `int Page = 1`, `int PageSize = 20`
  - Create `src/Requests.Application/Requests/PagedResult.cs` — a generic `sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)`
  - _Requirements: 3.1, 4.1, 4.4_

- [x] 2. Extend `IRequestRepository` and `IRequestService` interfaces
  - Add `Task<PagedResult<Request>> SearchAsync(SearchRequestsQuery query, int currentUserId, bool isAdministrator, CancellationToken cancellationToken = default)` to `IRequestRepository`
  - Add `Task<PagedResult<RequestDto>> SearchAsync(SearchRequestsQuery query, int currentUserId, bool isAdministrator, CancellationToken cancellationToken = default)` to `IRequestService`
  - Keep the existing `GetAllAsync` / `GetRequestsAsync` intact — do not remove them
  - _Requirements: 1.7, 2.3, 3.5, 4.5_

- [x] 3. Implement `RequestRepository.SearchAsync` in `Requests.Infrastructure`
  - Open `src/Requests.Infrastructure/Repositories/RequestRepository.cs`
  - Implement `SearchAsync` building a single `IQueryable<Request>` pipeline in this order:
    1. `_db.Requests.AsQueryable()`
    2. Ownership filter: if `!isAdministrator` → `WHERE OwnerId == userId OR AssignedToUserId == userId`
    3. `RequestNumber` partial match: if provided → `WHERE RequestNumber.Contains(value)` (EF Core translates to SQL `LIKE`)
    4. Status filter: if provided → `WHERE Status IN (...)`
    5. RequestType filter: if provided → `WHERE RequestType IN (...)`
    6. `CreatedFrom`: if provided → `WHERE CreatedAt >= value`
    7. `CreatedTo`: if provided → `WHERE CreatedAt <= value`
    8. `totalCount = await query.CountAsync()`
    9. Sorting: switch on `query.SortBy` (`Id`, `RequestNumber`, `Status`, `RequestType`, `CreatedAt`, `OwnerId`) + direction — use `OrderBy`/`OrderByDescending`; unknown field falls through to `CreatedAt desc`
    10. Pagination: `.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)`
    11. `items = await query.ToListAsync()`
    12. Return `new PagedResult<Request>(items, query.Page, query.PageSize, totalCount)`
  - _Requirements: 1.7, 2.3, 3.5, 4.5_

- [x] 4. Implement `RequestService.SearchAsync` in `Requests.Application`
  - Open `src/Requests.Application/Requests/RequestService.cs`
  - Add `SearchAsync` method: call `_repository.SearchAsync(query, currentUserId, isAdministrator, cancellationToken)`, project each `Request` to `RequestDto`, return `new PagedResult<RequestDto>(dtos, pagedRequests.Page, pagedRequests.PageSize, pagedRequests.TotalCount)`
  - _Requirements: 1.1–1.6, 2.1–2.2, 3.1–3.4, 4.1–4.4_

- [x] 5. Update `RequestsController` — add validation and new endpoint
  - Open `src/Requests.Api/Controllers/RequestsController.cs`
  - Add JWT authentication — `[Authorize]` על ה-Controller. זהות המשתמש נקראת מה-token claims (`userId`, `isAdmin`), לא מ-headers.
  - Add a new `[HttpGet("search")]` action that:
    - Accepts `[FromQuery] SearchRequestsQuery query`
    - Validates inputs:
      - `page < 1` or `pageSize < 1` or `pageSize > 200` → `400` with message
      - `createdFrom > createdTo` (when both supplied) → `400` with message
      - `sortBy` not in `["Id","RequestNumber","Status","RequestType","CreatedAt","OwnerId"]` (case-insensitive) → `400` with allowed-field list
      - `sortDirection` not `"asc"` or `"desc"` (case-insensitive) → `400`
    - Calls `_service.SearchAsync(query, userId, isAdmin, cancellationToken)`
    - Returns `Ok(result)` (`PagedResult<RequestDto>`)
  - Note: `RequestStatus` and `RequestType` enum binding errors are handled automatically by ASP.NET model binding; add a custom `[ModelStateInvalidFilter]` or check `ModelState` and return 400 with allowed values if needed
  - _Requirements: 1.1–1.6, 2.4, 4.6, 5.1–5.6_

- [x] 6. Add CORS policy in `Program.cs`
  - Open `src/Requests.Api/Program.cs`
  - Register a named CORS policy (e.g. `"angular-dev"`) allowing `http://localhost:4200`, all headers, all methods
  - Apply `app.UseCors("angular-dev")` before `app.MapControllers()`
  - _Requirements: 6.1_

- [x] 7. Checkpoint — build and verify backend compiles
  - Run `dotnet build` from the repo root; ensure zero errors before proceeding to tests.

- [x] 8. Update `FakeRequestRepository` and add `SearchAsync` unit tests
  - Open `tests/Requests.Tests/RequestServiceTests.cs`
  - Extend `FakeRequestRepository` (inner class) to implement the new `SearchAsync` signature — execute the filters in-memory so the fake matches the real behaviour for unit-test purposes (iterate the list, apply each predicate in sequence, count, sort, paginate)
  - Add the following test cases for `RequestService.SearchAsync`:
    - [x]* 8.1 Admin sees all requests regardless of ownership
      - _Requirements: 2.1, 9.1_
    - [x]* 8.2 RegularUser sees only owned or assigned requests
      - _Requirements: 2.2, 9.1_
    - [x]* 8.3 Filter by `RequestNumber` partial match (case-insensitive)
      - _Requirements: 1.1, 9.1_
    - [x]* 8.4 Filter by single `Status`
      - _Requirements: 1.2, 9.1_
    - [x]* 8.5 Filter by multiple `Status` values
      - _Requirements: 1.2, 9.1_
    - [x]* 8.6 Filter by `RequestType`
      - _Requirements: 1.3, 9.1_
    - [x]* 8.7 Filter by `CreatedFrom` and `CreatedTo` date range
      - _Requirements: 1.4, 1.5, 9.1_
    - [x]* 8.8 Combined filters apply AND logic — result is intersection of all active filters
      - _Requirements: 1.6, 9.3_
    - [x]* 8.9 `PagedResult.TotalCount` equals matching record count before pagination; `Items.Count <= PageSize`
      - _Requirements: 4.4, 9.4_
    - [x] 8.10 Default sort is `CreatedAt desc`
      - _Requirements: 3.3, 9.1_
    - [x]* 8.11 Sort `asc` / `desc` by supported fields
      - _Requirements: 3.1, 3.2, 3.4, 9.1_

- [x] 9. Checkpoint — run backend tests
  - Run `dotnet test`; all tests must pass before proceeding to frontend.

- [x] 10. Scaffold Angular project
  - From the repo root, create `frontend/` directory
  - Run `ng new requests-frontend --directory frontend --routing false --style scss --standalone false` (or equivalent Angular CLI command)
  - Confirm `src/app/app.module.ts` imports `HttpClientModule`, `ReactiveFormsModule`, `FormsModule`
  - _Requirements: 6.1_

- [x] 11. Create Angular models
  - Create `frontend/src/app/models/request.model.ts` exporting:
    - `RequestStatus` enum (`New=1, InProgress=2, Completed=3, Cancelled=4`)
    - `RequestType` enum (`General=1, Legal=2, Payment=3, Appeal=4`)
    - `RequestDto` interface matching the backend DTO fields
  - Create `frontend/src/app/models/paged-result.model.ts` exporting `PagedResult<T>` interface with `items`, `page`, `pageSize`, `totalCount`
  - Create `frontend/src/app/models/search-query.model.ts` exporting `SearchQuery` interface with all filter/sort/pagination fields
  - _Requirements: 6.1, 7.1, 8.1_

- [x] 12. Implement `RequestsService` (Angular)
  - Create `frontend/src/app/services/requests.service.ts`
  - Inject `HttpClient`; base URL configurable via `environment.apiUrl` (default: `http://localhost:5000`)
  - Implement `search(query: SearchQuery): Observable<PagedResult<RequestDto>>` that builds `HttpParams` from the query object and issues `GET /api/requests/search` with `Authorization: Bearer <token>` header (מוסף אוטומטית על ידי `authInterceptor`)
  - _Requirements: 6.2, 6.3, 6.4_

- [x] 13. Implement `SearchFilterComponent`
  - Generate `frontend/src/app/components/search-filter/search-filter.component.ts`
  - Use a `ReactiveFormsModule` `FormGroup` with controls: `requestNumber` (text), `status` (multi-select), `requestType` (multi-select), `createdFrom` (date), `createdTo` (date)
  - Emit `filterChange: EventEmitter<Partial<SearchQuery>>` on value changes
  - Apply `debounceTime(500)` to the `requestNumber` control; emit immediately for dropdown/date changes
  - Template: `search-filter.component.html` with labelled inputs, dropdowns populated from enum values
  - _Requirements: 6.1, 6.2_

- [x] 14. Implement `RequestsTableComponent`
  - Generate `frontend/src/app/components/requests-table/requests-table.component.ts`
  - Accept `@Input() items: RequestDto[]` and `@Input() sortBy: string` and `@Input() sortDirection: string`
  - Emit `sortChange: EventEmitter<{sortBy: string, sortDirection: string}>` when a column header is clicked — toggle direction if same column, default `asc` if new column
  - Columns: `Id`, `RequestNumber`, `Status`, `RequestType`, `CreatedAt`, `OwnerId`
  - Display ▲ / ▼ indicator on the currently sorted column
  - Show "לא נמצאו בקשות התואמות לחיפוש" when `items` is empty
  - _Requirements: 7.1, 7.2, 7.3, 7.4_

- [x] 15. Implement `PaginationComponent`
  - Generate `frontend/src/app/components/pagination/pagination.component.ts`
  - Accept `@Input() page: number`, `@Input() pageSize: number`, `@Input() totalCount: number`
  - Emit `pageChange: EventEmitter<number>`
  - Display: "עמוד {page}" and "סה״כ {totalCount} רשומות", "הקודם" button (disabled when `page === 1`), "הבא" button (disabled when `page * pageSize >= totalCount`)
  - _Requirements: 8.1, 8.2, 8.3, 8.4_

- [x] 16. Wire everything in `AppComponent`
  - Open `frontend/src/app/app.component.ts`
  - Inject `RequestsService`
  - Maintain state: `query: SearchQuery`, `result: PagedResult<RequestDto> | null`, `loading: boolean`, `error: string | null`
  - Hard-code demo credentials: `userId = 1`, `isAdmin = true` (configurable later)
  - On `filterChange` from `SearchFilterComponent`: merge changes into `query`, reset `page` to 1, call `loadRequests()`
  - On `sortChange` from `RequestsTableComponent`: update `query.sortBy` and `query.sortDirection`, call `loadRequests()`
  - On `pageChange` from `PaginationComponent`: update `query.page`, call `loadRequests()`
  - `loadRequests()`: set `loading = true`, subscribe to `RequestsService.search(...)`, on success set `result` and clear `error`, on error set `error` message, always set `loading = false`
  - Template: spinner while `loading`, error message while `error`, `SearchFilterComponent`, `RequestsTableComponent`, `PaginationComponent`
  - _Requirements: 6.2, 6.3, 6.4, 6.5, 7.2, 7.3, 8.2, 8.5_

- [x] 17. Checkpoint — verify frontend builds
  - Run `ng build` from `frontend/`; fix any TypeScript/template errors before proceeding.

- [x] 18. Create `README.md`
  - Create `README.md` at the repo root covering:
    - Project overview and architecture summary
    - Prerequisites (`.NET 8 SDK`, `Node.js`, `Angular CLI`)
    - How to run the backend: `dotnet run --project src/Requests.Api`
    - How to run the frontend: `cd frontend && npm install && ng serve`
    - How to run backend tests: `dotnet test`
    - Available API query parameters with types and defaults
    - Authentication: JWT Bearer token מתקבל מ-`POST /api/auth/login`
  - _Requirements: (documentation)_

- [x] 19. Create `AI-usage.md`
  - Create `AI-usage.md` at the repo root documenting:
    - Which AI tools were used and for what purpose
    - Prompts or interactions that shaped key design decisions
    - Any AI-generated code that was reviewed/modified and why
  - _Requirements: (documentation)_

- [x] 20. Final checkpoint — full build and test pass
  - Run `dotnet build` and `dotnet test` from the repo root; confirm all tests are green.
  - Run `ng build` from `frontend/`; confirm no build errors.

## Notes

- Tasks marked with `*` are optional and can be skipped for a faster MVP
- Backend tasks (1–9) must be completed before frontend tasks (10–17)
- The existing `GetAllAsync` / `GetRequestsAsync` flow is left untouched — no regressions
- CORS policy in task 6 is required for the Angular dev server (`localhost:4200`) to reach the API
- The `FakeRequestRepository` in tests must mirror the `IQueryable` semantics in memory so unit tests remain meaningful without a real DB


## Task Dependency Graph

```json
{
  "waves": [
    { "wave": 1, "tasks": ["1"] },
    { "wave": 2, "tasks": ["2"] },
    { "wave": 3, "tasks": ["3"] },
    { "wave": 4, "tasks": ["4"] },
    { "wave": 5, "tasks": ["5"] },
    { "wave": 6, "tasks": ["6"] },
    { "wave": 7, "tasks": ["7"] },
    { "wave": 8, "tasks": ["8"] },
    { "wave": 9, "tasks": ["9"] },
    { "wave": 10, "tasks": ["10"] },
    { "wave": 11, "tasks": ["11"] },
    { "wave": 12, "tasks": ["12"] },
    { "wave": 13, "tasks": ["13", "14", "15"] },
    { "wave": 14, "tasks": ["16"] },
    { "wave": 15, "tasks": ["17"] },
    { "wave": 16, "tasks": ["18", "19"] },
    { "wave": 17, "tasks": ["20"] }
  ]
}
```
