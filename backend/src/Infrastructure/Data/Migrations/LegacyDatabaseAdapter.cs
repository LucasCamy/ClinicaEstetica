using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace PainelEstetica.Infrastructure.Data.Migrations;

public static class LegacyDatabaseAdapter
{
    public static async Task<bool> UpgradeIfRequiredAsync(
        EsteticaDbContext db,
        string initialMigration,
        CancellationToken cancellationToken = default)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        if (!await IsLegacyDatabaseAsync((NpgsqlConnection)db.Database.GetDbConnection(), cancellationToken))
        {
            return false;
        }

        var sql = await ReadEmbeddedSqlAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync(
            sql.Replace("{{INITIAL_MIGRATION}}", initialMigration, StringComparison.Ordinal),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static async Task<bool> IsLegacyDatabaseAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                to_regclass('public."Users"') IS NOT NULL
                AND to_regclass('public."__EFMigrationsHistory"') IS NULL;
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task<string> ReadEmbeddedSqlAsync(CancellationToken cancellationToken)
    {
        var assembly = typeof(LegacyDatabaseAdapter).Assembly;
        var resource = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("LegacyToInitial.sql", StringComparison.Ordinal));
        await using var stream = assembly.GetManifestResourceStream(resource)
                                 ?? throw new InvalidOperationException("Adaptador SQL do banco legado não foi encontrado.");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
