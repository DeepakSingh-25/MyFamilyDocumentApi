using Documents.Core.Interfaces;
using Documents.Core.Models;
using MediatR;

namespace Documents.Application.Queries
{
    /// <summary>Medical tabs (byMember) aur member chips (byCategory) ke counts — search ke bina (spec 009 §B.3).</summary>
    public record GetDocumentCountsQuery(Guid UserId, Guid? MemberId, Guid? CategoryId) : IRequest<DocumentCountsDto>;

    public class GetDocumentCountsQueryHandler(IDocumentRepository documents) : IRequestHandler<GetDocumentCountsQuery, DocumentCountsDto>
    {
        public Task<DocumentCountsDto> Handle(GetDocumentCountsQuery request, CancellationToken ct) =>
            documents.CountAsync(request.UserId, request.MemberId, request.CategoryId, ct);
    }
}
