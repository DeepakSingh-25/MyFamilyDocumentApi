using Documents.Core.Interfaces;
using Documents.Core.Models;
using MediatR;

namespace Documents.Application.Commands
{
    /// <summary>
    /// Soft delete (spec 009 §B.2): document app se gayab, lekin row aur file rehti hain.
    /// Restore sirf SQL se — docs/migrations/005-AddDocumentSoftDelete.sql.
    /// </summary>
    public record DeleteDocumentCommand(Guid UserId, Guid Id) : IRequest<Result>;

    public class DeleteDocumentCommandHandler(IDocumentRepository documents, TimeProvider clock)
        : IRequestHandler<DeleteDocumentCommand, Result>
    {
        public async Task<Result> Handle(DeleteDocumentCommand command, CancellationToken ct) =>
            await documents.SoftDeleteAsync(command.UserId, command.Id, clock.GetUtcNow().UtcDateTime, ct)
                ? Result.Success()
                : Result.Fail(ErrorKind.NotFound, "Document not found.");
    }
}
