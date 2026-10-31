using Documents.Core.Interfaces;
using Documents.Core.Models;
using MediatR;

namespace Documents.Application.Queries
{
    public record GetMemberQuery(Guid UserId, Guid Id) : IRequest<Result<FamilyMemberDto>>;

    public class GetMemberQueryHandler(IFamilyMemberRepository members) : IRequestHandler<GetMemberQuery, Result<FamilyMemberDto>>
    {
        public async Task<Result<FamilyMemberDto>> Handle(GetMemberQuery request, CancellationToken ct)
        {
            var member = await members.GetAsync(request.UserId, request.Id, ct);
            return member is null
                ? Result.Fail<FamilyMemberDto>(ErrorKind.NotFound, "Family member not found.")
                : Result.Success(member);
        }
    }
}
