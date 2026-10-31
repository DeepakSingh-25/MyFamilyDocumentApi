namespace Documents.Core.Rules
{
    /// <summary>
    /// frontend/src/app/core/file-rules.ts ka server-side version: extension, size aur asli file header.
    /// Client ka Content-Type trust nahi kiya jata.
    /// </summary>
    public static class FileRules
    {
        public const long MaxBytes = 10L * 1024 * 1024;
        /// <summary>WebP / AVIF ki pehchaan byte 8–11 tak jati hai.</summary>
        public const int HeaderLength = 12;

        /// <summary>Profile photo max size (frontend file-rules.ts#checkPhoto).</summary>
        public const long PhotoMaxBytes = 2L * 1024 * 1024;

        /// <summary>Header mein Offset se shuru hone wale Bytes.</summary>
        private sealed record Signature(int Offset, byte[] Bytes);

        /// <summary>Saare Signatures match hone chahiye.</summary>
        private sealed record FileKind(string Type, string Mime, string[] Extensions, Signature[] Signatures);

        /// <summary>Documents: PDF / JPG / PNG.</summary>
        private static readonly FileKind[] Kinds =
        [
            new("pdf", "application/pdf", ["pdf"], [new(0, [0x25, 0x50, 0x44, 0x46])]),  // %PDF
            new("jpg", "image/jpeg", ["jpg", "jpeg"], [new(0, [0xFF, 0xD8, 0xFF])]),
            new("png", "image/png", ["png"], [new(0, [0x89, 0x50, 0x4E, 0x47])]),
        ];

        /// <summary>Profile photo: browser mein dikhne wale image formats.</summary>
        private static readonly FileKind[] PhotoKinds =
        [
            Kinds[1],
            Kinds[2],
            new("webp", "image/webp", ["webp"], [new(0, "RIFF"u8.ToArray()), new(8, "WEBP"u8.ToArray())]),
            new("avif", "image/avif", ["avif"], [new(4, "ftypavif"u8.ToArray())]),
        ];

        public const string PhotoTypesText = "JPG, PNG, WebP and AVIF";

        /// <summary>Extension + size. Error message ya null.</summary>
        public static string? CheckBasics(string fileName, long length)
        {
            if (FindByExtension(Kinds, fileName) is null) return "Only PDF, JPG and PNG files are allowed.";
            if (length <= 0) return "The file is empty.";
            if (length > MaxBytes) return "File is too large. Maximum size is 10 MB.";
            return null;
        }

        /// <summary>Photo ka size. Error message ya null. Type extension se nahi, DetectPhotoType se (content).</summary>
        public static string? CheckPhotoBasics(long length)
        {
            if (length <= 0) return "The file is empty.";
            if (length > PhotoMaxBytes) return "Photo is too large. Maximum size is 2 MB.";
            return null;
        }

        /// <summary>pdf / jpg / png jab header extension se match kare, warna null.</summary>
        public static string? DetectType(string fileName, ReadOnlySpan<byte> header) => Detect(Kinds, fileName, header);

        /// <summary>
        /// jpg / png / webp / avif — sirf content (header) se, extension se nahi: Windows browser se save hui JPEG
        /// aksar .jfif hoti hai. Stored type server ka hota hai, isliye extension par bharosa ki zarurat nahi.
        /// </summary>
        public static string? DetectPhotoType(ReadOnlySpan<byte> header)
        {
            foreach (var kind in PhotoKinds)
                if (Matches(kind, header)) return kind.Type;
            return null;
        }

        public static string MimeFor(string fileType) =>
            Kinds.Concat(PhotoKinds).FirstOrDefault(k => k.Type == fileType)?.Mime ?? "application/octet-stream";

        private static string? Detect(FileKind[] kinds, string fileName, ReadOnlySpan<byte> header)
        {
            var kind = FindByExtension(kinds, fileName);
            return kind is not null && Matches(kind, header) ? kind.Type : null;
        }

        private static bool Matches(FileKind kind, ReadOnlySpan<byte> header)
        {
            foreach (var sig in kind.Signatures)
            {
                var end = sig.Offset + sig.Bytes.Length;
                if (header.Length < end || !header[sig.Offset..end].SequenceEqual(sig.Bytes)) return false;
            }
            return true;
        }

        private static FileKind? FindByExtension(FileKind[] kinds, string fileName)
        {
            var ext = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
            return kinds.FirstOrDefault(k => k.Extensions.Contains(ext));
        }
    }
}
