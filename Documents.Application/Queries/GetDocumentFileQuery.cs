using Documents.Core.Interfaces;
using Documents.Core.Models;
using Documents.Core.Rules;
using MediatR;

namespace Documents.Application.Queries
{
    public record GetDocumentFileQuery(Guid UserId, Guid Id) : IRequest<Result<DocumentFileDto>>;

    public class GetDocumentFileQueryHandler(IDocumentRepository documents, IFileStorage storage)
        : IRequestHandler<GetDocumentFileQuery, Result<DocumentFileDto>>
    {
        public async Task<Result<DocumentFileDto>> Handle(GetDocumentFileQuery request, CancellationToken ct)
        {
            var info = await documents.GetFileInfoAsync(request.UserId, request.Id, ct);
            if (info is null) return Result.Fail<DocumentFileDto>(ErrorKind.NotFound, "Document not found.");

            var stream = storage.OpenRead(info.StorageKey);
            if (stream is null) return Result.Fail<DocumentFileDto>(ErrorKind.NotFound, "The file of this document is missing.");

            return Result.Success(new DocumentFileDto(stream, FileRules.MimeFor(info.FileType), info.FileName));
        }
    }
}
