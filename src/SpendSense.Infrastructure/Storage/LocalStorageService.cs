using Microsoft.Extensions.Options;
using SpendSense.Application.Abstractions;
using SpendSense.Application.Options;

namespace SpendSense.Infrastructure.Storage;

public sealed class LocalStorageService(IOptions<StorageOptions> options) : IStorageService
{
    public async Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(options.Value.LocalPath);
        var safeName = $"{Guid.NewGuid():N}{Path.GetExtension(fileName)}";
        var path = Path.Combine(options.Value.LocalPath, safeName);
        await using var output = File.Create(path);
        await content.CopyToAsync(output, cancellationToken);
        return path.Replace('\\', '/');
    }

    public Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        if (File.Exists(storagePath)) File.Delete(storagePath);
        return Task.CompletedTask;
    }
}
