using Documents.Core.Interfaces;
using Documents.Core.Models;
using Documents.Core.Rules;

namespace Documents.Application.Common
{
    /// <summary>frontend document.service.ts#validate ka server version — same messages.</summary>
    internal sealed class DocumentValidation(IFamilyMemberRepository members, ICategoryRepository categories)
    {
        /// <summary>Details check. Error message ya null.</summary>
        public async Task<string?> CheckDetailsAsync(Guid userId, DocumentDetails d, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(d.Name)) return "Document name is required.";
            if (d.Name.Trim().Length > DomainRules.DocumentNameMax) return "Document name is too long.";
            if ((d.HospitalOrDoctor?.Trim().Length ?? 0) > DomainRules.HospitalOrDoctorMax) return "Hospital / doctor is too long.";
            if ((d.Notes?.Trim().Length ?? 0) > DomainRules.NotesMax) return "Notes are too long.";
            if (d.MemberId == Guid.Empty || !await members.ExistsAsync(userId, d.MemberId, ct)) return "Please choose a family member.";
            if (d.CategoryId == Guid.Empty || !await categories.ExistsAsync(userId, d.CategoryId, ct)) return "Please choose a category.";
            return null;
        }

        /// <summary>Extension, size aur asli header. (detected type, error) — stream wapas shuru par.</summary>
        public static async Task<(string? FileType, string? Error)> CheckFileAsync(UploadedFile file, CancellationToken ct)
        {
            var basic = FileRules.CheckBasics(file.FileName, file.Length);
            if (basic is not null) return (null, basic);
            if (!file.Content.CanSeek) return (null, "The file could not be read. Please try again.");

            var header = new byte[FileRules.HeaderLength];
            var read = await file.Content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
            file.Content.Position = 0;

            var type = FileRules.DetectType(file.FileName, header.AsSpan(0, read));
            return type is null
                ? (null, "The file content does not match its type. Please upload a real PDF, JPG or PNG.")
                : (type, null);
        }

        /// <summary>Hospital/doctor sirf Medical category mein rakhte hain; khaali string = null.</summary>
        public static (string Name, string? HospitalOrDoctor, string? Notes) Clean(DocumentDetails d) =>
            (d.Name.Trim(),
             d.CategoryId == DomainRules.MedicalCategoryId ? NullIfEmpty(d.HospitalOrDoctor) : null,
             NullIfEmpty(d.Notes));

        /// <summary>Display ke liye sirf file ka naam (koi path nahi), max 255.</summary>
        public static string SafeFileName(string fileName)
        {
            var name = Path.GetFileName(fileName.Replace('\\', '/').Split('/').Last()).Trim();
            return name.Length <= 255 ? name : name[^255..];
        }

        private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
