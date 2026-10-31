namespace Documents.Core.Entities
{
    /// <summary>Member ka rishta. System rows (UserId null, 13 fixed GUIDs — DomainRules.SystemRelationshipId, 1 = Self) + user ke apne (spec 013).</summary>
    public class RelationshipEntity
    {
        public Guid Id { get; set; }
        /// <summary>Null = system (built-in); warna owner user.</summary>
        public Guid? UserId { get; set; }
        public string Name { get; set; } = null!;
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
