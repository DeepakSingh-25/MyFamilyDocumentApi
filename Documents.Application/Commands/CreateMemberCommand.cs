using Documents.Application.Common;
using Documents.Core.Entities;
using Documents.Core.Interfaces;
using Documents.Core.Models;
using Documents.Core.Options;
using Documents.Core.Rules;
using MediatR;
using Microsoft.Extensions.Options;

namespace Documents.Application.Commands
{
    public record CreateMemberCommand(Guid UserId, MemberRequest Request) : IRequest<Result<FamilyMemberDto>>;

    public class CreateMemberCommandHandler(
        IFamilyMemberRepository members,
        IRelationshipRepository relationships,
        IOptions<AppOptions> app,
        TimeProvider clock)
        : IRequestHandler<CreateMemberCommand, Result<FamilyMemberDto>>
    {
        private const string DuplicateMessage = "A family member with this name already exists.";

        public async Task<Result<FamilyMemberDto>> Handle(CreateMemberCommand command, CancellationToken ct)
        {
            var req = command.Request;
            var problem = MemberValidation.Check(req, LocalClock.Today(clock, app.Value));
            if (problem is not null) return Result.Fail<FamilyMemberDto>(ErrorKind.Validation, problem);
            var relationshipId = req.RelationshipId!.Value;
            // Sirf system ya apna active relationship — kisi aur ka id "nahi mila" jaisa hi
            if (!await relationships.ExistsAsync(command.UserId, relationshipId, ct))
                return Result.Fail<FamilyMemberDto>(ErrorKind.Validation, MemberValidation.RelationshipMessage);

            var name = req.Name.Trim();
            if (await members.NameExistsAsync(command.UserId, name, null, ct))
                return Result.Fail<FamilyMemberDto>(ErrorKind.Conflict, DuplicateMessage);
            if (relationshipId == DomainRules.SelfRelationshipId && await members.SelfExistsAsync(command.UserId, null, ct))
                return Result.Fail<FamilyMemberDto>(ErrorKind.Conflict, MemberValidation.SelfTakenMessage);

            var entity = new FamilyMemberEntity
            {
                UserId = command.UserId,
                Name = name,
                RelationshipId = relationshipId,
                DateOfBirth = req.DateOfBirth,
                CreatedOn = clock.GetUtcNow().UtcDateTime,
            };
            if (!await members.AddAsync(entity, ct))
                return Result.Fail<FamilyMemberDto>(ErrorKind.Conflict, DuplicateMessage);

            // Relationship ka naam DTO mein chahiye — wapas padh lo (ek chhoti query)
            var dto = await members.GetAsync(command.UserId, entity.Id, ct);
            return dto is null ? Result.Fail<FamilyMemberDto>(ErrorKind.NotFound, "Family member not found.") : Result.Success(dto);
        }
    }
}
