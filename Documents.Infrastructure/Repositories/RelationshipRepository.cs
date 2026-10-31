using Documents.Core.Entities;
using Documents.Core.Interfaces;
using Documents.Core.Models;
using Documents.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Documents.Infrastructure.Repositories
{
    public class RelationshipRepository(AppDbContext db) : IRelationshipRepository
    {
        public async Task<IReadOnlyList<RelationshipDto>> ListAsync(Guid userId, CancellationToken ct) =>
            await Visible(userId).AsNoTracking()
                .OrderBy(r => r.SortOrder).ThenBy(r => r.Name).ThenBy(r => r.Id)
                .Select(r => new RelationshipDto(r.Id, r.Name, r.UserId == null, r.SortOrder))
                .ToListAsync(ct);

        public Task<bool> ExistsAsync(Guid userId, Guid id, CancellationToken ct) =>
            Visible(userId).AnyAsync(r => r.Id == id, ct);

        public Task<RelationshipEntity?> GetVisibleAsync(Guid userId, Guid id, CancellationToken ct) =>
            Visible(userId).FirstOrDefaultAsync(r => r.Id == id, ct);

        public Task<bool> NameExistsAsync(Guid userId, string name, Guid? exceptId, CancellationToken ct) =>
            Visible(userId).AnyAsync(r => r.Name == name && r.Id != exceptId, ct);

        public Task<int> CountCustomAsync(Guid userId, CancellationToken ct) =>
            db.Relationships.CountAsync(r => r.UserId == userId && r.IsActive, ct);

        // Subquery par query filter: soft-deleted members nahi gine jaate (unki FK rehti hai, isliye row deactivate hoti hai, delete nahi)
        public async Task<bool> DeactivateIfUnusedAsync(Guid userId, Guid id, CancellationToken ct) =>
            await db.Relationships
                .Where(r => r.Id == id && r.UserId == userId && r.IsActive && !db.FamilyMembers.Any(m => m.RelationshipId == id))
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct) > 0;

        public async Task<bool> AddAsync(RelationshipEntity relationship, CancellationToken ct)
        {
            db.Relationships.Add(relationship);
            return await SaveAsync(ct);
        }

        public async Task<bool> SaveAsync(CancellationToken ct)
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

        /// <summary>System + is user ke apne active relationships.</summary>
        private IQueryable<RelationshipEntity> Visible(Guid userId) =>
            db.Relationships.Where(r => r.IsActive && (r.UserId == null || r.UserId == userId));
    }
}
