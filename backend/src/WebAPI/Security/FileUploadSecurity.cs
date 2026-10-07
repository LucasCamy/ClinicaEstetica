using System.IO.Compression;
using System.Text;

namespace PainelEstetica.WebAPI.Security;

public static class FileUploadSecurity
{
    public const long MaxPhotoBytes = 25 * 1024 * 1024;
    public const long MaxDocumentBytes = 15 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, string> PhotoContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp"
        };

    private static readonly IReadOnlyDictionary<string, string> DocumentContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            [".csv"] = "text/csv"
        };

    public static async Task<ValidatedUpload> ValidatePhotoAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        ValidateCommon(file, MaxPhotoBytes);

        var extension = Path.GetExtension(file.FileName);
        if (!PhotoContentTypes.TryGetValue(extension, out var contentType))
        {
            throw new ArgumentException("Formato de imagem não permitido. Use JPG, PNG ou WEBP.");
        }

        var header = await ReadHeaderAsync(file, 12, cancellationToken);
        var hasValidSignature = extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".png" => header.Length >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            ".webp" => header.Length >= 12 &&
                       header.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
                       header.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };

        if (!hasValidSignature)
        {
            throw new ArgumentException("O conteúdo do arquivo não corresponde a uma imagem válida.");
        }

        return new ValidatedUpload(extension.ToLowerInvariant(), contentType);
    }

    public static async Task<ValidatedUpload> ValidateDocumentAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        ValidateCommon(file, MaxDocumentBytes);

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!DocumentContentTypes.TryGetValue(extension, out var contentType))
        {
            throw new ArgumentException("Formato não permitido. Use PDF, DOCX, XLSX ou CSV.");
        }

        switch (extension)
        {
            case ".pdf":
                await ValidatePdfContentAsync(file, cancellationToken);
                break;
            case ".docx":
                await ValidateOpenXmlAsync(file, "word/document.xml", cancellationToken);
                break;
            case ".xlsx":
                await ValidateOpenXmlAsync(file, "xl/workbook.xml", cancellationToken);
                break;
            case ".csv":
                await ValidateCsvAsync(file, cancellationToken);
                break;
        }

        return new ValidatedUpload(extension, contentType);
    }

    public static async Task<ValidatedUpload> ValidatePdfAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        ValidateCommon(file, MaxDocumentBytes);
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".pdf")
        {
            throw new ArgumentException("Envie somente arquivos PDF para termos digitais.");
        }
        await ValidatePdfContentAsync(file, cancellationToken);
        return new ValidatedUpload(".pdf", "application/pdf");
    }

    public static string GetDocumentContentType(string fileName) =>
        DocumentContentTypes.TryGetValue(Path.GetExtension(fileName), out var contentType)
            ? contentType
            : "application/octet-stream";

    public static string GetSafeOriginalFileName(string fileName)
    {
        var safeName = Path.GetFileName(fileName).Trim();
        if (string.IsNullOrWhiteSpace(safeName) || safeName.Length > 255)
        {
            throw new ArgumentException("O nome do arquivo é inválido ou excede 255 caracteres.");
        }
        return safeName;
    }

    public static string GetStorageDirectory(IConfiguration configuration, string category)
    {
        var configuredRoot = configuration["FileStorage:BasePath"] ?? "storage";
        var root = Path.IsPathRooted(configuredRoot)
            ? configuredRoot
            : Path.Combine(AppContext.BaseDirectory, configuredRoot);

        var directory = Path.GetFullPath(Path.Combine(root, category));
        var normalizedRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;

        if (!directory.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Diretório de armazenamento inválido.");
        }

        Directory.CreateDirectory(directory);
        return directory;
    }

    public static string ResolveStoredFile(
        IConfiguration configuration,
        string category,
        string storedFileName)
    {
        var directory = GetStorageDirectory(configuration, category);
        return Path.Combine(directory, Path.GetFileName(storedFileName));
    }

    private static void ValidateCommon(IFormFile? file, long maxBytes)
    {
        if (file is null || file.Length == 0)
        {
            throw new ArgumentException("Arquivo vazio ou inválido.");
        }

        if (file.Length > maxBytes)
        {
            throw new ArgumentException($"O arquivo excede o limite de {maxBytes / 1024 / 1024} MB.");
        }
    }

    private static async Task<byte[]> ReadHeaderAsync(
        IFormFile file,
        int length,
        CancellationToken cancellationToken)
    {
        var header = new byte[length];
        await using var stream = file.OpenReadStream();
        var bytesRead = await stream.ReadAsync(header.AsMemory(0, length), cancellationToken);
        return header[..bytesRead];
    }

    private static async Task ValidatePdfContentAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var header = await ReadHeaderAsync(file, 5, cancellationToken);
        if (header.Length < 5 || !header.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
        {
            throw new ArgumentException("O conteúdo enviado não é um PDF válido.");
        }
    }

    private static Task ValidateOpenXmlAsync(
        IFormFile file,
        string requiredEntry,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var archive = new ZipArchive(file.OpenReadStream(), ZipArchiveMode.Read);
            if (archive.Entries.Count is 0 or > 2048)
            {
                throw new ArgumentException("O documento Office possui uma estrutura inválida.");
            }

            const long maxExpandedBytes = 100L * 1024 * 1024;
            long expandedBytes = 0;
            var hasContentTypes = false;
            var hasRequiredEntry = false;

            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var normalizedName = entry.FullName.Replace('\\', '/');
                if (normalizedName.StartsWith('/') ||
                    normalizedName.Split('/', StringSplitOptions.RemoveEmptyEntries).Contains(".."))
                {
                    throw new ArgumentException("O documento Office contém caminhos inválidos.");
                }

                if (entry.Length < 0 || entry.Length > maxExpandedBytes - expandedBytes ||
                    entry.CompressedLength > 0 && entry.Length / Math.Max(1, entry.CompressedLength) > 200)
                {
                    throw new ArgumentException("O documento Office excede o limite seguro de expansão.");
                }
                expandedBytes += entry.Length;

                if (normalizedName.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase))
                {
                    hasContentTypes = true;
                }
                if (normalizedName.Equals(requiredEntry, StringComparison.OrdinalIgnoreCase))
                {
                    hasRequiredEntry = true;
                }
                if (normalizedName.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("Documentos com macros não são permitidos.");
                }
            }

            if (!hasContentTypes || !hasRequiredEntry)
            {
                throw new ArgumentException("O conteúdo não corresponde ao formato Office informado.");
            }
        }
        catch (InvalidDataException)
        {
            throw new ArgumentException("O documento Office está corrompido ou possui formato inválido.");
        }

        return Task.CompletedTask;
    }

    private static async Task ValidateCsvAsync(IFormFile file, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = file.OpenReadStream();
            using var reader = new StreamReader(
                stream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 4096,
                leaveOpen: false);
            var buffer = new char[4096];
            int read;
            while ((read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
            {
                if (buffer.AsSpan(0, read).Contains('\0'))
                {
                    throw new ArgumentException("O CSV contém conteúdo binário inválido.");
                }
            }
        }
        catch (DecoderFallbackException)
        {
            throw new ArgumentException("O CSV deve utilizar codificação UTF-8.");
        }
    }
}

public sealed record ValidatedUpload(string Extension, string ContentType);
