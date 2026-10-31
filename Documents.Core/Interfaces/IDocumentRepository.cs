using Documents.Core.Entities;
using Documents.Core.Models;

namespace Documents.Core.Interfaces
{
    /// <summary>Har method userId leta hai aur usi se filter karta hai.</summary>
    public interface IDocumentRepository
    {
        /// <summary>Ek page, newest first (UploadedOn desc, Id desc). Page last page se aage ho to items khaali.</summary>
        Task<PagedResult<FamilyDocumentDto>> ListAsync(Guid userId, DocumentFilter filter, PageRequest page, CancellationToken ct);

        /// <summary>Member / category ke hisaab se counts (search nahi) — sirf count &gt; 0 wale groups.</summary>
        Task<DocumentCountsDto> CountAsync(Guid userId, Guid? memberId, Guid? categoryId, CancellationToken ct);

        Task<FamilyDocumentDto?> GetAsync(Guid userId, Guid id, CancellationToken ct);

        /// <summary>Tracked entity — handler badalkar <see cref="SaveChangesAsync"/> bulata hai.</summary>
        Task<DocumentEntity?> GetForUpdateAsync(Guid userId, Guid id, CancellationToken ct);

        Task AddAsync(DocumentEntity document, CancellationToken ct);
        Task SaveChangesAsync(CancellationToken ct);

        /// <summary>Soft delete: DeletedOn set, file nahi hatti. False jab nahi mila (ya pehle se deleted).</summary>
        Task<bool> SoftDeleteAsync(Guid userId, Guid id, DateTime deletedOnUtc, CancellationToken ct);

        Task<DocumentFileInfo?> GetFileInfoAsync(Guid userId, Guid id, CancellationToken ct);

        Task<DashboardSummaryDto> GetSummaryAsync(Guid userId, DateTime monthStartUtc, CancellationToken ct);
    }
}
