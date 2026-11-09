using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// The differ, the migrations-assembly snapshot and the runtime model initializer are all
// EF-internal APIs. They are the only honest oracle for "did the model change at all": the
// same two services back the `dotnet ef migrations` design-time tooling, so trusting them here
// is trusting the same code path EF itself uses to decide whether a migration is needed.
#pragma warning disable EF1001
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;
#pragma warning restore EF1001

/// <summary>
/// Pins the operations EF's own differ finds between the migrations snapshot and the live model
/// against a recorded baseline: an added column, a renamed table or a changed default shows up
/// here as a diff against the fixture, not as a surprise migration.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ModelDiffBaselineTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Fixture path, copied to the test output by the csproj.</summary>
    private const string BaselinePath = "Fixtures/model-diff-baseline.txt";

    /// <summary>The differ's operations, one per line, must equal the recorded baseline.</summary>
    [Fact]
    public void ModelDiffersFromSnapshotExactlyAsRecorded()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Act
        var actual = Describe(context);
        var expected = File.ReadAllText(BaselinePath).Replace("\r\n", "\n").Trim();

        // Assert
        actual.Should().Be(expected,
            "the conformance work must not change the model; regenerate the fixture only with a schema change that has its own migration");
    }

    /// <summary>Renders the differ's output deterministically.</summary>
    /// <param name="context">The live context.</param>
    /// <returns>One operation per line, sorted, or "(no differences)".</returns>
#pragma warning disable EF1001 // the differ/snapshot/initializer trio is EF-internal; see the class remark above.
    private static string Describe(AppDbContext context)
    {
        var services = context.GetInfrastructure();
        var differ = services.GetRequiredService<IMigrationsModelDiffer>();
        var assembly = services.GetRequiredService<IMigrationsAssembly>();
        var initializer = services.GetRequiredService<IModelRuntimeInitializer>();
        var snapshot = assembly.ModelSnapshot?.Model
            ?? throw new InvalidOperationException("The migrations assembly has no model snapshot.");
        var snapshotModel = initializer.Initialize(snapshot);
        var operations = differ.GetDifferences(
            snapshotModel.GetRelationalModel(), context.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        if (operations.Count == 0)
        {
            return "(no differences)";
        }

        return string.Join('\n', operations.Select(Render).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// One line per operation naming what it touches. The operation types have no useful
    /// <c>ToString</c>; a column operation is rendered with its table, column, CLR type and
    /// nullability so a diff against the fixture says which column moved, not just that one did.
    /// </summary>
    /// <param name="operation">A differ operation.</param>
    /// <returns>A stable, comparable line.</returns>
    private static string Render(MigrationOperation operation)
    {
        return operation switch
        {
            ColumnOperation column =>
                $"{operation.GetType().Name} {column.Table}.{column.Name} {column.ClrType.Name} nullable={column.IsNullable}"
                + $" type={column.ColumnType} default={column.DefaultValue ?? column.DefaultValueSql}",
            TableOperation table => $"{operation.GetType().Name} {table.Name}",
            _ => operation.GetType().Name,
        };
    }
#pragma warning restore EF1001
}
