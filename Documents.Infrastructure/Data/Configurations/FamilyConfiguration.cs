using Documents.Core.Entities;
using Documents.Core.Rules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Documents.Infrastructure.Data.Configurations
{
    public class FamilyMemberConfiguration : IEntityTypeConfiguration<FamilyMemberEntity>
    {
        public void Configure(EntityTypeBuilder<FamilyMemberEntity> builder)
        {
            builder.ToTable("FamilyMembers");
            builder.Property(m => m.Name).HasMaxLength(DomainRules.MemberNameMax).IsRequired();
            // Naam per user unique (default collation case-insensitive hai) — sirf active members mein,
            // taaki deleted "Asha" ke baad nayi "Asha" add ho sake (spec 007 §B.5)
            builder.HasIndex(m => new { m.UserId, m.Name }).IsUnique().HasFilter("\"DeletedOn\" IS NULL");
            // Soft delete: deleted members poore app se gayab; restore sirf SQL se. Filter bypass karne wala code kahin nahi.
            builder.HasQueryFilter(m => m.DeletedOn == null);
            // Restrict: SQL Server multiple cascade paths allow nahi karta (Users → Documents bhi hai)
            builder.HasOne<UserEntity>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
            // Relationship ab lookup row hai (spec 013); use mein ho to delete nahi hota
            builder.HasOne(m => m.Relationship).WithMany().HasForeignKey(m => m.RelationshipId).OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class RelationshipConfiguration : IEntityTypeConfiguration<RelationshipEntity>
    {
        public void Configure(EntityTypeBuilder<RelationshipEntity> builder)
        {
            builder.ToTable("Relationships");
            // Id = Guid: apne rows ke ids EF client par banata hai (sequential GUID, spec 014); system rows ke fixed GUIDs
            builder.Property(r => r.Name).HasMaxLength(DomainRules.RelationshipMax).IsRequired();
            // UserId null = system. SQL Server unique index mein NULLs barabar maane jaate hain, isliye system naam bhi unique.
            // Sirf active rows — delete ke baad wahi naam dobara ban sake.
            builder.HasIndex(r => new { r.UserId, r.Name }).IsUnique().HasFilter("\"IsActive\" = true");
            builder.HasOne<UserEntity>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);

            // Reference data — fixed GUIDs contract hain (Self = SystemRelationshipId(1)), order DomainRules.SystemRelationships wala
            builder.HasData(DomainRules.SystemRelationships.Select((name, i) => new RelationshipEntity
            {
                Id = DomainRules.SystemRelationshipId(i + 1),
                Name = name,
                SortOrder = i + 1 == DomainRules.SystemRelationships.Count ? DomainRules.OtherSortOrder : (i + 1) * 10,
                IsActive = true,
            }));
        }
    }

    public class DocumentCategoryConfiguration : IEntityTypeConfiguration<DocumentCategoryEntity>
    {
        public void Configure(EntityTypeBuilder<DocumentCategoryEntity> builder)
        {
            builder.ToTable("DocumentCategories");
            // System rows ke fixed GUIDs seed se; user ki categories ke ids EF client par banata hai (spec 014)
            builder.Property(c => c.Name).HasMaxLength(DomainRules.CategoryNameMax).IsRequired();
            builder.Property(c => c.Icon).HasMaxLength(40).IsRequired();
            builder.Property(c => c.Examples).HasMaxLength(DomainRules.CategoryExamplesMax).IsRequired();
            // UserId null = system; naam per user unique, sirf active rows (RelationshipConfiguration jaisa)
            builder.HasIndex(c => new { c.UserId, c.Name }).IsUnique().HasFilter("\"IsActive\" = true");
            builder.HasOne<UserEntity>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Restrict);

            // Reference data (demo data nahi) — fixed GUIDs frontend ke saath contract hain (spec 014), 3 = Medical.
            builder.HasData(
                new DocumentCategoryEntity { Id = DomainRules.SystemCategoryId(1), Name = "Identity Documents", Icon = "bi-person-vcard", Examples = "Aadhaar Card, PAN Card, Voter ID, Passport", SortOrder = 10, IsActive = true },
                new DocumentCategoryEntity { Id = DomainRules.SystemCategoryId(2), Name = "Bank & Financial", Icon = "bi-bank", Examples = "Bank Passbook, bank papers, FD receipts, loan documents", SortOrder = 20, IsActive = true },
                new DocumentCategoryEntity { Id = DomainRules.MedicalCategoryId, Name = "Medical Documents", Icon = "bi-heart-pulse", Examples = "Prescriptions, blood/lab reports, X-ray, MRI/CT, discharge summary, vaccination, health insurance, medical bills", SortOrder = 30, IsActive = true },
                new DocumentCategoryEntity { Id = DomainRules.SystemCategoryId(4), Name = "Property & Home", Icon = "bi-house-door", Examples = "Registry, property papers, rent agreement, electricity and water bills", SortOrder = 40, IsActive = true },
                new DocumentCategoryEntity { Id = DomainRules.SystemCategoryId(5), Name = "Vehicle Documents", Icon = "bi-car-front", Examples = "RC, Driving Licence, vehicle insurance, PUC", SortOrder = 50, IsActive = true },
                new DocumentCategoryEntity { Id = DomainRules.SystemCategoryId(6), Name = "Education Documents", Icon = "bi-mortarboard", Examples = "Marksheets, degrees, certificates", SortOrder = 60, IsActive = true },
                new DocumentCategoryEntity { Id = DomainRules.SystemCategoryId(7), Name = "Other Documents", Icon = "bi-folder2-open", Examples = "Any other important family document", SortOrder = DomainRules.OtherSortOrder, IsActive = true });
        }
    }

    public class DocumentConfiguration : IEntityTypeConfiguration<DocumentEntity>
    {
        public void Configure(EntityTypeBuilder<DocumentEntity> builder)
        {
            builder.ToTable("Documents");
            builder.Property(d => d.Name).HasMaxLength(DomainRules.DocumentNameMax).IsRequired();
            builder.Property(d => d.HospitalOrDoctor).HasMaxLength(DomainRules.HospitalOrDoctorMax);
            builder.Property(d => d.Notes).HasMaxLength(DomainRules.NotesMax);
            builder.Property(d => d.FileName).HasMaxLength(255).IsRequired();
            builder.Property(d => d.FileType).HasMaxLength(3).IsUnicode(false).IsRequired();
            builder.Property(d => d.StorageKey).HasMaxLength(260).IsRequired();

            builder.HasOne<UserEntity>().WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Restrict);
            // Cascade sirf DB level par bacha hai; app member ko soft delete karta hai (row kabhi remove nahi hoti)
            builder.HasOne(d => d.Member).WithMany(m => m.Documents).HasForeignKey(d => d.MemberId).OnDelete(DeleteBehavior.Cascade);
            // Soft delete (spec 009 §B.2): deleted document AUR soft-deleted member ke documents har jagah chhupe
            // (list, counts, search, file, dashboard, member.documentCount). EF Core 8 entity par sirf AAKHRI
            // HasQueryFilter rakhta hai — dono shartein isi ek expression mein rehni chahiye, doosra filter mat jodna.
            builder.HasQueryFilter(d => d.DeletedOn == null && d.Member.DeletedOn == null);
            builder.HasOne(d => d.Category).WithMany().HasForeignKey(d => d.CategoryId).OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(d => new { d.UserId, d.UploadedOn, d.Id }).IsDescending(false, true, true);
            builder.HasIndex(d => new { d.UserId, d.MemberId });
            builder.HasIndex(d => new { d.UserId, d.CategoryId });
        }
    }
}
