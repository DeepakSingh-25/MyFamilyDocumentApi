using Documents.Core.Interfaces;
using Documents.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Documents.Infrastructure.Services
{
    /// <summary>
    /// Private local folder (Storage:RootPath) — wwwroot nahi, static files ki tarah serve nahi hota.
    /// Key = "yyyy/MM/{guid}.{type}", hamesha server banata hai.
    /// </summary>
    public class LocalFileStorage(IOptions<StorageOptions> options, TimeProvider clock, ILogger<LocalFileStorage> logger) : IFileStorage
    {
        private string Root => Path.GetFullPath(options.Value.RootPath);

        public async Task<string> SaveAsync(Stream content, string fileType, CancellationToken ct)
        {
            var now = clock.GetUtcNow();
            var key = $"{now:yyyy}/{now:MM}/{Guid.NewGuid():N}.{fileType}";
            var path = ResolvePath(key);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            try
            {
                await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                await content.CopyToAsync(file, ct);
            }
            catch
            {
                // Adhoori file (cancel / disk full) na bache — iska koi key kahin save nahi hua
                Delete(key);
                throw;
            }
            return key;
        }

        public Stream? OpenRead(string storageKey)
        {
            var path = ResolvePath(storageKey);
            return File.Exists(path)
                ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
                : null;
        }

        public void Delete(string storageKey)
        {
            try
            {
                var path = ResolvePath(storageKey);
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                // Best effort — DB row pehle hi commit ho chuki hai; sirf key log karo (file ka naam nahi)
                logger.LogWarning(ex, "Could not delete stored file {StorageKey}.", storageKey);
            }
        }

        /// <summary>Path hamesha Root ke andar hi rahe (path traversal se bachao).</summary>
        private string ResolvePath(string storageKey)
        {
            var root = Root;
            var full = Path.GetFullPath(Path.Combine(root, storageKey));
            if (!full.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Invalid storage key.");
            return full;
        }
    }
}
