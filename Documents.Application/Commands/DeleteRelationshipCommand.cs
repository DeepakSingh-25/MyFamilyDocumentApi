using Documents.Application.Common;
using Documents.Core.Interfaces;
using Documents.Core.Models;
using MediatR;

namespace Documents.Application.Commands
{
    /// <summary>
    /// Apna relationship hatana = deactivate (spec 013). Active members use karte hon to 409 — pehle unka relationship badlo.
    /// Soft-deleted members ki FK rehti hai, isliye row kabhi delete nahi hoti.
    /// </summary>
    public record DeleteRelationshipCommand(Guid UserId, Guid Id) : IRequest<Result>;

    public class DeleteRelationshipCommandHandler(IRelationshipRepository relationships) : IRequestHandler<DeleteRelationshipCommand, Result>
    {
        public async Task<Result> Handle(DeleteRelationshipCommand command, CancellationToken ct)
        {
            var relationship = await relationships.GetVisibleAsync(command.UserId, command.Id, ct);
            if (relationship is null) return Result.Fail(ErrorKind.NotFound, UpdateRelationshipCommandHandler.NotFoundMessage);
            if (relationship.UserId is null) return Result.Fail(ErrorKind.Forbidden, LookupValidation.SystemMessage);
            // 404 / 403 upar ho chuke; yahan false = use mein hai
            return await relationships.DeactivateIfUnusedAsync(command.UserId, command.Id, ct)
                ? Result.Success()
                : Result.Fail(ErrorKind.Conflict, "This relationship is used by a family member. Change their relationship first.");
        }
    }
}
