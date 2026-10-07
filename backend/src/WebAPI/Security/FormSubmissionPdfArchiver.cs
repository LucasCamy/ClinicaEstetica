using System.Globalization;
using System.Text.Json;
using PainelEstetica.Application.Common;
using PainelEstetica.Application.Forms;
using PainelEstetica.Domain.Entities;
using PainelEstetica.Domain.Enums;
using PainelEstetica.Infrastructure.Data;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PainelEstetica.WebAPI.Security;

/// <summary>
/// Produz, exclusivamente no servidor, o artefato PDF associado à versão original
/// finalizada de um formulário. Nenhum conteúdo clínico é enviado a serviços externos.
/// </summary>
public sealed class FormSubmissionPdfArchiver(
    IConfiguration configuration,
    EsteticaDbContext db,
    StorageQuotaManager quotaManager,
    IClinicClock clinicClock) : IFormSubmissionPdfArchiver
{
    private const string StorageCategory = "forms";

    public async Task<FormFinalPdfSnapshot> ArchiveAsync(
        FormSubmission submission,
        FormSchemaDefinition schema,
        IReadOnlyList<FormSignature> signatures,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(submission);
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(signatures);
        if (submission.Client is null || submission.FormVersion is null)
        {
            throw new InvalidOperationException("Não foi possível montar o PDF sem os dados do cliente e da versão do formulário.");
        }

        var bytes = GeneratePdf(submission, schema, signatures);
        if (bytes.Length == 0 || bytes.Length > FileUploadSecurity.MaxDocumentBytes)
        {
            throw new BusinessRuleException("O PDF gerado para o formulário excede o limite de armazenamento permitido.");
        }

        var storedName = $"{Guid.NewGuid():N}.pdf";
        var target = FileUploadSecurity.ResolveStoredFile(configuration, StorageCategory, storedName);
        var temporary = FileUploadSecurity.ResolveStoredFile(configuration, "temporary", $"{Guid.NewGuid():N}.pdf");
        try
        {
            await WriteNewFileAsync(temporary, bytes, cancellationToken);
            var stored = await SecureFileWriter.DescribeAsync(temporary, cancellationToken);
            await using var quotaLease = await quotaManager.AcquireAsync(cancellationToken);
            await quotaManager.EnsureCanStoreAsync(db, submission.ClientId, stored.FileSizeBytes, cancellationToken);
            File.Move(temporary, target);
            return new FormFinalPdfSnapshot(storedName, stored.FileSizeBytes, stored.Sha256);
        }
        finally
        {
            SecureFileWriter.TryDelete(temporary);
        }
    }

    public void Delete(FormFinalPdfSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.StoredFileName)) return;
        var path = FileUploadSecurity.ResolveStoredFile(configuration, StorageCategory, snapshot.StoredFileName);
        SecureFileWriter.TryDelete(path);
    }

    private static async Task WriteNewFileAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private byte[] GeneratePdf(
        FormSubmission submission,
        FormSchemaDefinition schema,
        IReadOnlyList<FormSignature> signatures)
    {
        using var answersDocument = JsonDocument.Parse(submission.AnswersJson);
        var answers = answersDocument.RootElement;
        var signaturesByField = signatures
            .Where(item => item.FormSubmissionAmendmentId is null && item.AnswersHash == submission.AnswersHash)
            .GroupBy(item => item.FieldId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.CapturedAtUtc).Last(), StringComparer.Ordinal);

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(40);
                page.MarginVertical(36);
                page.DefaultTextStyle(style => style.FontSize(10).FontColor("#1f2937"));

                page.Header().Column(header =>
                {
                    header.Spacing(3);
                    header.Item().Text("FORMULÁRIO CLÍNICO FINALIZADO").FontSize(16).SemiBold().FontColor("#0f172a");
                    header.Item().Text(submission.FormVersion.Name).FontSize(12).SemiBold().FontColor("#334155");
                    header.Item().BorderBottom(1).BorderColor("#cbd5e1").PaddingBottom(8)
                        .Text($"Versão {submission.FormVersion.VersionNumber} • categoria: {submission.FormVersion.Category}")
                        .FontSize(8).FontColor("#64748b");
                });

                page.Content().PaddingVertical(14).Column(column =>
                {
                    column.Spacing(12);
                    column.Item().Element(InformationPanel).Column(panel =>
                    {
                        panel.Spacing(2);
                        panel.Item().Text("Identificação do preenchimento").SemiBold().FontSize(10).FontColor("#0f172a");
                        panel.Item().Text($"Cliente: {submission.Client.Name}");
                        if (!string.IsNullOrWhiteSpace(submission.Client.Cpf))
                        {
                            panel.Item().Text($"CPF: {submission.Client.Cpf}");
                        }
                        panel.Item().Text($"Finalizado em: {FormatDateTime(submission.FinalizedAtUtc)}");
                        panel.Item().Text($"Hash do formulário: {submission.FormVersion.SchemaHash}").FontSize(7).FontColor("#64748b");
                        panel.Item().Text($"Hash das respostas: {submission.AnswersHash}").FontSize(7).FontColor("#64748b");
                    });

                    foreach (var field in schema.Fields)
                    {
                        if (field.Type == FormFieldType.Section)
                        {
                            column.Item().PaddingTop(4).Text(field.Label).FontSize(13).SemiBold().FontColor("#0f172a");
                            if (!string.IsNullOrWhiteSpace(field.Description))
                            {
                                column.Item().Text(field.Description).FontSize(9).FontColor("#475569");
                            }
                            continue;
                        }

                        if (field.Type == FormFieldType.InformationalText)
                        {
                            column.Item().Element(InformationPanel).Text(string.Join("\n", new[] { field.Label, field.Description }.Where(value => !string.IsNullOrWhiteSpace(value))));
                            continue;
                        }

                        column.Item().Element(AnswerPanel).Column(answer =>
                        {
                            answer.Spacing(5);
                            answer.Item().Text(text =>
                            {
                                text.Span(field.Label).SemiBold();
                                if (field.Required) text.Span("  • obrigatório").FontSize(8).FontColor("#64748b");
                            });
                            if (!string.IsNullOrWhiteSpace(field.Description))
                            {
                                answer.Item().Text(field.Description).FontSize(8).FontColor("#64748b");
                            }

                            if (field.Type == FormFieldType.Signature)
                            {
                                if (signaturesByField.TryGetValue(field.Id, out var signature))
                                {
                                    answer.Item().Height(105).Border(1).BorderColor("#cbd5e1").Padding(4).Svg(signature.RenderedSvg).FitArea();
                                    answer.Item().Text($"Assinada por {signature.SignerName} em {FormatDateTime(signature.CapturedAtUtc)}")
                                        .FontSize(8).FontColor("#334155");
                                    answer.Item().Text($"Hash da assinatura: {signature.SignatureHash}").FontSize(7).FontColor("#64748b");
                                }
                                else
                                {
                                    answer.Item().Text("Assinatura não registrada.").Italic().FontColor("#64748b");
                                }
                            }
                            else
                            {
                                answer.Item().Text(FormatAnswer(field, answers)).FontSize(10).FontColor("#0f172a");
                            }
                        });
                    }

                    if (signatures.Count > 0)
                    {
                        column.Item().Element(InformationPanel).Text(
                            "Integridade: as assinaturas deste documento foram vinculadas aos hashes da versão do formulário e das respostas no momento da finalização.")
                            .FontSize(8).FontColor("#334155");
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Documento clínico imutável • página ").FontSize(8).FontColor("#64748b");
                    text.CurrentPageNumber().FontSize(8).FontColor("#64748b");
                    text.Span(" de ").FontSize(8).FontColor("#64748b");
                    text.TotalPages().FontSize(8).FontColor("#64748b");
                });
            });
        }).GeneratePdf();
    }

    private static IContainer InformationPanel(IContainer container) =>
        container.Border(1).BorderColor("#cbd5e1").Background("#f8fafc").Padding(10);

    private static IContainer AnswerPanel(IContainer container) =>
        container.Border(1).BorderColor("#dbe3ed").Padding(10);

    private static string FormatAnswer(FormFieldDefinition field, JsonElement answers)
    {
        if (answers.ValueKind != JsonValueKind.Object || !answers.TryGetProperty(field.Id, out var value))
        {
            return "Não informado";
        }

        return field.Type switch
        {
            FormFieldType.Date => FormatDate(value),
            FormFieldType.YesNo or FormFieldType.Checkbox => value.ValueKind == JsonValueKind.True ? "Sim" : value.ValueKind == JsonValueKind.False ? "Não" : "Não informado",
            FormFieldType.Dropdown => OptionLabel(field, value.GetString()),
            FormFieldType.CheckboxGroup or FormFieldType.MultiSelect => value.ValueKind == JsonValueKind.Array
                ? string.Join(", ", value.EnumerateArray().Select(item => OptionLabel(field, item.GetString())))
                : "Não informado",
            FormFieldType.Number => value.ValueKind == JsonValueKind.Number ? value.GetRawText() : "Não informado",
            _ => value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
                ? value.GetString()!.Trim()
                : "Não informado"
        };
    }

    private static string OptionLabel(FormFieldDefinition field, string? id) =>
        field.Options.FirstOrDefault(option => string.Equals(option.Id, id, StringComparison.Ordinal))?.Label
        ?? id
        ?? "Não informado";

    private static string FormatDate(JsonElement value) =>
        value.ValueKind == JsonValueKind.String && DateOnly.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("pt-BR"))
            : "Não informado";

    private string FormatDateTime(DateTime? value) =>
        value.HasValue
            ? TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.SpecifyKind(value.Value, DateTimeKind.Utc),
                    clinicClock.TimeZone)
                .ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("pt-BR"))
            : "Não informado";
}
