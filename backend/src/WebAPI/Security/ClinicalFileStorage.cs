using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using PainelEstetica.Infrastructure.Data;
using SkiaSharp;

namespace PainelEstetica.WebAPI.Security;

public static class ClinicalPhotoProcessor
{
    public const int MaxOutputDimension = 2560;
    public const long MaxDecodedPixels = 32_000_000;
    private const int JpegQuality = 84;

    public static async Task<ProcessedPhoto> ProcessAsync(
        IFormFile file,
        string targetPath,
        CancellationToken cancellationToken)
    {
        var validated = await FileUploadSecurity.ValidatePhotoAsync(file, cancellationToken);
        await using var input = file.OpenReadStream();
        using var codec = SKCodec.Create(input)
            ?? throw new ArgumentException("Não foi possível decodificar a imagem enviada.");

        EnsureCodecMatchesExtension(codec.EncodedFormat, validated.Extension);
        if (codec.FrameCount > 1)
        {
            throw new ArgumentException("Imagens animadas não são permitidas no prontuário.");
        }

        var sourceInfo = codec.Info;
        var pixelCount = checked((long)sourceInfo.Width * sourceInfo.Height);
        if (sourceInfo.Width <= 0 || sourceInfo.Height <= 0 || pixelCount > MaxDecodedPixels)
        {
            throw new ArgumentException(
                "A imagem possui dimensões inválidas ou excede 32 megapixels. Reduza a resolução e tente novamente.");
        }

        var decodeInfo = new SKImageInfo(
            sourceInfo.Width,
            sourceInfo.Height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul);
        using var decoded = new SKBitmap(decodeInfo);
        var decodeResult = codec.GetPixels(decodeInfo, decoded.GetPixels());
        if (decodeResult != SKCodecResult.Success)
        {
            throw new ArgumentException("A imagem está corrompida, incompleta ou utiliza um formato não suportado.");
        }

        using var oriented = ApplyOrientation(decoded, codec.EncodedOrigin, SKColors.White);
        var scale = Math.Min(1d, (double)MaxOutputDimension / Math.Max(oriented.Width, oriented.Height));
        var outputWidth = Math.Max(1, (int)Math.Round(oriented.Width * scale));
        var outputHeight = Math.Max(1, (int)Math.Round(oriented.Height * scale));

        using var resized = scale < 1d
            ? oriented.Resize(
                new SKImageInfo(outputWidth, outputHeight, SKColorType.Bgra8888, SKAlphaType.Premul),
                new SKSamplingOptions(SKCubicResampler.Mitchell))
            : null;
        var outputBitmap = resized ?? oriented;
        if (outputBitmap.Width != outputWidth || outputBitmap.Height != outputHeight)
        {
            throw new ArgumentException("Não foi possível redimensionar a imagem com segurança.");
        }

        using var image = SKImage.FromBitmap(outputBitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality)
            ?? throw new ArgumentException("Não foi possível normalizar a imagem enviada.");

        await using (var output = new FileStream(
                         targetPath,
                         FileMode.CreateNew,
                         FileAccess.Write,
                         FileShare.None,
                         81920,
                         FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            encoded.SaveTo(output);
            await output.FlushAsync(cancellationToken);
        }

        var stored = await SecureFileWriter.DescribeAsync(targetPath, cancellationToken);
        return new ProcessedPhoto(
            ".jpg",
            "image/jpeg",
            file.Length,
            stored.FileSizeBytes,
            stored.Sha256,
            outputWidth,
            outputHeight);
    }

    internal static SKBitmap ApplyOrientation(SKBitmap source, SKEncodedOrigin origin, SKColor background)
    {
        var swapsAxes = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or
            SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var target = new SKBitmap(
            swapsAxes ? source.Height : source.Width,
            swapsAxes ? source.Width : source.Height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul);

        using var canvas = new SKCanvas(target);
        canvas.Clear(background);
        canvas.SetMatrix(CreateOrientationMatrix(origin, source.Width, source.Height));
        canvas.DrawBitmap(source, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
        canvas.Flush();
        return target;
    }

    private static SKMatrix CreateOrientationMatrix(SKEncodedOrigin origin, int width, int height) => origin switch
    {
        SKEncodedOrigin.TopRight => Matrix(-1, 0, width, 0, 1, 0),
        SKEncodedOrigin.BottomRight => Matrix(-1, 0, width, 0, -1, height),
        SKEncodedOrigin.BottomLeft => Matrix(1, 0, 0, 0, -1, height),
        SKEncodedOrigin.LeftTop => Matrix(0, 1, 0, 1, 0, 0),
        SKEncodedOrigin.RightTop => Matrix(0, -1, height, 1, 0, 0),
        SKEncodedOrigin.RightBottom => Matrix(0, -1, height, -1, 0, width),
        SKEncodedOrigin.LeftBottom => Matrix(0, 1, 0, -1, 0, width),
        _ => SKMatrix.Identity
    };

    private static SKMatrix Matrix(
        float scaleX,
        float skewX,
        float transX,
        float skewY,
        float scaleY,
        float transY) => new()
    {
        ScaleX = scaleX,
        SkewX = skewX,
        TransX = transX,
        SkewY = skewY,
        ScaleY = scaleY,
        TransY = transY,
        Persp0 = 0,
        Persp1 = 0,
        Persp2 = 1
    };

    internal static void EnsureCodecMatchesExtension(SKEncodedImageFormat format, string extension)
    {
        var matches = extension switch
        {
            ".jpg" or ".jpeg" => format == SKEncodedImageFormat.Jpeg,
            ".png" => format == SKEncodedImageFormat.Png,
            ".webp" => format == SKEncodedImageFormat.Webp,
            _ => false
        };
        if (!matches)
        {
            throw new ArgumentException("A extensão da imagem não corresponde ao conteúdo decodificado.");
        }
    }
}

public static class BrandLogoProcessor
{
    public const long MaxInputBytes = 5 * 1024 * 1024;
    public const int MinWidth = 320;
    public const int MinHeight = 80;
    public const int MaxOutputWidth = 1600;
    public const int MaxOutputHeight = 600;
    private const long MaxDecodedPixels = 12_000_000;

    public static async Task<ProcessedPhoto> ProcessAsync(
        IFormFile file,
        string targetPath,
        CancellationToken cancellationToken)
    {
        if (file.Length > MaxInputBytes)
        {
            throw new ArgumentException("A logo excede o limite de 5 MB.");
        }

        var validated = await FileUploadSecurity.ValidatePhotoAsync(file, cancellationToken);
        await using var input = file.OpenReadStream();
        using var codec = SKCodec.Create(input)
            ?? throw new ArgumentException("Não foi possível decodificar a logo enviada.");

        ClinicalPhotoProcessor.EnsureCodecMatchesExtension(codec.EncodedFormat, validated.Extension);
        if (codec.FrameCount > 1)
        {
            throw new ArgumentException("Logos animadas não são permitidas.");
        }

        var sourceInfo = codec.Info;
        var pixelCount = checked((long)sourceInfo.Width * sourceInfo.Height);
        if (sourceInfo.Width < MinWidth || sourceInfo.Height < MinHeight)
        {
            throw new ArgumentException($"A logo deve ter no mínimo {MinWidth} × {MinHeight} pixels.");
        }
        if (pixelCount > MaxDecodedPixels)
        {
            throw new ArgumentException("A logo excede 12 megapixels. Reduza a resolução e tente novamente.");
        }

        var decodeInfo = new SKImageInfo(
            sourceInfo.Width,
            sourceInfo.Height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul);
        using var decoded = new SKBitmap(decodeInfo);
        var decodeResult = codec.GetPixels(decodeInfo, decoded.GetPixels());
        if (decodeResult != SKCodecResult.Success)
        {
            throw new ArgumentException("A logo está corrompida, incompleta ou utiliza um formato não suportado.");
        }

        using var oriented = ClinicalPhotoProcessor.ApplyOrientation(decoded, codec.EncodedOrigin, SKColors.Transparent);
        var scale = Math.Min(
            1d,
            Math.Min((double)MaxOutputWidth / oriented.Width, (double)MaxOutputHeight / oriented.Height));
        var outputWidth = Math.Max(1, (int)Math.Round(oriented.Width * scale));
        var outputHeight = Math.Max(1, (int)Math.Round(oriented.Height * scale));
        using var resized = scale < 1d
            ? oriented.Resize(
                new SKImageInfo(outputWidth, outputHeight, SKColorType.Bgra8888, SKAlphaType.Premul),
                new SKSamplingOptions(SKCubicResampler.Mitchell))
            : null;
        var outputBitmap = resized ?? oriented;

        using var image = SKImage.FromBitmap(outputBitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new ArgumentException("Não foi possível normalizar a logo enviada.");
        await using (var output = new FileStream(
                         targetPath,
                         FileMode.CreateNew,
                         FileAccess.Write,
                         FileShare.None,
                         81920,
                         FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            encoded.SaveTo(output);
            await output.FlushAsync(cancellationToken);
        }

        var stored = await SecureFileWriter.DescribeAsync(targetPath, cancellationToken);
        return new ProcessedPhoto(
            ".png",
            "image/png",
            file.Length,
            stored.FileSizeBytes,
            stored.Sha256,
            outputWidth,
            outputHeight);
    }
}

public static class SecureFileWriter
{
    public static async Task<StoredFileDetails> WriteAsync(
        IFormFile file,
        string targetPath,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        long totalBytes = 0;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];

        try
        {
            await using var input = file.OpenReadStream();
            await using var output = new FileStream(
                targetPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                buffer.Length,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            int read;
            while ((read = await input.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
            {
                totalBytes += read;
                if (totalBytes > maxBytes)
                {
                    throw new ArgumentException($"O arquivo excede o limite de {maxBytes / 1024 / 1024} MB.");
                }
                hash.AppendData(buffer, 0, read);
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
            await output.FlushAsync(cancellationToken);
            return new StoredFileDetails(totalBytes, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
        }
        catch
        {
            TryDelete(targetPath);
            throw;
        }
    }

    public static async Task<StoredFileDetails> DescribeAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return new StoredFileDetails(stream.Length, Convert.ToHexString(hash).ToLowerInvariant());
    }

    public static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
            // A limpeza será retomada pela rotina de manutenção futura.
        }
        catch (UnauthorizedAccessException)
        {
            // A falha original não deve ser ocultada por uma falha de limpeza.
        }
    }
}

public sealed class StorageQuotaManager(IConfiguration configuration)
{
    public const long DefaultClientLimitBytes = 500L * 1024 * 1024;
    public const long DefaultInstallationLimitBytes = 20L * 1024 * 1024 * 1024;
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        return new QuotaLease(gate);
    }

    public async Task<StorageUsageSnapshot> GetUsageAsync(
        EsteticaDbContext db,
        Guid clientId,
        CancellationToken cancellationToken)
    {
        var clientPhotos = await db.ClientPhotos
            .Where(photo => photo.ClientId == clientId)
            .SumAsync(photo => (long?)photo.FileSizeBytes, cancellationToken) ?? 0;
        var clientDocuments = await db.ClientDocuments
            .Where(document => document.ClientId == clientId)
            .SumAsync(document => (long?)document.FileSizeBytes, cancellationToken) ?? 0;
        var clientTerms = await db.TermSubmissions
            .Where(term => term.ClientId == clientId && term.FinalPdfFileSizeBytes.HasValue)
            .SumAsync(term => term.FinalPdfFileSizeBytes ?? 0, cancellationToken);
        var clientForms = await db.FormSubmissions
            .Where(form => form.ClientId == clientId && form.FinalPdfFileSizeBytes.HasValue)
            .SumAsync(form => form.FinalPdfFileSizeBytes ?? 0, cancellationToken);
        var allPhotos = await db.ClientPhotos
            .SumAsync(photo => (long?)photo.FileSizeBytes, cancellationToken) ?? 0;
        var allDocuments = await db.ClientDocuments
            .SumAsync(document => (long?)document.FileSizeBytes, cancellationToken) ?? 0;
        var allTerms = await db.TermSubmissions
            .Where(term => term.FinalPdfFileSizeBytes.HasValue)
            .SumAsync(term => term.FinalPdfFileSizeBytes ?? 0, cancellationToken);
        var allForms = await db.FormSubmissions
            .Where(form => form.FinalPdfFileSizeBytes.HasValue)
            .SumAsync(form => form.FinalPdfFileSizeBytes ?? 0, cancellationToken);

        return new StorageUsageSnapshot(
            checked(clientPhotos + clientDocuments + clientTerms + clientForms),
            GetPositiveLimit("FileStorage:MaxClientBytes", DefaultClientLimitBytes),
            checked(allPhotos + allDocuments + allTerms + allForms),
            GetPositiveLimit("FileStorage:MaxInstallationBytes", DefaultInstallationLimitBytes));
    }

    public async Task EnsureCanStoreAsync(
        EsteticaDbContext db,
        Guid clientId,
        long pendingBytes,
        CancellationToken cancellationToken)
    {
        if (pendingBytes <= 0) throw new ArgumentException("O arquivo processado está vazio.");
        var usage = await GetUsageAsync(db, clientId, cancellationToken);
        if (pendingBytes > usage.ClientLimitBytes - usage.ClientUsedBytes)
        {
            throw new ArgumentException(
                $"O armazenamento deste paciente atingiria o limite de {FormatMegabytes(usage.ClientLimitBytes)} MB.");
        }
        if (pendingBytes > usage.InstallationLimitBytes - usage.InstallationUsedBytes)
        {
            throw new ArgumentException(
                $"O armazenamento da clínica atingiria o limite de {FormatMegabytes(usage.InstallationLimitBytes)} MB. Libere espaço ou aumente a quota antes de continuar.");
        }
    }

    public async Task<StorageUsageSnapshot> GetInstallationUsageAsync(
        EsteticaDbContext db,
        CancellationToken cancellationToken)
    {
        var allPhotos = await db.ClientPhotos
            .SumAsync(photo => (long?)photo.FileSizeBytes, cancellationToken) ?? 0;
        var allDocuments = await db.ClientDocuments
            .SumAsync(document => (long?)document.FileSizeBytes, cancellationToken) ?? 0;
        var allTerms = await db.TermSubmissions
            .Where(term => term.FinalPdfFileSizeBytes.HasValue)
            .SumAsync(term => term.FinalPdfFileSizeBytes ?? 0, cancellationToken);
        var allForms = await db.FormSubmissions
            .Where(form => form.FinalPdfFileSizeBytes.HasValue)
            .SumAsync(form => form.FinalPdfFileSizeBytes ?? 0, cancellationToken);

        return new StorageUsageSnapshot(
            0,
            GetPositiveLimit("FileStorage:MaxClientBytes", DefaultClientLimitBytes),
            checked(allPhotos + allDocuments + allTerms + allForms),
            GetPositiveLimit("FileStorage:MaxInstallationBytes", DefaultInstallationLimitBytes));
    }

    private long GetPositiveLimit(string key, long defaultValue)
    {
        var value = configuration.GetValue<long?>(key) ?? defaultValue;
        if (value <= 0) throw new InvalidOperationException($"A configuração {key} deve ser maior que zero.");
        return value;
    }

    private static long FormatMegabytes(long bytes) => bytes / 1024 / 1024;

    private sealed class QuotaLease(SemaphoreSlim semaphore) : IAsyncDisposable
    {
        private SemaphoreSlim? current = semaphore;

        public ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref current, null)?.Release();
            return ValueTask.CompletedTask;
        }
    }
}

public sealed record ProcessedPhoto(
    string Extension,
    string ContentType,
    long OriginalFileSizeBytes,
    long FileSizeBytes,
    string Sha256,
    int Width,
    int Height);

public sealed record StoredFileDetails(long FileSizeBytes, string Sha256);

public sealed record StorageUsageSnapshot(
    long ClientUsedBytes,
    long ClientLimitBytes,
    long InstallationUsedBytes,
    long InstallationLimitBytes);
