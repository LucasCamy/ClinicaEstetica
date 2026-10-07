using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using PainelEstetica.Application.Forms;
using PainelEstetica.Application.Security;
using PainelEstetica.Domain.Entities;
using PainelEstetica.Domain.Enums;
using PainelEstetica.Infrastructure.Data;
using PainelEstetica.Infrastructure.Identity;
using Xunit;

namespace PainelEstetica.IntegrationTests;

public sealed class FormSubmissionWorkflowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string ValidPassword = "Testing-password-2026!";
    private readonly ApiFactory factory = factory;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Finalization_freezes_answers_and_amendment_preserves_original_with_new_signature()
    {
        var username = $"submission-{Guid.NewGuid():N}";
        var actorId = await CreateClinicalUserAsync(username);
        var seeded = await SeedPublishedFormAndClientAsync(actorId);
        using var client = factory.CreateClient();
        await LoginAsync(client, username);

        var available = await client.GetFromJsonAsync<IReadOnlyList<AvailableFormVersionDto>>(
            "/api/forms/available", JsonOptions);
        Assert.Contains(available!, item => item.FormVersionId == seeded.VersionId && item.VersionNumber == 1);

        var invalidAppointment = await SendWithCsrfAsync(client, HttpMethod.Post,
            $"/api/clients/{seeded.ClientId}/form-submissions",
            new { formVersionId = seeded.VersionId, appointmentId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalidAppointment.StatusCode);

        var create = await SendWithCsrfAsync(client, HttpMethod.Post,
            $"/api/clients/{seeded.ClientId}/form-submissions",
            new { formVersionId = seeded.VersionId, appointmentId = (Guid?)null });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var draft = await create.Content.ReadFromJsonAsync<SubmissionContract>(JsonOptions);
        Assert.NotNull(draft);
        Assert.Equal(FormSubmissionStatus.Draft, draft.Status);

        var summaries = await client.GetFromJsonAsync<IReadOnlyList<SubmissionSummaryContract>>(
            $"/api/clients/{seeded.ClientId}/form-submissions", JsonOptions);
        Assert.Contains(summaries!, item => item.Id == draft.Id && item.Status == FormSubmissionStatus.Draft);

        var detail = await client.GetFromJsonAsync<SubmissionContract>(
            $"/api/clients/{seeded.ClientId}/form-submissions/{draft.Id}", JsonOptions);
        Assert.Equal(draft.Id, detail!.Id);

        var answersV1 = Answers("Ana Paciente", "dry");
        var saved = await SendWithCsrfAsync(client, HttpMethod.Put,
            $"/api/clients/{seeded.ClientId}/form-submissions/{draft.Id}/draft",
            new { revision = draft.Revision, answers = answersV1 });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        draft = await saved.Content.ReadFromJsonAsync<SubmissionContract>(JsonOptions);
        Assert.NotNull(draft);

        var missingSignature = await SendWithCsrfAsync(client, HttpMethod.Post,
            $"/api/clients/{seeded.ClientId}/form-submissions/{draft.Id}/finalize",
            new { revision = draft.Revision, answers = answersV1, signatures = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.BadRequest, missingSignature.StatusCode);

        var invalidSigner = await SendWithCsrfAsync(client, HttpMethod.Post,
            $"/api/clients/{seeded.ClientId}/form-submissions/{draft.Id}/finalize",
            new { revision = draft.Revision, answers = answersV1, signatures = new[] { Signature(" ") } });
        Assert.Equal(HttpStatusCode.BadRequest, invalidSigner.StatusCode);

        var finalizedResponse = await SendWithCsrfAsync(client, HttpMethod.Post,
            $"/api/clients/{seeded.ClientId}/form-submissions/{draft.Id}/finalize",
            new { revision = draft.Revision, answers = answersV1, signatures = new[] { Signature("Ana Paciente") } });
        Assert.Equal(HttpStatusCode.OK, finalizedResponse.StatusCode);
        var finalized = await finalizedResponse.Content.ReadFromJsonAsync<SubmissionContract>(JsonOptions);
        Assert.NotNull(finalized);
        Assert.Equal(FormSubmissionStatus.Finalized, finalized.Status);
        Assert.Single(finalized.Signatures);
        Assert.Equal("Ana Paciente", finalized.OriginalAnswers.GetProperty("field_name").GetString());
        Assert.Equal(64, finalized.OriginalAnswersHash.Length);

        var finalPdf = await client.GetAsync($"/api/clients/{seeded.ClientId}/form-submissions/{draft.Id}/content");
        Assert.Equal(HttpStatusCode.OK, finalPdf.StatusCode);
        Assert.Equal("application/pdf", finalPdf.Content.Headers.ContentType?.MediaType);
        Assert.True((await finalPdf.Content.ReadAsByteArrayAsync()).AsSpan().StartsWith("%PDF-"u8));

        summaries = await client.GetFromJsonAsync<IReadOnlyList<SubmissionSummaryContract>>(
            $"/api/clients/{seeded.ClientId}/form-submissions", JsonOptions);
        Assert.Contains(summaries!, item => item.Id == draft.Id && item.IsSigned && item.Status == FormSubmissionStatus.Finalized);

        var signatureContent = await client.GetAsync(finalized.Signatures[0].ContentUrl);
        Assert.Equal(HttpStatusCode.OK, signatureContent.StatusCode);
        Assert.Equal("image/svg+xml", signatureContent.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("<svg", await signatureContent.Content.ReadAsStringAsync());

        var overwrite = await SendWithCsrfAsync(client, HttpMethod.Put,
            $"/api/clients/{seeded.ClientId}/form-submissions/{draft.Id}/draft",
            new { revision = finalized.Revision, answers = Answers("Nome sobrescrito", "oily") });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, overwrite.StatusCode);

        var answersV2 = Answers("Ana Paciente", "oily");
        var amendmentResponse = await SendWithCsrfAsync(client, HttpMethod.Post,
            $"/api/clients/{seeded.ClientId}/form-submissions/{draft.Id}/amend",
            new
            {
                revision = finalized.Revision,
                reason = "Paciente corrigiu a classificação informada durante a conferência.",
                answers = answersV2,
                signatures = new[] { Signature("Ana Paciente") }
            });
        Assert.Equal(HttpStatusCode.OK, amendmentResponse.StatusCode);
        var amended = await amendmentResponse.Content.ReadFromJsonAsync<SubmissionContract>(JsonOptions);
        Assert.NotNull(amended);
        Assert.Equal(FormSubmissionStatus.Amended, amended.Status);
        Assert.Equal("dry", amended.OriginalAnswers.GetProperty("field_skin").GetString());
        Assert.Equal("oily", amended.EffectiveAnswers.GetProperty("field_skin").GetString());
        Assert.Single(amended.Amendments);
        Assert.Equal(2, amended.Signatures.Count);
        Assert.NotEqual(amended.OriginalAnswersHash, amended.EffectiveAnswersHash);

        var voidResponse = await SendWithCsrfAsync(client, HttpMethod.Post,
            $"/api/clients/{seeded.ClientId}/form-submissions/{draft.Id}/void",
            new { revision = amended.Revision, reason = "Registro de teste anulado sem exclusão do histórico." });
        Assert.Equal(HttpStatusCode.OK, voidResponse.StatusCode);
        var voided = await voidResponse.Content.ReadFromJsonAsync<SubmissionContract>(JsonOptions);
        Assert.NotNull(voided);
        Assert.Equal(FormSubmissionStatus.Voided, voided.Status);
        Assert.Single(voided.Amendments);
        Assert.Equal(2, voided.Signatures.Count);
    }

    private async Task<Guid> CreateClinicalUserAsync(string username)
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
        foreach (var permission in new[]
                 {
                     AppPermissions.ClinicalRead,
                     AppPermissions.ClinicalManage,
                     AppPermissions.FormRead,
                     AppPermissions.FormManage
                 })
        {
            db.UserPermissionOverrides.Add(new UserPermissionOverride
            {
                UserId = user.Id,
                Permission = permission,
                IsGranted = true,
                UpdatedAtUtc = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<(Guid ClientId, Guid VersionId)> SeedPublishedFormAndClientAsync(Guid actorId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsteticaDbContext>();
        var client = new Client
        {
            Id = Guid.NewGuid(),
            Name = "Paciente de formulário",
            Phone = "11999999999",
            Email = "submission-patient@example.invalid",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var schema = new FormSchemaDefinition
        {
            Fields =
            [
                new FormFieldDefinition
                {
                    Id = "field_name", Type = FormFieldType.ShortText, Label = "Nome", Required = true
                },
                new FormFieldDefinition
                {
                    Id = "field_skin", Type = FormFieldType.Dropdown, Label = "Tipo de pele", Required = true,
                    Options =
                    [
                        new FormFieldOption { Id = "dry", Label = "Seca" },
                        new FormFieldOption { Id = "oily", Label = "Oleosa" }
                    ]
                },
                new FormFieldDefinition
                {
                    Id = "field_consent", Type = FormFieldType.Checkbox, Label = "Confirmo as informações", Required = true
                },
                new FormFieldDefinition
                {
                    Id = "field_signature", Type = FormFieldType.Signature, Label = "Assinatura", Required = true
                }
            ]
        };
        var normalized = FormSchemaCodec.Normalize(schema, true);
        var template = new FormTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Avaliação versionada",
            Category = "Teste clínico",
            Status = FormTemplateStatus.Published,
            DraftSchemaJson = FormSchemaCodec.Serialize(normalized),
            CreatedByUserId = actorId,
            UpdatedByUserId = actorId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        var version = new FormVersion
        {
            Id = Guid.NewGuid(),
            FormTemplateId = template.Id,
            Template = template,
            VersionNumber = 1,
            Name = template.Name,
            Category = template.Category,
            SchemaJson = FormSchemaCodec.Serialize(normalized),
            SchemaHash = FormSchemaCodec.Hash(normalized),
            ChangeSummary = "Versão de teste",
            PublishedByUserId = actorId,
            PublishedAtUtc = DateTime.UtcNow
        };
        db.Clients.Add(client);
        db.FormTemplates.Add(template);
        db.FormVersions.Add(version);
        await db.SaveChangesAsync();
        return (client.Id, version.Id);
    }

    private static Dictionary<string, object> Answers(string name, string skin) => new()
    {
        ["field_name"] = name,
        ["field_skin"] = skin,
        ["field_consent"] = true
    };

    private static object Signature(string signerName) => new
    {
        fieldId = "field_signature",
        signerName,
        pointerType = "pen",
        canvasWidth = 960,
        canvasHeight = 320,
        strokes = new[]
        {
            new[]
            {
                new { x = 0.1, y = 0.5 },
                new { x = 0.3, y = 0.4 },
                new { x = 0.6, y = 0.65 }
            }
        }
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
    private sealed record SubmissionSummaryContract(Guid Id, FormSubmissionStatus Status, bool IsSigned);
    private sealed record SignatureContract(Guid Id, string ContentUrl);
    private sealed record AmendmentContract(Guid Id, int AmendmentNumber);
    private sealed record SubmissionContract(
        Guid Id,
        FormSubmissionStatus Status,
        int Revision,
        JsonElement OriginalAnswers,
        string OriginalAnswersHash,
        JsonElement EffectiveAnswers,
        string EffectiveAnswersHash,
        IReadOnlyList<SignatureContract> Signatures,
        IReadOnlyList<AmendmentContract> Amendments);
}
