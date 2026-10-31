using Documents.Core.Entities;
using Documents.Core.Models;

namespace Documents.Core.Interfaces
{
    /// <summary>
    /// "Visible" = system (UserId null) + isi user ke apne active relationships (spec 013).
    /// Doosre user ka relationship kahin nahi dikhta — na list mein, na id se.
    /// </summary>
    public interface IRelationshipRepository
    {
        Task<IReadOnlyList<RelationshipDto>> ListAsync(Guid userId, CancellationToken ct);
        Task<bool> ExistsAsync(Guid userId, Guid id, CancellationToken ct);

        /// <summary>Tracked entity (update / delete ke liye). Null = nahi mila ya kisi aur ka.</summary>
        Task<RelationshipEntity?> GetVisibleAsync(Guid userId, Guid id, CancellationToken ct);

        /// <summary>Kya ye naam system ya is user ke kisi active relationship mein hai (case-insensitive collation).</summary>
        Task<bool> NameExistsAsync(Guid userId, string name, Guid? exceptId, CancellationToken ct);

        Task<int> CountCustomAsync(Guid userId, CancellationToken ct);

        /// <summary>
        /// Apna relationship ek hi statement mein deactivate karta hai, sirf jab koi active member use na kare.
        /// False = use mein hai (ya ab active nahi).
        /// </summary>
        Task<bool> DeactivateIfUnusedAsync(Guid userId, Guid id, CancellationToken ct);

        /// <summary>False jab naam pehle se hai (unique index).</summary>
        Task<bool> AddAsync(RelationshipEntity relationship, CancellationToken ct);

        /// <summary>Tracked changes save. False = naam clash (unique index).</summary>
        Task<bool> SaveAsync(CancellationToken ct);
    }
}
