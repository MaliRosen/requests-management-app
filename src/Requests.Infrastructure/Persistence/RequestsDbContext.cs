using Microsoft.EntityFrameworkCore;
using Requests.Domain.Entities;

namespace Requests.Infrastructure.Persistence;

public class RequestsDbContext : DbContext
{
    public RequestsDbContext(DbContextOptions<RequestsDbContext> options) : base(options)
    {
    }

    public DbSet<Request> Requests => Set<Request>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Request>(entity =>
        {
            // אינדקסים בודדים — מייעלים סינון לפי שדה אחד
            entity.HasIndex(r => r.Status)
                  .HasDatabaseName("IX_Requests_Status");

            entity.HasIndex(r => r.RequestType)
                  .HasDatabaseName("IX_Requests_RequestType");

            entity.HasIndex(r => r.CreatedAt)
                  .HasDatabaseName("IX_Requests_CreatedAt");

            entity.HasIndex(r => r.RequestNumber)
                  .HasDatabaseName("IX_Requests_RequestNumber");

            entity.HasIndex(r => r.AssignedToUserId)
                  .HasDatabaseName("IX_Requests_AssignedToUserId");

            // אינדקס מורכב — מייעל את הקייס הנפוץ ביותר:
            // משתמש רגיל מסנן לפי ownership ואז ממיין לפי תאריך
            // WHERE OwnerId = X ORDER BY CreatedAt DESC
            entity.HasIndex(r => new { r.OwnerId, r.CreatedAt })
                  .HasDatabaseName("IX_Requests_OwnerId_CreatedAt");
        });
    }
}
