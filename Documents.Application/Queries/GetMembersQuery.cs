using Documents.Core.Interfaces;
using Documents.Core.Models;
using MediatR;

namespace Documents.Application.Queries
{
    public record GetMembersQuery(Guid UserId) : IRequest<IReadOnlyList<FamilyMemberDto>>;

    public class GetMembersQueryHandler(IFamilyMemberRepository members) : IRequestHandler<GetMembersQuery, IReadOnlyList<FamilyMemberDto>>
    {
        public Task<IReadOnlyList<FamilyMemberDto>> Handle(GetMembersQuery request, CancellationToken ct) =>
            members.ListAsync(request.UserId, ct);
    }
}
