using Documents.Core.Interfaces;
using Documents.Core.Models;
using Documents.Core.Rules;
using MediatR;

namespace Documents.Application.Queries
{
    /// <summary>Documents ka ek page (spec 009 §B.1) — hamesha envelope, kabhi bare array nahi.</summary>
    public record GetDocumentsQuery(Guid UserId, DocumentFilter Filter, PageRequest Page) : IRequest<Result<PagedResult<FamilyDocumentDto>>>;

    public class GetDocumentsQueryHandler(IDocumentRepository documents)
        : IRequestHandler<GetDocumentsQuery, Result<PagedResult<FamilyDocumentDto>>>
    {
        public async Task<Result<PagedResult<FamilyDocumentDto>>> Handle(GetDocumentsQuery request, CancellationToken ct)
        {
            var error = Validate(request);
            if (error is not null) return Result.Fail<PagedResult<FamilyDocumentDto>>(ErrorKind.Validation, error);

            var filter = request.Filter with { Search = request.Filter.Search?.Trim() };
            return Result.Success(await documents.ListAsync(request.UserId, filter, request.Page, ct));
        }

        private static string? Validate(GetDocumentsQuery request)
        {
            if (request.Filter.Search?.Trim().Length > DomainRules.SearchMax) return "Search text is too long.";
            if (request.Page.Page < 1) return "Page must be 1 or more.";
            if (request.Page.PageSize is < 1 or > PageRequest.MaxSize)
                return $"Page size must be between 1 and {PageRequest.MaxSize}.";
            return null;
        }
    }
}
