namespace Documents.Core.Entities
{
    /// <summary>
    /// System categories (UserId null, 7 rows) ke fixed GUIDs ek contract hain (DomainRules.SystemCategoryId, Medical = 3) — frontend bhi yahi ids use karta hai (spec 014).
    /// User apni categories bhi bana sakta hai (spec 013); delete = IsActive false.
    /// </summary>
    public class DocumentCategoryEntity
    {
        public Guid Id { get; set; }
        /// <summary>Null = system (built-in); warna owner user.</summary>
        public Guid? UserId { get; set; }
        public string Name { get; set; } = null!;
        public string Icon { get; set; } = null!;
        /// <summary>Dropdown ke neeche dikhne wala hint.</summary>
        public string Examples { get; set; } = null!;
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
