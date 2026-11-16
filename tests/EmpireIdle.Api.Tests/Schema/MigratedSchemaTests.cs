using EmpireIdle.Api.Tests.Infrastructure;
using EmpireIdle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace EmpireIdle.Api.Tests.Schema;

/// <summary>
/// Міграції створюють ту саму схему, яку описує модель.
/// Снапшот цього не гарантує: після ручної правки migrations add
/// розбіжності не бачить — так EquipmentRolls існувала лише в моделі.
/// </summary>
[Collection("postgres")]
public class MigratedSchemaTests
{
    /// <summary>Системна колонка: є в кожній таблиці, але information_schema її не показує.</summary>
    private const string RowVersionColumn = "xmin";

    private readonly TestApiFactory _factory;

    public MigratedSchemaTests(PostgresFixture postgres) => _factory = postgres.Factory;

    [Fact]
    public async Task Migrations_ShouldCreateEveryMappedColumn()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var expected = context.GetService<IDesignTimeModel>().Model.GetRelationalModel().Tables
            .Where(table => !table.IsExcludedFromMigrations)
            .SelectMany(table => table.Columns
                .Where(column => column.Name != RowVersionColumn)
                .Select(column => $"{table.Schema ?? "public"}.{table.Name}.{column.Name}"))
            .ToList();

        var actual = await context.Database
            .SqlQuery<string>($"""
                SELECT table_schema || '.' || table_name || '.' || column_name AS "Value"
                FROM information_schema.columns
                """)
            .ToListAsync();

        var missing = expected.Except(actual).Order().ToList();

        Assert.Empty(missing);
    }
}
