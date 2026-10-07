using Microsoft.EntityFrameworkCore;
using PainelEstetica.Infrastructure.Data;
using PainelEstetica.Infrastructure.Data.Migrations;

const string initialMigration = "20260805185052_InitialSchema";
var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                       ?? Environment.GetEnvironmentVariable("DATABASE_URL")
                       ?? throw new InvalidOperationException("A connection string do banco é obrigatória para executar migrations.");

var options = new DbContextOptionsBuilder<EsteticaDbContext>()
    .UseNpgsql(connectionString, npgsql =>
        npgsql.MigrationsAssembly(typeof(EsteticaDbContext).Assembly.FullName))
    .Options;

await using var db = new EsteticaDbContext(options);
if (await LegacyDatabaseAdapter.UpgradeIfRequiredAsync(db, initialMigration))
{
    Console.WriteLine("Banco legado adaptado com preservação dos registros existentes.");
}

await db.Database.MigrateAsync();
Console.WriteLine("Migrations aplicadas com sucesso.");
