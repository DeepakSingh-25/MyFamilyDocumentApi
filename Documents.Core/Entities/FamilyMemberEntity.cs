namespace Documents.Core.Entities
{
    public class FamilyMemberEntity
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Name { get; set; } = null!;
        /// <summary>FK → Relationships (system ya isi user ka apna, spec 013).</summary>
        public Guid RelationshipId { get; set; }
        public DateOnly? DateOfBirth { get; set; }
        public DateTime CreatedOn { get; set; }
        /// <summary>Soft delete (UTC); null = active. Restore sirf SQL se (spec 007 §B.5).</summary>
        public DateTime? DeletedOn { get; set; }
        public RelationshipEntity Relationship { get; set; } = null!;
        public ICollection<DocumentEntity> Documents { get; set; } = [];
    }
}
