using Documents.Core.Interfaces;
using Documents.Core.Models;
using MediatR;

namespace Documents.Application.Queries
{
    public record GetDocumentQuery(Guid UserId, Guid Id) : IRequest<Result<FamilyDocumentDto>>;

    public class GetDocumentQueryHandler(IDocumentRepository documents) : IRequestHandler<GetDocumentQuery, Result<FamilyDocumentDto>>
    {
        public async Task<Result<FamilyDocumentDto>> Handle(GetDocumentQuery request, CancellationToken ct)
        {
            var doc = await documents.GetAsync(request.UserId, request.Id, ct);
            return doc is null ? Result.Fail<FamilyDocumentDto>(ErrorKind.NotFound, "Document not found.") : Result.Success(doc);
        }
    }
}
