using Documents.Application.Common;
using Documents.Core.Interfaces;
using Documents.Core.Models;
using MediatR;

namespace Documents.Application.Commands
{
    /// <summary>Apne relationship ka naam badalna. System → 403, kisi aur ka ya missing → 404 (spec 013).</summary>
    public record UpdateRelationshipCommand(Guid UserId, Guid Id, RelationshipRequest Request) : IRequest<Result<RelationshipDto>>;

    public class UpdateRelationshipCommandHandler(IRelationshipRepository relationships) : IRequestHandler<UpdateRelationshipCommand, Result<RelationshipDto>>
    {
        internal const string NotFoundMessage = "Relationship not found.";

        public async Task<Result<RelationshipDto>> Handle(UpdateRelationshipCommand command, CancellationToken ct)
        {
            var problem = LookupValidation.CheckRelationship(command.Request);
            if (problem is not null) return Result.Fail<RelationshipDto>(ErrorKind.Validation, problem);

            var relationship = await relationships.GetVisibleAsync(command.UserId, command.Id, ct);
            if (relationship is null) return Result.Fail<RelationshipDto>(ErrorKind.NotFound, NotFoundMessage);
            if (relationship.UserId is null) return Result.Fail<RelationshipDto>(ErrorKind.Forbidden, LookupValidation.SystemMessage);

            var name = command.Request.Name.Trim();
            if (await relationships.NameExistsAsync(command.UserId, name, command.Id, ct))
                return Result.Fail<RelationshipDto>(ErrorKind.Conflict, CreateRelationshipCommandHandler.DuplicateMessage);

            relationship.Name = name;
            if (!await relationships.SaveAsync(ct))
                return Result.Fail<RelationshipDto>(ErrorKind.Conflict, CreateRelationshipCommandHandler.DuplicateMessage);

            return Result.Success(new RelationshipDto(relationship.Id, relationship.Name, IsSystem: false, relationship.SortOrder));
        }
    }
}
