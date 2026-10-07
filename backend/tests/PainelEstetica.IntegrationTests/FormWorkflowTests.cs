using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using PainelEstetica.Application.Forms;
using PainelEstetica.Application.Security;
using PainelEstetica.Domain.Enums;
using PainelEstetica.Infrastructure.Data;
using PainelEstetica.Infrastructure.Identity;
using Xunit;

namespace PainelEstetica.IntegrationTests;

public sealed class FormWorkflowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string ValidPassword = "Testing-password-2026!";
    private readonly ApiFactory factory = factory;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Published_versions_are_immutable_and_stale_drafts_are_rejected()
    {
        var username = $"forms-{Guid.NewGuid():N}";
        await CreateFormsUserAsync(username);
        using var client = factory.CreateClient();
        await LoginAsync(client, username);

        var createdResponse = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/forms", new
        {
            name = "Avaliação facial",
            category = "Anamnese",
            description = "Questionário inicial"
        });
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<FormTemplateDetail>(JsonOptions);
        Assert.NotNull(created);

        var firstDraftResponse = await SendWithCsrfAsync(client, HttpMethod.Put, $"/api/forms/{created.Id}/draft", new
        {
            draftRevision = created.DraftRevision,
            name = created.Name,
            category = created.Category,
            description = created.Description,
            schema = Schema("field_skin", "Tipo de pele")
        });
        Assert.Equal(HttpStatusCode.OK, firstDraftResponse.StatusCode);
        var firstDraft = await firstDraftResponse.Content.ReadFromJsonAsync<FormTemplateDetail>(JsonOptions);
        Assert.NotNull(firstDraft);
        Assert.Equal(2, firstDraft.DraftRevision);

        var firstPublishResponse = await SendWithCsrfAsync(client, HttpMethod.Post, $"/api/forms/{created.Id}/publish", new
        {
            draftRevision = firstDraft.DraftRevision,
            changeSummary = "Primeira versão clínica"
        });
        Assert.True(
            firstPublishResponse.StatusCode == HttpStatusCode.Created,
            $"Publish returned {(int)firstPublishResponse.StatusCode}: {await firstPublishResponse.Content.ReadAsStringAsync()}");
        var firstVersion = await firstPublishResponse.Content.ReadFromJsonAsync<FormVersionContract>(JsonOptions);
        Assert.NotNull(firstVersion);
        Assert.Equal(1, firstVersion.VersionNumber);

        var afterPublish = await client.GetFromJsonAsync<FormTemplateDetail>($"/api/forms/{created.Id}", JsonOptions);
        Assert.NotNull(afterPublish);
        Assert.Equal(FormTemplateStatus.Published, afterPublish.Status);

        var secondDraftResponse = await SendWithCsrfAsync(client, HttpMethod.Put, $"/api/forms/{created.Id}/draft", new
        {
            draftRevision = afterPublish.DraftRevision,
            name = afterPublish.Name,
            category = afterPublish.Category,
            description = afterPublish.Description,
            schema = new
            {
                fields = new[]
                {
                    Field("field_skin", "Tipo de pele"),
                    Field("field_allergy", "Possui alergias?")
                }
            }
        });
        Assert.Equal(HttpStatusCode.OK, secondDraftResponse.StatusCode);
        var secondDraft = await secondDraftResponse.Content.ReadFromJsonAsync<FormTemplateDetail>(JsonOptions);
        Assert.NotNull(secondDraft);

        var secondPublishResponse = await SendWithCsrfAsync(client, HttpMethod.Post, $"/api/forms/{created.Id}/publish", new
        {
            draftRevision = secondDraft.DraftRevision,
            changeSummary = "Incluído campo de alergias"
        });
        Assert.Equal(HttpStatusCode.Created, secondPublishResponse.StatusCode);

        var immutableFirstVersion = await client.GetFromJsonAsync<FormVersionContract>($"/api/forms/{created.Id}/versions/1", JsonOptions);
        Assert.NotNull(immutableFirstVersion);
        Assert.Single(immutableFirstVersion.Schema.Fields);

        var staleUpdate = await SendWithCsrfAsync(client, HttpMethod.Put, $"/api/forms/{created.Id}/draft", new
        {
            draftRevision = 1,
            name = created.Name,
            category = created.Category,
            description = created.Description,
            schema = Schema("field_stale", "Campo antigo")
        });
        Assert.Equal(HttpStatusCode.Conflict, staleUpdate.StatusCode);
    }

    private async Task CreateFormsUserAsync(string username)
    {
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = username,
            Email = $"{username}@example.invalid",
            EmailConfirmed = true,
            IsActive = true,
            MustChangePassword = false,
            CreatedAtUtc = DateTime.UtcNow,
            LockoutEnabled = true
        };
        Assert.True((await manager.CreateAsync(user, ValidPassword)).Succeeded);
        Assert.True((await manager.AddToRoleAsync(user, AppRoles.Receptionist)).Succeeded);

        var db = scope.ServiceProvider.GetRequiredService<EsteticaDbContext>();
        db.UserPermissionOverrides.AddRange(
            new UserPermissionOverride { UserId = user.Id, Permission = AppPermissions.FormRead, IsGranted = true, UpdatedAtUtc = DateTime.UtcNow },
            new UserPermissionOverride { UserId = user.Id, Permission = AppPermissions.FormManage, IsGranted = true, UpdatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    private static object Schema(string id, string label) => new { fields = new[] { Field(id, label) } };

    private static object Field(string id, string label) => new
    {
        id,
        type = "ShortText",
        label,
        description = "",
        required = false,
        placeholder = "",
        options = Array.Empty<object>()
    };

    private static async Task LoginAsync(HttpClient client, string username)
    {
        var response = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/auth/login", new
        {
            username,
            password = ValidPassword
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendWithCsrfAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        object payload)
    {
        var token = await client.GetFromJsonAsync<CsrfContract>("/api/auth/csrf");
        Assert.NotNull(token);
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(payload) };
        request.Headers.Add("X-CSRF-TOKEN", token.Token);
        return await client.SendAsync(request);
    }

    private sealed record CsrfContract(string Token);
    private sealed record FormTemplateDetail(
        Guid Id,
        string Name,
        string Category,
        string Description,
        FormTemplateStatus Status,
        int DraftRevision,
        FormSchemaDefinition DraftSchema);
    private sealed record FormVersionContract(
        int VersionNumber,
        FormSchemaDefinition Schema);
}
