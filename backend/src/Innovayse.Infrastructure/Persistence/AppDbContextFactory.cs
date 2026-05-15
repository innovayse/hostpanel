namespace Innovayse.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

/// <summary>
/// Design-time factory used by EF Core tooling (dotnet ef migrations) to create
/// an <see cref="AppDbContext"/> without starting the full application host.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    /// <summary>
    /// Creates an <see cref="AppDbContext"/> configured with a placeholder connection string
    /// suitable for generating migrations at design time.
    /// </summary>
    /// <param name="args">Command-line arguments passed by the tooling (unused).</param>
    /// <returns>A configured <see cref="AppDbContext"/> instance.</returns>
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=innovayse_dev;Username=postgres;Password=password",
                npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .Options;

        return new AppDbContext(options);
    }
}
