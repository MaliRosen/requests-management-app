using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Requests.Application.Requests;
using Requests.Infrastructure.Persistence;
using Requests.Infrastructure.Repositories;

namespace Requests.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services)
    {
        // SQLite: קובץ אחד על הדיסק — אינדקסים פועלים בפועל, נתונים נשמרים בין הפעלות.
        // להחלפה ל-SQL Server בפרודקשן: החלף ב-UseSqlServer(connectionString)
        services.AddDbContext<RequestsDbContext>(options =>
            options.UseSqlite("Data Source=requests.db"));

        services.AddScoped<IRequestRepository, RequestRepository>();
        services.AddScoped<IRequestService, RequestService>();

        return services;
    }
}
