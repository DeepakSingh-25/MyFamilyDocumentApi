using Documents.Core.Entities;
using Documents.Core.Models;

namespace Documents.Core.Interfaces
{
    /// <summary>Har method userId leta hai aur usi se filter karta hai. Soft-deleted members kahin nahi dikhte.</summary>
    public interface IFamilyMemberRepository
    {
        Task<IReadOnlyList<FamilyMemberDto>> ListAsync(Guid userId, CancellationToken ct);
        Task<FamilyMemberDto?> GetAsync(Guid userId, Guid id, CancellationToken ct);
        Task<bool> ExistsAsync(Guid userId, Guid id, CancellationToken ct);
        Task<bool> NameExistsAsync(Guid userId, string name, Guid? exceptId, CancellationToken ct);

        /// <summary>Kya is user ka koi doosra active "Self" (DomainRules.SelfRelationshipId) member hai.</summary>
        Task<bool> SelfExistsAsync(Guid userId, Guid? exceptId, CancellationToken ct);

        /// <summary>False jab naam pehle se hai (unique index).</summary>
        Task<bool> AddAsync(FamilyMemberEntity member, CancellationToken ct);

        /// <summary>Null = member nahi mila; false = naam clash; true = saved.</summary>
        Task<bool?> UpdateAsync(Guid userId, Guid id, string name, Guid relationshipId, DateOnly? dateOfBirth, CancellationToken ct);

        /// <summary>Soft delete: sirf DeletedOn set hota hai, documents aur files rehte hain. False jab active member nahi mila.</summary>
        Task<bool> SoftDeleteAsync(Guid userId, Guid id, DateTime deletedOnUtc, CancellationToken ct);
    }
}
