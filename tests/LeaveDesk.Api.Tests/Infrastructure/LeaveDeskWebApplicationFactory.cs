using LeaveDesk.Api.Infrastructure.Data;
using LeaveDesk.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LeaveDesk.Api.Tests.Infrastructure;

/// <summary>
/// Levanta la API completa en memoria (TestServer) contra una base SQLite en memoria,
/// sin depender de SQL Server ni de ningun servicio externo.
/// </summary>
public class LeaveDeskWebApplicationFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<LeaveDeskDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<LeaveDeskDbContext>(options => options.UseSqlite(_connection));

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<LeaveDeskDbContext>();
            context.Database.EnsureCreated();

            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            DataSeeder.SeedAsync(context, passwordHasher).GetAwaiter().GetResult();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection?.Dispose();
        }
    }
}
