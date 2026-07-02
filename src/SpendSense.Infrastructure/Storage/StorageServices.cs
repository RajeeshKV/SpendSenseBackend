using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpendSense.Application.Abstractions;
using SpendSense.Application.Options;

namespace SpendSense.Infrastructure.Storage;

internal interface IStorageProvider
{
    string Name { get; }
    bool IsConfigured { get; }
    Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken = default);
    Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default);
}

internal sealed class ResilientStorageService(IEnumerable<IStorageProvider> providers, IOptions<StorageOptions> options, ILogger<ResilientStorageService> logger) : IStorageService
{
    public async Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        var orderedProviders = ResolveProviders().ToList();
        Exception? lastError = null;
        var payload = await ReadPayloadAsync(content, cancellationToken);

        foreach (var provider in orderedProviders)
        {
            if (!provider.IsConfigured)
            {
                logger.LogInformation("Storage provider {Provider} is not configured. Skipping.", provider.Name);
                continue;
            }

            try
            {
                await using var providerStream = new MemoryStream(payload, writable: false);
                return await provider.SaveAsync(providerStream, fileName, contentType, cancellationToken);
            }
            catch (Exception ex)
            {
                lastError = ex;
                logger.LogWarning(ex, "Storage provider {Provider} failed. Trying next configured provider.", provider.Name);
            }
        }

        throw new InvalidOperationException("No configured storage provider was able to save the file.", lastError);
    }

    public async Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var providerName = storagePath.Split(':', 2)[0];
        var provider = providers.FirstOrDefault(x => x.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase));
        if (provider is not null) await provider.DeleteAsync(storagePath, cancellationToken);
    }

    private IEnumerable<IStorageProvider> ResolveProviders()
    {
        var requested = new[] { options.Value.Provider, options.Value.BackupProvider, "Local" }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var name in requested)
        {
            var provider = providers.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (provider is not null) yield return provider;
        }
    }

    private static async Task<byte[]> ReadPayloadAsync(Stream content, CancellationToken cancellationToken)
    {
        if (content.CanSeek) content.Position = 0;
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }
}

internal sealed class LocalStorageProvider(IOptions<StorageOptions> options) : IStorageProvider
{
    public string Name => "Local";
    public bool IsConfigured => true;

    public async Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(options.Value.LocalPath);
        var safeName = $"{Guid.NewGuid():N}{Path.GetExtension(fileName)}";
        var path = Path.Combine(options.Value.LocalPath, safeName);
        await using var output = File.Create(path);
        await content.CopyToAsync(output, cancellationToken);
        return $"Local:{path.Replace('\\', '/')}";
    }

    public Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var path = storagePath.StartsWith("Local:", StringComparison.OrdinalIgnoreCase) ? storagePath[6..] : storagePath;
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }
}

internal sealed class SupabaseStorageProvider(HttpClient httpClient, IOptions<SupabaseOptions> options) : IStorageProvider
{
    public string Name => "Supabase";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.Url) && !string.IsNullOrWhiteSpace(options.Value.ServiceRoleKey);

    public async Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var safeName = $"{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}{Path.GetExtension(fileName)}";
        var url = $"{settings.Url.TrimEnd('/')}/storage/v1/object/{settings.StorageBucket}/{safeName}";
        using var request = new HttpRequestMessage(System.Net.Http.HttpMethod.Post, url);
        request.Headers.TryAddWithoutValidation("apikey", settings.ServiceRoleKey);
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {settings.ServiceRoleKey}");
        request.Content = new StreamContent(content);
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Supabase upload failed with {(int)response.StatusCode} {response.StatusCode}: {body}", null, response.StatusCode);
        }
        return $"Supabase:{settings.StorageBucket}/{safeName}";
    }

    public Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class CloudinaryStorageProvider(IOptions<CloudinaryOptions> options) : IStorageProvider
{
    private readonly CloudinaryOptions _options = options.Value;

    public string Name => "Cloudinary";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.CloudName) && !string.IsNullOrWhiteSpace(_options.ApiKey) && !string.IsNullOrWhiteSpace(_options.ApiSecret);

    public async Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var account = new Account(_options.CloudName, _options.ApiKey, _options.ApiSecret);
        var cloudinary = new Cloudinary(account);
        var result = await cloudinary.UploadAsync(new RawUploadParams
        {
            File = new FileDescription(fileName, content),
            Folder = _options.Folder,
            Type = "authenticated",
            UseFilename = true,
            UniqueFilename = true,
            Overwrite = false
        }, "raw", cancellationToken);

        if (result.Error is not null)
        {
            throw new InvalidOperationException($"Cloudinary upload failed: {result.Error.Message}");
        }

        return $"Cloudinary:{result.PublicId}";
    }

    public Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

