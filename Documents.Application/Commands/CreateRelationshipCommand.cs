using Documents.Application.Common;
using Documents.Core.Entities;
using Documents.Core.Interfaces;
using Documents.Core.Models;
using Documents.Core.Rules;
using MediatR;

namespace Documents.Application.Commands
{
    /// <summary>User ka apna relationship (spec 013). Naam system ya apne kisi relationship se clash na kare.</summary>
    public record CreateRelationshipCommand(Guid UserId, RelationshipRequest Request) : IRequest<Result<RelationshipDto>>;

    public class CreateRelationshipCommandHandler(IRelationshipRepository relationships) : IRequestHandler<CreateRelationshipCommand, Result<RelationshipDto>>
    {
        internal const string DuplicateMessage = "A relationship with this name already exists.";

        public async Task<Result<RelationshipDto>> Handle(CreateRelationshipCommand command, CancellationToken ct)
        {
            var problem = LookupValidation.CheckRelationship(command.Request);
            if (problem is not null) return Result.Fail<RelationshipDto>(ErrorKind.Validation, problem);

            var name = command.Request.Name.Trim();
            if (await relationships.NameExistsAsync(command.UserId, name, null, ct))
                return Result.Fail<RelationshipDto>(ErrorKind.Conflict, DuplicateMessage);
            if (await relationships.CountCustomAsync(command.UserId, ct) >= DomainRules.CustomLookupMax)
                return Result.Fail<RelationshipDto>(ErrorKind.Validation, LookupValidation.TooManyMessage);

            var entity = new RelationshipEntity
            {
                UserId = command.UserId,
                Name = name,
                SortOrder = DomainRules.CustomSortOrder,
                IsActive = true,
            };
            if (!await relationships.AddAsync(entity, ct))
                return Result.Fail<RelationshipDto>(ErrorKind.Conflict, DuplicateMessage);

            return Result.Success(new RelationshipDto(entity.Id, entity.Name, IsSystem: false, entity.SortOrder));
        }
    }
}
