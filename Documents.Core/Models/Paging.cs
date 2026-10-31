namespace Documents.Core.Models
{
    /// <summary>Page number (1 se) aur page size — handler validate karta hai (spec 009 §B.1).</summary>
    public sealed record PageRequest(int Page, int PageSize)
    {
        public const int DefaultSize = 10;
        public const int MaxSize = 50;
    }

    /// <summary>Paged list ka envelope: items + page info. TotalPages bhi JSON mein jata hai.</summary>
    public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
    {
        /// <summary>ceil(TotalCount / PageSize); koi row nahi to 0.</summary>
        public int TotalPages => PageSize <= 0 ? 0 : (int)((TotalCount + (long)PageSize - 1) / PageSize);
    }
}
