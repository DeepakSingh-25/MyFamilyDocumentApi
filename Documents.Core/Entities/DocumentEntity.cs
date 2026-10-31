namespace Documents.Core.Entities
{
    public class DocumentEntity
    {
        public Guid Id { get; set; }
        /// <summary>Ownership filter ke liye denormalised; hamesha Member.UserId ke barabar.</summary>
        public Guid UserId { get; set; }
        public Guid MemberId { get; set; }
        public Guid CategoryId { get; set; }
        public string Name { get; set; } = null!;
        public DateOnly? DocumentDate { get; set; }
        public string? HospitalOrDoctor { get; set; }
        public string? Notes { get; set; }
        /// <summary>Original file name — sirf display / download ke liye.</summary>
        public string FileName { get; set; } = null!;
        /// <summary>pdf / jpg / png — content se detect kiya hua.</summary>
        public string FileType { get; set; } = null!;
        public long FileSize { get; set; }
        /// <summary>Server-generated storage path. Kabhi client ko nahi bheja jata.</summary>
        public string StorageKey { get; set; } = null!;
        public DateTime UploadedOn { get; set; }
        public DateTime? UpdatedOn { get; set; }
        /// <summary>Soft delete (UTC); null = active. File storage mein rehti hai; restore sirf SQL se (spec 009 §B.2).</summary>
        public DateTime? DeletedOn { get; set; }

        public FamilyMemberEntity Member { get; set; } = null!;
        public DocumentCategoryEntity Category { get; set; } = null!;
    }
}
