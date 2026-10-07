using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using PainelEstetica.Application.Security;
using PainelEstetica.Infrastructure.Data;
using PainelEstetica.Infrastructure.Identity;
using Xunit;

namespace PainelEstetica.IntegrationTests;

public sealed class SecurityBoundaryTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string ValidPassword = "Testing-password-2026!";
    private readonly ApiFactory _factory = factory;

    public static TheoryData<string> PrivateGetEndpoints => new()
    {
        "/api/clients",
        "/api/appointments",
        "/api/procedures",
        "/api/leads",
        "/api/reports/dashboard",
        "/api/finance/overview",
        "/api/finance/entries",
        "/api/users",
        "/api/audit",
        "/api/settings/operational",
        "/api/settings/storage",
        "/api/forms",
        "/api/forms/available",
        "/api/clients/11111111-1111-1111-1111-111111111111/clinical",
        "/api/clients/11111111-1111-1111-1111-111111111111/form-submissions",
        "/api/clients/11111111-1111-1111-1111-111111111111/photos/22222222-2222-2222-2222-222222222222/content"
    };

    [Theory]
    [MemberData(nameof(PrivateGetEndpoints))]
    public async Task Anonymous_requests_to_private_endpoints_are_unauthorized(string endpoint)
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync(endpoint);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/api/public/cms")]
    [InlineData("/api/public/procedures")]
    public async Task Expected_public_endpoints_are_available(string endpoint)
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync(endpoint);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/upload")]
    [InlineData("/uploads/probe.html")]
    public async Task Removed_public_upload_surfaces_are_not_found(string endpoint)
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync(endpoint);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Login_requires_antiforgery_token()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "missing",
            password = ValidPassword
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Receptionist_can_manage_basic_registration_and_schedule_but_not_clinical_data()
    {
        var username = Unique("reception");
        await CreateUserAsync(username, AppRoles.Receptionist);
        using var client = _factory.CreateClient();
        await LoginAsync(client, username);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/clients")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/appointments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/forms")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/forms/available")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/clients/11111111-1111-1111-1111-111111111111/clinical")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/clients/11111111-1111-1111-1111-111111111111/form-submissions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/reports/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/finance/overview")).StatusCode);
    }

    [Fact]
    public async Task Explicit_permission_override_is_enforced_server_side()
    {
        var username = Unique("override");
        var user = await CreateUserAsync(username, AppRoles.Receptionist);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EsteticaDbContext>();
            db.UserPermissionOverrides.Add(new UserPermissionOverride
            {
                UserId = user.Id,
                Permission = AppPermissions.ClinicalRead,
                IsGranted = true,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        await LoginAsync(client, username);
        var response = await client.GetAsync("/api/clients/11111111-1111-1111-1111-111111111111/clinical");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Temporary_password_blocks_business_endpoints_until_changed()
    {
        var username = Unique("password-change");
        await CreateUserAsync(username, AppRoles.Receptionist, mustChangePassword: true);
        using var client = _factory.CreateClient();
        await LoginAsync(client, username);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/appointments")).StatusCode);

        var changed = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/auth/change-password", new
        {
            currentPassword = ValidPassword,
            newPassword = "Changed-password-2026!"
        });
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/appointments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/finance/overview")).StatusCode);
    }

    [Fact]
    public async Task Professional_is_blocked_until_mfa_is_enabled()
    {
        var username = Unique("professional");
        var user = await CreateUserAsync(username, AppRoles.Professional);
        using var client = _factory.CreateClient();
        await LoginAsync(client, username);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/appointments")).StatusCode);

        var setupWithoutCsrf = await client.PostAsJsonAsync("/api/auth/mfa/setup", new { });
        Assert.Equal(HttpStatusCode.BadRequest, setupWithoutCsrf.StatusCode);
        var setup = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/auth/mfa/setup", new { });
        Assert.Equal(HttpStatusCode.OK, setup.StatusCode);
        var code = await GenerateMfaCodeAsync(user.Id);
        var enabled = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/auth/mfa/enable", new { code });
        Assert.True(
            enabled.StatusCode == HttpStatusCode.OK,
            $"MFA enable returned {(int)enabled.StatusCode}: {await enabled.Content.ReadAsStringAsync()}");
        var recoveryCodes = await enabled.Content.ReadFromJsonAsync<RecoveryCodesContract>();
        Assert.NotNull(recoveryCodes);
        Assert.Equal(8, recoveryCodes.RecoveryCodes.Count);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/appointments")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/finance/overview")).StatusCode);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await SendWithCsrfAsync(client, HttpMethod.Post, "/api/auth/logout", new { })).StatusCode);
        var login = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/auth/login", new
        {
            username,
            password = ValidPassword
        });
        Assert.Equal("RequiresTwoFactor", (await login.Content.ReadFromJsonAsync<LoginContract>())?.Status);
        var recoveryLogin = await SendWithCsrfAsync(
            client,
            HttpMethod.Post,
            "/api/auth/login/recovery",
            new { code = recoveryCodes.RecoveryCodes[0] });
        Assert.Equal(HttpStatusCode.OK, recoveryLogin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/appointments")).StatusCode);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await SendWithCsrfAsync(client, HttpMethod.Post, "/api/auth/logout", new { })).StatusCode);
        await SendWithCsrfAsync(client, HttpMethod.Post, "/api/auth/login", new
        {
            username,
            password = ValidPassword
        });
        var reusedRecoveryCode = await SendWithCsrfAsync(
            client,
            HttpMethod.Post,
            "/api/auth/login/recovery",
            new { code = recoveryCodes.RecoveryCodes[0] });
        Assert.Equal(HttpStatusCode.Unauthorized, reusedRecoveryCode.StatusCode);
    }

    [Fact]
    public async Task Admin_can_manage_users_and_access_change_is_audited()
    {
        var adminName = Unique("admin");
        await CreateUserAsync(adminName, AppRoles.Admin, mfaEnabled: true);
        using var client = _factory.CreateClient();
        await LoginAsync(client, adminName, expectMfa: true);

        var newUsername = Unique("managed");
        var create = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/users", new
        {
            username = newUsername,
            email = $"{newUsername}@example.invalid",
            temporaryPassword = "Managed-password-2026!",
            roles = new[] { AppRoles.Receptionist }
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<UserContract>();
        Assert.NotNull(created);

        var update = await SendWithCsrfAsync(client, HttpMethod.Put, $"/api/users/{created.Id}/access", new
        {
            roles = new[] { AppRoles.Receptionist },
            permissionOverrides = new[] { new { permission = AppPermissions.ReportRead, isGranted = true } }
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var audit = await client.GetFromJsonAsync<PagedContract<AuditContract>>("/api/audit?page=1&pageSize=100");
        Assert.NotNull(audit);
        Assert.Contains(audit.Items, item => item.Action == "User.AccessChanged" && item.ResourceId == created.Id.ToString());
    }

    [Fact]
    public async Task Identity_validation_error_is_localized_and_keeps_portuguese_characters_readable()
    {
        var adminName = Unique("localized-admin");
        await CreateUserAsync(adminName, AppRoles.Admin, mfaEnabled: true);
        using var client = _factory.CreateClient();
        await LoginAsync(client, adminName, expectMfa: true);

        var response = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/users", new
        {
            username = "teste teste",
            email = "teste@example.invalid",
            temporaryPassword = "Managed-password-2026!",
            roles = new[] { AppRoles.Receptionist }
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.Contains("nome de usuário", responseBody);
        Assert.DoesNotContain("Username", responseBody);
        Assert.DoesNotContain(@"\u00e", responseBody.ToLowerInvariant());
    }

    [Fact]
    public async Task Authenticated_mutation_without_antiforgery_is_rejected()
    {
        var username = Unique("csrf");
        await CreateUserAsync(username, AppRoles.Receptionist);
        using var client = _factory.CreateClient();
        await LoginAsync(client, username);
        var response = await client.PostAsJsonAsync("/api/clients", new
        {
            name = "Teste",
            phone = "67999999999"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Deactivated_user_is_denied_even_with_existing_cookie()
    {
        var username = Unique("disabled");
        var user = await CreateUserAsync(username, AppRoles.Receptionist);
        using var client = _factory.CreateClient();
        await LoginAsync(client, username);

        using (var scope = _factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var stored = await manager.FindByIdAsync(user.Id.ToString());
            Assert.NotNull(stored);
            stored.IsActive = false;
            await manager.UpdateAsync(stored);
            await manager.UpdateSecurityStampAsync(stored);
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/appointments")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    private async Task<ApplicationUser> CreateUserAsync(
        string username,
        string role,
        bool mustChangePassword = false,
        bool mfaEnabled = false)
    {
        using var scope = _factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = username,
            Email = $"{username}@example.invalid",
            EmailConfirmed = true,
            IsActive = true,
            MustChangePassword = mustChangePassword,
            CreatedAtUtc = DateTime.UtcNow,
            LockoutEnabled = true
        };
        Assert.True((await manager.CreateAsync(user, ValidPassword)).Succeeded);
        Assert.True((await manager.AddToRoleAsync(user, role)).Succeeded);
        if (mfaEnabled)
        {
            await manager.ResetAuthenticatorKeyAsync(user);
            Assert.True((await manager.SetTwoFactorEnabledAsync(user, true)).Succeeded);
        }
        return user;
    }

    private async Task LoginAsync(HttpClient client, string username, bool expectMfa = false)
    {
        var login = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/auth/login", new
        {
            username,
            password = ValidPassword
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var result = await login.Content.ReadFromJsonAsync<LoginContract>();
        Assert.NotNull(result);

        if (!expectMfa)
        {
            Assert.Equal("Authenticated", result.Status);
            return;
        }

        Assert.Equal("RequiresTwoFactor", result.Status);
        using var scope = _factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await manager.FindByNameAsync(username);
        Assert.NotNull(user);
        var code = await CreateAuthenticatorCodeAsync(manager, user);
        var mfa = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/auth/login/mfa", new { code });
        Assert.True(
            mfa.StatusCode == HttpStatusCode.OK,
            $"MFA login returned {(int)mfa.StatusCode}: {await mfa.Content.ReadAsStringAsync()}");
    }

    private async Task<string> GenerateMfaCodeAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await manager.FindByIdAsync(userId.ToString());
        Assert.NotNull(user);
        return await CreateAuthenticatorCodeAsync(manager, user);
    }

    private static async Task<string> CreateAuthenticatorCodeAsync(
        UserManager<ApplicationUser> manager,
        ApplicationUser user)
    {
        var sharedKey = await manager.GetAuthenticatorKeyAsync(user);
        Assert.False(string.IsNullOrWhiteSpace(sharedKey));
        var key = DecodeBase32(sharedKey);
        var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        var counterBytes = new byte[8];
        for (var index = 7; index >= 0; index--)
        {
            counterBytes[index] = (byte)(counter & 0xff);
            counter >>= 8;
        }

        var hash = HMACSHA1.HashData(key, counterBytes);
        var offset = hash[^1] & 0x0f;
        var binary = ((hash[offset] & 0x7f) << 24)
                     | (hash[offset + 1] << 16)
                     | (hash[offset + 2] << 8)
                     | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6");
    }

    private static byte[] DecodeBase32(string value)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var output = new List<byte>();
        var buffer = 0;
        var bitsLeft = 0;
        foreach (var character in value.TrimEnd('=').ToUpperInvariant())
        {
            var index = alphabet.IndexOf(character);
            if (index < 0) continue;
            buffer = (buffer << 5) | index;
            bitsLeft += 5;
            if (bitsLeft < 8) continue;
            bitsLeft -= 8;
            output.Add((byte)(buffer >> bitsLeft));
            buffer &= (1 << bitsLeft) - 1;
        }
        return output.ToArray();
    }

    private static async Task<HttpResponseMessage> SendWithCsrfAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        object payload)
    {
        var token = await client.GetFromJsonAsync<CsrfContract>("/api/auth/csrf");
        Assert.NotNull(token);
        using var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("X-CSRF-TOKEN", token.Token);
        return await client.SendAsync(request);
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..Math.Min(50, prefix.Length + 33)];

    private sealed record CsrfContract(string Token);
    private sealed record LoginContract(string Status);
    private sealed record RecoveryCodesContract(IReadOnlyList<string> RecoveryCodes);
    private sealed record UserContract(Guid Id);
    private sealed record AuditContract(string Action, string? ResourceId);
    private sealed record PagedContract<T>(IReadOnlyList<T> Items);
}
