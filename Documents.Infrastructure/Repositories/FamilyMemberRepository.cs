using Documents.Core.Entities;
using Documents.Core.Interfaces;
using Documents.Core.Models;
using Documents.Core.Rules;
using Documents.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Documents.Infrastructure.Repositories
{
    public class FamilyMemberRepository(AppDbContext db) : IFamilyMemberRepository
    {
        public async Task<IReadOnlyList<FamilyMemberDto>> ListAsync(Guid userId, CancellationToken ct) =>
            await db.FamilyMembers.AsNoTracking()
                .Where(m => m.UserId == userId)
                .OrderBy(m => m.CreatedOn).ThenBy(m => m.Id)
                .Select(m => new FamilyMemberDto(m.Id, m.Name, m.RelationshipId, m.Relationship.Name, m.DateOfBirth, m.Documents.Count))
                .ToListAsync(ct);

        public Task<FamilyMemberDto?> GetAsync(Guid userId, Guid id, CancellationToken ct) =>
            db.FamilyMembers.AsNoTracking()
                .Where(m => m.UserId == userId && m.Id == id)
                .Select(m => new FamilyMemberDto(m.Id, m.Name, m.RelationshipId, m.Relationship.Name, m.DateOfBirth, m.Documents.Count))
                .FirstOrDefaultAsync(ct);

        public Task<bool> ExistsAsync(Guid userId, Guid id, CancellationToken ct) =>
            db.FamilyMembers.AnyAsync(m => m.UserId == userId && m.Id == id, ct);

        public Task<bool> NameExistsAsync(Guid userId, string name, Guid? exceptId, CancellationToken ct) =>
            db.FamilyMembers.AnyAsync(m => m.UserId == userId && m.Name == name && m.Id != exceptId, ct);

        public Task<bool> SelfExistsAsync(Guid userId, Guid? exceptId, CancellationToken ct) =>
            db.FamilyMembers.AnyAsync(m => m.UserId == userId && m.RelationshipId == DomainRules.SelfRelationshipId && m.Id != exceptId, ct);

        public async Task<bool> AddAsync(FamilyMemberEntity member, CancellationToken ct)
        {
            db.FamilyMembers.Add(member);
            return await TrySaveAsync(ct);
        }

        public async Task<bool?> UpdateAsync(Guid userId, Guid id, string name, Guid relationshipId, DateOnly? dateOfBirth, CancellationToken ct)
        {
            var member = await db.FamilyMembers.FirstOrDefaultAsync(m => m.UserId == userId && m.Id == id, ct);
            if (member is null) return null;
            member.Name = name;
            member.RelationshipId = relationshipId;
            member.DateOfBirth = dateOfBirth;
            return await TrySaveAsync(ct);
        }

        public async Task<bool> SoftDeleteAsync(Guid userId, Guid id, DateTime deletedOnUtc, CancellationToken ct)
        {
            // Query filter (DeletedOn == null) yahan bhi lagta hai — dobara delete = 0 rows = 404.
            // Documents ko touch nahi karte: unka filter member ke DeletedOn se chalta hai.
            var rows = await db.FamilyMembers
                .Where(m => m.UserId == userId && m.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.DeletedOn, deletedOnUtc), ct);
            return rows > 0;
        }

        private async Task<bool> TrySaveAsync(CancellationToken ct)
        {
            try
            {
                await db.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
            {
                db.ChangeTracker.Clear();
                return false;
            }
        }
    }
}
