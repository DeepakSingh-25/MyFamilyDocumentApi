namespace Documents.Core.Models
{
    public sealed record DocumentFilter(string? Search, Guid? MemberId, Guid? CategoryId);

    /// <summary>Document ki details (file ke bina) — create aur update dono mein.</summary>
    public sealed record DocumentDetails(
        string Name,
        Guid MemberId,
        Guid CategoryId,
        DateOnly? DocumentDate,
        string? HospitalOrDoctor,
        string? Notes);

    /// <summary>Upload ki hui file — controller IFormFile se banata hai taaki Application ASP.NET par depend na kare.</summary>
    public sealed record UploadedFile(Stream Content, string FileName, long Length);

    public sealed record FamilyDocumentDto(
        Guid Id,
        string Name,
        Guid MemberId,
        string MemberName,
        Guid CategoryId,
        string CategoryName,
        DateOnly? DocumentDate,
        string? HospitalOrDoctor,
        string? Notes,
        string FileName,
        string FileType,
        long FileSize,
        DateTime UploadedOn,
        DateTime? UpdatedOn);

    /// <summary>Download / preview ke liye stream. Caller dispose karta hai.</summary>
    public sealed record DocumentFileDto(Stream Content, string ContentType, string FileName);

    /// <summary>Repository se file ki jagah — StorageKey sirf server par rehta hai.</summary>
    public sealed record DocumentFileInfo(string StorageKey, string FileName, string FileType);

    /// <summary>IsSystem = built-in (rename / delete nahi ho sakta, spec 013). SortOrder: list order (web naya item sahi jagah rakhta hai).</summary>
    public sealed record DocumentCategoryDto(Guid Id, string Name, string Icon, string Examples, bool IsSystem, int SortOrder);

    public sealed record CategoryCountDto(DocumentCategoryDto Category, int Count);

    /// <summary>Medical tabs aur member chips ke counts (spec 009 §B.3) — sirf count &gt; 0 wale groups.</summary>
    public sealed record DocumentCountsDto(int Total, IReadOnlyList<MemberCountDto> ByMember, IReadOnlyList<CategoryIdCountDto> ByCategory);

    public sealed record MemberCountDto(Guid MemberId, int Count);

    public sealed record CategoryIdCountDto(Guid CategoryId, int Count);

    public sealed record DashboardSummaryDto(
        int TotalMembers,
        int TotalDocuments,
        int MedicalDocuments,
        int AddedThisMonth,
        IReadOnlyList<FamilyDocumentDto> Recent,
        IReadOnlyList<CategoryCountDto> ByCategory);
}
