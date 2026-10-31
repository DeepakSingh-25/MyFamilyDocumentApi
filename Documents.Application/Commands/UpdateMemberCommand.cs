using Documents.Application.Common;
using Documents.Core.Interfaces;
using Documents.Core.Models;
using Documents.Core.Options;
using Documents.Core.Rules;
using MediatR;
using Microsoft.Extensions.Options;

namespace Documents.Application.Commands
{
    public record UpdateMemberCommand(Guid UserId, Guid Id, MemberRequest Request) : IRequest<Result<FamilyMemberDto>>;

    public class UpdateMemberCommandHandler(
        IFamilyMemberRepository members,
        IRelationshipRepository relationships,
        IOptions<AppOptions> app,
        TimeProvider clock)
        : IRequestHandler<UpdateMemberCommand, Result<FamilyMemberDto>>
    {
        private const string NotFoundMessage = "Family member not found.";
        private const string DuplicateMessage = "A family member with this name already exists.";

        public async Task<Result<FamilyMemberDto>> Handle(UpdateMemberCommand command, CancellationToken ct)
        {
            var req = command.Request;
            var problem = MemberValidation.Check(req, LocalClock.Today(clock, app.Value));
            if (problem is not null) return Result.Fail<FamilyMemberDto>(ErrorKind.Validation, problem);
            var relationshipId = req.RelationshipId!.Value;
            if (!await relationships.ExistsAsync(command.UserId, relationshipId, ct))
                return Result.Fail<FamilyMemberDto>(ErrorKind.Validation, MemberValidation.RelationshipMessage);

            if (relationshipId == DomainRules.SelfRelationshipId)
            {
                // Pehle 404 (kisi aur ka / missing id), phir Self clash — sirf "Self" par ek extra query
                if (!await members.ExistsAsync(command.UserId, command.Id, ct))
                    return Result.Fail<FamilyMemberDto>(ErrorKind.NotFound, NotFoundMessage);
                if (await members.SelfExistsAsync(command.UserId, command.Id, ct))
                    return Result.Fail<FamilyMemberDto>(ErrorKind.Conflict, MemberValidation.SelfTakenMessage);
            }

            // Alag Exists/NameExists check nahi (spec 007 §B.4): UpdateAsync member load karta hai (null = 404)
            // aur naam clash unique index pakadta hai (false = 409)
            var saved = await members.UpdateAsync(command.UserId, command.Id, req.Name.Trim(), relationshipId, req.DateOfBirth, ct);
            if (saved is null) return Result.Fail<FamilyMemberDto>(ErrorKind.NotFound, NotFoundMessage);
            if (saved == false) return Result.Fail<FamilyMemberDto>(ErrorKind.Conflict, DuplicateMessage);

            var dto = await members.GetAsync(command.UserId, command.Id, ct);
            return dto is null ? Result.Fail<FamilyMemberDto>(ErrorKind.NotFound, NotFoundMessage) : Result.Success(dto);
        }
    }
}
