using System.IO.Compression;
using System.Text;
using Microsoft.AspNetCore.Http;
using PainelEstetica.WebAPI.Security;
using Xunit;

namespace PainelEstetica.IntegrationTests;

public sealed class FileUploadSecurityTests
{
    [Fact]
    public async Task Valid_pdf_is_accepted()
    {
        var file = CreateFile("%PDF-1.7\nclinical test"u8.ToArray(), "termo.pdf", "application/pdf");

        var validated = await FileUploadSecurity.ValidateDocumentAsync(file, CancellationToken.None);

        Assert.Equal(".pdf", validated.Extension);
        Assert.Equal("application/pdf", validated.ContentType);
    }

    [Fact]
    public async Task Extension_without_matching_signature_is_rejected()
    {
        var file = CreateFile("not a pdf"u8.ToArray(), "termo.pdf", "application/pdf");

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            FileUploadSecurity.ValidateDocumentAsync(file, CancellationToken.None));

        Assert.Contains("PDF válido", exception.Message);
    }

    [Theory]
    [InlineData("documento.docx", "word/document.xml", ".docx")]
    [InlineData("planilha.xlsx", "xl/workbook.xml", ".xlsx")]
    public async Task Valid_open_xml_document_is_accepted(
        string fileName,
        string requiredEntry,
        string expectedExtension)
    {
        var file = CreateFile(CreateOpenXml(requiredEntry), fileName, "application/octet-stream");

        var validated = await FileUploadSecurity.ValidateDocumentAsync(file, CancellationToken.None);

        Assert.Equal(expectedExtension, validated.Extension);
    }

    [Fact]
    public async Task Office_document_with_macro_is_rejected()
    {
        var file = CreateFile(
            CreateOpenXml("word/document.xml", "word/vbaProject.bin"),
            "documento.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document");

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            FileUploadSecurity.ValidateDocumentAsync(file, CancellationToken.None));

        Assert.Contains("macros", exception.Message);
    }

    [Fact]
    public async Task Binary_content_disguised_as_csv_is_rejected()
    {
        var file = CreateFile(new byte[] { 0x41, 0x00, 0x42 }, "dados.csv", "text/csv");

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            FileUploadSecurity.ValidateDocumentAsync(file, CancellationToken.None));

        Assert.Contains("binário", exception.Message);
    }

    private static FormFile CreateFile(byte[] content, string fileName, string contentType)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private static byte[] CreateOpenXml(string requiredEntry, string? additionalEntry = null)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", "<Types />");
            WriteEntry(archive, requiredEntry, "<document />");
            if (additionalEntry is not null)
            {
                WriteEntry(archive, additionalEntry, "macro");
            }
        }
        return stream.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var entryStream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(content);
        entryStream.Write(bytes);
    }
}
