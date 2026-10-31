using Documents.Core.Models;

namespace Documents.Api.Tests
{
    /// <summary>
    /// GET /api/documents ka JSON envelope jaisa wire par aata hai. Core ka PagedResult nahi use kiya kyunki
    /// uska TotalPages computed hai — yahan check karna hai ki totalPages sach mein serialise hota hai.
    /// </summary>
    internal sealed record DocumentPage(List<FamilyDocumentDto> Items, int Page, int PageSize, int TotalCount, int TotalPages);
}
