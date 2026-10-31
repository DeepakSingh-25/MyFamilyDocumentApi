using System.Linq.Expressions;
using Documents.Core.Entities;
using Documents.Core.Interfaces;
using Documents.Core.Models;
using Documents.Core.Rules;
using Documents.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Documents.Infrastructure.Repositories
{
    public class DocumentRepository(AppDbContext db) : IDocumentRepository
    {
        private static readonly Expression<Func<DocumentEntity, FamilyDocumentDto>> ToDto = d => new FamilyDocumentDto(
            d.Id, d.Name, d.MemberId, d.Member.Name, d.CategoryId, d.Category.Name,
            d.DocumentDate, d.HospitalOrDoctor, d.Notes,
            d.FileName, d.FileType, d.FileSize, d.UploadedOn, d.UpdatedOn);

        public async Task<PagedResult<FamilyDocumentDto>> ListAsync(Guid userId, DocumentFilter filter, PageRequest page, CancellationToken ct)
        {
            var query = Filtered(userId, filter.MemberId, filter.CategoryId);

            var term = filter.Search?.Trim();
            if (!string.IsNullOrEmpty(term))
                query = query.Where(d => d.Name.Contains(term) || (d.HospitalOrDoctor != null && d.HospitalOrDoctor.Contains(term)));

            var totalCount = await query.CountAsync(ct);
            var result = new PagedResult<FamilyDocumentDto>([], page.Page, page.PageSize, totalCount);
            // Last page se aage: item query skip — offset kabhi totalCount se bada nahi, int overflow bhi nahi
            if (page.Page > result.TotalPages) return result;

            var items = await query
                .OrderByDescending(d => d.UploadedOn).ThenByDescending(d => d.Id)
                .Skip((page.Page - 1) * page.PageSize).Take(page.PageSize)
                .Select(ToDto)
                .ToListAsync(ct);
            return result with { Items = items };
        }

        public async Task<DocumentCountsDto> CountAsync(Guid userId, Guid? memberId, Guid? categoryId, CancellationToken ct)
        {
            var query = Filtered(userId, memberId, categoryId);

            var byMember = await query
                .GroupBy(d => d.MemberId)
                .OrderBy(g => g.Key)
                .Select(g => new MemberCountDto(g.Key, g.Count()))
                .ToListAsync(ct);
            var byCategory = await query
                .GroupBy(d => d.CategoryId)
                .OrderBy(g => g.Key)
                .Select(g => new CategoryIdCountDto(g.Key, g.Count()))
                .ToListAsync(ct);

            // Har document ka ek hi member hai, isliye total = byMember ka sum (ek query kam)
            return new DocumentCountsDto(byMember.Sum(x => x.Count), byMember, byCategory);
        }

        public Task<FamilyDocumentDto?> GetAsync(Guid userId, Guid id, CancellationToken ct) =>
            db.Documents.AsNoTracking()
                .Where(d => d.UserId == userId && d.Id == id)
                .Select(ToDto)
                .FirstOrDefaultAsync(ct);

        public Task<DocumentEntity?> GetForUpdateAsync(Guid userId, Guid id, CancellationToken ct) =>
            db.Documents.FirstOrDefaultAsync(d => d.UserId == userId && d.Id == id, ct);

        public async Task AddAsync(DocumentEntity document, CancellationToken ct)
        {
            db.Documents.Add(document);
            await db.SaveChangesAsync(ct);
        }

        public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

        public async Task<bool> SoftDeleteAsync(Guid userId, Guid id, DateTime deletedOnUtc, CancellationToken ct)
        {
            // Query filter (DeletedOn == null, member active) yahan bhi lagta hai — dobara delete = 0 rows = 404.
            // UpdatedOn nahi badalta, taaki restore document ko bilkul pehle jaisa laaye.
            var rows = await db.Documents
                .Where(d => d.UserId == userId && d.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.DeletedOn, deletedOnUtc), ct);
            return rows > 0;
        }

        public Task<DocumentFileInfo?> GetFileInfoAsync(Guid userId, Guid id, CancellationToken ct) =>
            db.Documents.AsNoTracking()
                .Where(d => d.UserId == userId && d.Id == id)
                .Select(d => new DocumentFileInfo(d.StorageKey, d.FileName, d.FileType))
                .FirstOrDefaultAsync(ct);

        public async Task<DashboardSummaryDto> GetSummaryAsync(Guid userId, DateTime monthStartUtc, CancellationToken ct)
        {
            var docs = db.Documents.AsNoTracking().Where(d => d.UserId == userId);

            var totalMembers = await db.FamilyMembers.CountAsync(m => m.UserId == userId, ct);
            var totalDocuments = await docs.CountAsync(ct);
            var medical = await docs.CountAsync(d => d.CategoryId == DomainRules.MedicalCategoryId, ct);
            var thisMonth = await docs.CountAsync(d => d.UploadedOn >= monthStartUtc, ct);
            var recent = await docs
                .OrderByDescending(d => d.UploadedOn).ThenByDescending(d => d.Id)
                .Take(6)
                .Select(ToDto)
                .ToListAsync(ct);

            var counts = await docs
                .GroupBy(d => d.CategoryId)
                .Select(g => new { CategoryId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CategoryId, x => x.Count, ct);
            // Sirf system + is user ki apni active categories (spec 013) — doosre user ki kabhi nahi
            var categories = await db.DocumentCategories.AsNoTracking()
                .Where(c => c.IsActive && (c.UserId == null || c.UserId == userId))
                .OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ThenBy(c => c.Id)
                .Select(c => new DocumentCategoryDto(c.Id, c.Name, c.Icon, c.Examples, c.UserId == null, c.SortOrder))
                .ToListAsync(ct);
            var byCategory = categories
                .Select(c => new CategoryCountDto(c, counts.GetValueOrDefault(c.Id)))
                .ToList();

            return new DashboardSummaryDto(totalMembers, totalDocuments, medical, thisMonth, recent, byCategory);
        }

        /// <summary>Owner + member/category filter (null ya Guid.Empty = filter nahi). Query filter deleted rows pehle hi hata deta hai.</summary>
        private IQueryable<DocumentEntity> Filtered(Guid userId, Guid? memberId, Guid? categoryId)
        {
            var query = db.Documents.AsNoTracking().Where(d => d.UserId == userId);
            if (memberId is { } member && member != Guid.Empty) query = query.Where(d => d.MemberId == member);
            if (categoryId is { } category && category != Guid.Empty) query = query.Where(d => d.CategoryId == category);
            return query;
        }
    }
}
