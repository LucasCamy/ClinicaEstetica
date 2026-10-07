using Microsoft.EntityFrameworkCore;
using Npgsql;
using PainelEstetica.Infrastructure.Data;
using PainelEstetica.Infrastructure.Data.Migrations;
using Testcontainers.PostgreSql;
using Xunit;

namespace PainelEstetica.IntegrationTests;

public sealed class PostgreSqlMigrationTests : IAsyncLifetime
{
    private const string InitialMigration = "20260805185052_InitialSchema";
    private const string BrandLogoMigration = "20260810170000_AddLandingBrandLogos";
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16.9-alpine")
        .WithDatabase("migration_tests")
        .WithUsername("migration_user")
        .WithPassword("migration-tests-only-password")
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();
    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task Migrations_create_a_clean_database_from_zero()
    {
        var connectionString = await CreateDatabaseAsync("clean_schema");
        await using var db = CreateContext(connectionString);
        Assert.False(await LegacyDatabaseAdapter.UpgradeIfRequiredAsync(db, InitialMigration));
        await db.Database.MigrateAsync();

        Assert.Contains(InitialMigration, await db.Database.GetAppliedMigrationsAsync());
        Assert.Equal(3, await db.ProcedureTypes.CountAsync());
        var content = await db.CmsContents.SingleAsync();
        Assert.Equal("/brand/leilaine-arakaki-logo-light.png", content.LogoOnDarkUrl);
        Assert.Equal("/brand/leilaine-arakaki-logo-dark.png", content.LogoOnLightUrl);
        Assert.Contains(BrandLogoMigration, await db.Database.GetAppliedMigrationsAsync());
        Assert.True(await TableExistsAsync(connectionString, "AuditEvents"));
        Assert.True(await TableExistsAsync(connectionString, "UserPermissionOverrides"));
    }

    [Fact]
    public async Task Legacy_adapter_preserves_records_and_registers_the_baseline()
    {
        var connectionString = await CreateDatabaseAsync("legacy_upgrade");
        var legacySql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "LegacySchema.sql"));
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(legacySql, connection);
            await command.ExecuteNonQueryAsync();
        }

        await using var db = CreateContext(connectionString);
        Assert.True(await LegacyDatabaseAdapter.UpgradeIfRequiredAsync(db, InitialMigration));
        await db.Database.MigrateAsync();

        var legacyUser = await db.Users.AsNoTracking().SingleAsync(user => user.UserName == "legacy-admin");
        var legacyClient = await db.Clients.AsNoTracking().SingleAsync(client => client.Name == "Cliente legado preservado");
        Assert.Equal("legacy-hash-preserved", legacyUser.PasswordHash);
        Assert.True(legacyUser.MustChangePassword);
        Assert.Equal("12345678901", legacyClient.Cpf);
        Assert.Equal(new DateOnly(1990, 5, 20), legacyClient.BirthDate);
        Assert.Contains(InitialMigration, await db.Database.GetAppliedMigrationsAsync());
        Assert.Equal(1, await CountAsync(connectionString, "SELECT COUNT(*) FROM \"UserRoles\";"));
    }

    private EsteticaDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<EsteticaDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(EsteticaDbContext).Assembly.FullName))
            .Options;
        return new EsteticaDbContext(options);
    }

    private async Task<string> CreateDatabaseAsync(string name)
    {
        var adminConnection = new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
        {
            Database = "postgres"
        };
        await using var connection = new NpgsqlConnection(adminConnection.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\";", connection);
        await command.ExecuteNonQueryAsync();
        var target = new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString()) { Database = name };
        return target.ConnectionString;
    }

    private static async Task<bool> TableExistsAsync(string connectionString, string table)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT to_regclass(@name) IS NOT NULL;", connection);
        command.Parameters.AddWithValue("name", $"public.\"{table}\"");
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    private static async Task<long> CountAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }
}
