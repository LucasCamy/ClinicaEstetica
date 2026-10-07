using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PainelEstetica.IntegrationTests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"PainelEstetica-Testing-{Guid.NewGuid():N}";
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        "PainelEsteticaTests",
        Guid.NewGuid().ToString("N"));

    public ApiFactory()
    {
        Environment.SetEnvironmentVariable("Database__UseInMemory", "true");
        Environment.SetEnvironmentVariable("Database__ApplyMigrations", "false");
        Environment.SetEnvironmentVariable("BootstrapAdmin__Username", "bootstrap-admin");
        Environment.SetEnvironmentVariable("BootstrapAdmin__Email", "bootstrap-admin@example.invalid");
        Environment.SetEnvironmentVariable("BootstrapAdmin__Password", "Bootstrap-password-2026!");
        Environment.SetEnvironmentVariable("DataProtection__KeysPath", Path.Combine(_tempRoot, "keys"));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:UseInMemory"] = "true",
                ["Database:ApplyMigrations"] = "false",
                ["Database:InMemoryName"] = _databaseName,
                ["BootstrapAdmin:Username"] = "bootstrap-admin",
                ["BootstrapAdmin:Email"] = "bootstrap-admin@example.invalid",
                ["BootstrapAdmin:Password"] = "Bootstrap-password-2026!",
                ["FileStorage:BasePath"] = Path.Combine(_tempRoot, "storage"),
                ["DataProtection:KeysPath"] = Path.Combine(_tempRoot, "keys"),
                ["Clinic:TimeZoneId"] = "America/Cuiaba",
                ["RateLimiting:AuthPermitLimit"] = "100"
            });
        });
    }
}
