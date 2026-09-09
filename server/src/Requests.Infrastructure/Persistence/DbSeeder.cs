using Requests.Domain.Entities;

namespace Requests.Infrastructure.Persistence;

public static class DbSeeder
{
    // 100,000 רשומות — מדגים ביצועים ריאליים לדרישת "מיליוני רשומות"
    private const int RecordCount = 100_000;

    public static void Seed(RequestsDbContext db)
    {
        if (db.Requests.Any())
            return;

        var random = new Random(42);
        var statuses = Enum.GetValues<RequestStatus>();
        var types = Enum.GetValues<RequestType>();

        // הכנסה ב-batches של 1,000 — מונע טעינת כל הרשומות לזיכרון בבת אחת
        const int batchSize = 1_000;
        int totalBatches = RecordCount / batchSize;

        for (int batch = 0; batch < totalBatches; batch++)
        {
            var requests = Enumerable.Range(batch * batchSize + 1, batchSize)
                .Select(i => new Request
                {
                    RequestNumber = $"REQ-{i:000000}",
                    CustomerId = (i % 100) + 1,
                    OwnerId = (i % 5) + 1,
                    AssignedToUserId = i % 7 == 0 ? null : ((i + 1) % 5) + 1,
                    Status = statuses[random.Next(statuses.Length)],
                    RequestType = types[random.Next(types.Length)],
                    CreatedAt = DateTime.UtcNow.AddDays(-(i % 730)), // פיזור על שנתיים
                    UpdatedAt = DateTime.UtcNow.AddDays(-(i % 100))
                })
                .ToList();

            db.Requests.AddRange(requests);
            db.SaveChanges();
        }
    }
}
