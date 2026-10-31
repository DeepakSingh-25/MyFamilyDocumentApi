using Documents.Core.Entities;
using Documents.Core.Models;

namespace Documents.Core.Interfaces
{
    /// <summary>
    /// "Visible" = system (UserId null) + isi user ki apni active categories (spec 013).
    /// Doosre user ki category kahin nahi dikhti — na list mein, na id se.
    /// </summary>
    public interface ICategoryRepository
    {
        Task<IReadOnlyList<DocumentCategoryDto>> ListAsync(Guid userId, CancellationToken ct);
        Task<bool> ExistsAsync(Guid userId, Guid id, CancellationToken ct);

        /// <summary>Tracked entity (update / delete ke liye). Null = nahi mili ya kisi aur ki.</summary>
        Task<DocumentCategoryEntity?> GetVisibleAsync(Guid userId, Guid id, CancellationToken ct);

        /// <summary>Kya ye naam system ya is user ki kisi active category mein hai (case-insensitive collation).</summary>
        Task<bool> NameExistsAsync(Guid userId, string name, Guid? exceptId, CancellationToken ct);

        Task<int> CountCustomAsync(Guid userId, CancellationToken ct);

        /// <summary>
        /// Apni category ko ek hi statement mein deactivate karta hai, sirf jab koi active document use na kare
        /// (check aur update ke beech koi upload na ghuse). False = use mein hai (ya ab active nahi).
        /// </summary>
        Task<bool> DeactivateIfUnusedAsync(Guid userId, Guid id, CancellationToken ct);

        /// <summary>False jab naam pehle se hai (unique index).</summary>
        Task<bool> AddAsync(DocumentCategoryEntity category, CancellationToken ct);

        /// <summary>Tracked changes save. False = naam clash (unique index).</summary>
        Task<bool> SaveAsync(CancellationToken ct);
    }
}
