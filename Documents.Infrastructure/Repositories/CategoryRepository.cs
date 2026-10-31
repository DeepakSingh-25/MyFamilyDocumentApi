using Documents.Core.Entities;
using Documents.Core.Interfaces;
using Documents.Core.Models;
using Documents.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Documents.Infrastructure.Repositories
{
    public class CategoryRepository(AppDbContext db) : ICategoryRepository
    {
        public async Task<IReadOnlyList<DocumentCategoryDto>> ListAsync(Guid userId, CancellationToken ct) =>
            await Visible(userId).AsNoTracking()
                .OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ThenBy(c => c.Id)
                .Select(c => new DocumentCategoryDto(c.Id, c.Name, c.Icon, c.Examples, c.UserId == null, c.SortOrder))
                .ToListAsync(ct);

        public Task<bool> ExistsAsync(Guid userId, Guid id, CancellationToken ct) =>
            Visible(userId).AnyAsync(c => c.Id == id, ct);

        public Task<DocumentCategoryEntity?> GetVisibleAsync(Guid userId, Guid id, CancellationToken ct) =>
            Visible(userId).FirstOrDefaultAsync(c => c.Id == id, ct);

        public Task<bool> NameExistsAsync(Guid userId, string name, Guid? exceptId, CancellationToken ct) =>
            Visible(userId).AnyAsync(c => c.Name == name && c.Id != exceptId, ct);

        public Task<int> CountCustomAsync(Guid userId, CancellationToken ct) =>
            db.DocumentCategories.CountAsync(c => c.UserId == userId && c.IsActive, ct);

        // Subquery par bhi query filter lagta hai: deleted documents (aur deleted members ke) nahi gine jaate
        public async Task<bool> DeactivateIfUnusedAsync(Guid userId, Guid id, CancellationToken ct) =>
            await db.DocumentCategories
                .Where(c => c.Id == id && c.UserId == userId && c.IsActive && !db.Documents.Any(d => d.CategoryId == id))
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsActive, false), ct) > 0;

        public async Task<bool> AddAsync(DocumentCategoryEntity category, CancellationToken ct)
        {
            db.DocumentCategories.Add(category);
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

        /// <summary>System + is user ki apni active categories.</summary>
        private IQueryable<DocumentCategoryEntity> Visible(Guid userId) =>
            db.DocumentCategories.Where(c => c.IsActive && (c.UserId == null || c.UserId == userId));
    }
}
