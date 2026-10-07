using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PainelEstetica.Domain.Entities;
using PainelEstetica.Infrastructure.Data;
using PainelEstetica.WebAPI.Security;
using SkiaSharp;
using Xunit;

namespace PainelEstetica.IntegrationTests;

public sealed class ClinicalFileStorageTests
{
    [Fact]
    public async Task Photo_is_resized_reencoded_and_hashed()
    {
        var bytes = CreateJpeg(4000, 2000);
        var file = CreateFile(bytes, "foto-4k.jpg", "image/jpeg");
        var target = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.jpg");

        try
        {
            var processed = await ClinicalPhotoProcessor.ProcessAsync(file, target, CancellationToken.None);

            Assert.Equal(".jpg", processed.Extension);
            Assert.Equal("image/jpeg", processed.ContentType);
            Assert.Equal(2560, processed.Width);
            Assert.Equal(1280, processed.Height);
            Assert.Equal(64, processed.Sha256.Length);
            Assert.Equal(new FileInfo(target).Length, processed.FileSizeBytes);

            using var output = SKCodec.Create(target);
            Assert.NotNull(output);
            Assert.Equal(SKEncodedImageFormat.Jpeg, output.EncodedFormat);
            Assert.Equal(2560, output.Info.Width);
            Assert.Equal(1280, output.Info.Height);
        }
        finally
        {
            SecureFileWriter.TryDelete(target);
        }
    }

    [Fact]
    public async Task Image_disguised_with_another_extension_is_rejected()
    {
        var file = CreateFile(CreatePng(40, 20), "foto.jpg", "image/jpeg");
        var target = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.jpg");

        try
        {
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                ClinicalPhotoProcessor.ProcessAsync(file, target, CancellationToken.None));

            Assert.Contains("conteúdo", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(target));
        }
        finally
        {
            SecureFileWriter.TryDelete(target);
        }
    }

    [Fact]
    public async Task Secure_writer_records_exact_size_and_sha256()
    {
        var bytes = "documento clínico"u8.ToArray();
        var file = CreateFile(bytes, "registro.csv", "text/csv");
        var target = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");

        try
        {
            var stored = await SecureFileWriter.WriteAsync(file, target, 1024, CancellationToken.None);

            Assert.Equal(bytes.Length, stored.FileSizeBytes);
            Assert.Equal("8f87bc8db35e7a02db0fa860349363d832a1b18125967052d9bce328196a5f42", stored.Sha256);
        }
        finally
        {
            SecureFileWriter.TryDelete(target);
        }
    }

    [Fact]
    public async Task Patient_quota_rejects_upload_that_would_exceed_limit()
    {
        var clientId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<EsteticaDbContext>()
            .UseInMemoryDatabase($"quota-{Guid.NewGuid():N}")
            .Options;
        await using var db = new EsteticaDbContext(options);
        db.ClientPhotos.Add(new ClientPhoto
        {
            ClientId = clientId,
            FilePath = "existing.jpg",
            FileSizeBytes = 8,
            Sha256 = new string('a', 64)
        });
        await db.SaveChangesAsync();

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FileStorage:MaxClientBytes"] = "10",
            ["FileStorage:MaxInstallationBytes"] = "100"
        }).Build();
        var quota = new StorageQuotaManager(configuration);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            quota.EnsureCanStoreAsync(db, clientId, 3, CancellationToken.None));

        Assert.Contains("paciente", exception.Message, StringComparison.OrdinalIgnoreCase);
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

    private static byte[] CreateJpeg(int width, int height) => CreateImage(width, height, SKEncodedImageFormat.Jpeg);

    private static byte[] CreatePng(int width, int height) => CreateImage(width, height, SKEncodedImageFormat.Png);

    private static byte[] CreateImage(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(164, 84, 112));
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }
}
