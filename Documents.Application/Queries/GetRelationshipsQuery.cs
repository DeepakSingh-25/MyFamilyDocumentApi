using Documents.Core.Interfaces;
using Documents.Core.Models;
using MediatR;

namespace Documents.Application.Queries
{
    /// <summary>System relationships + is user ke apne active relationships (spec 013).</summary>
    public record GetRelationshipsQuery(Guid UserId) : IRequest<IReadOnlyList<RelationshipDto>>;

    public class GetRelationshipsQueryHandler(IRelationshipRepository relationships) : IRequestHandler<GetRelationshipsQuery, IReadOnlyList<RelationshipDto>>
    {
        public Task<IReadOnlyList<RelationshipDto>> Handle(GetRelationshipsQuery request, CancellationToken ct) =>
            relationships.ListAsync(request.UserId, ct);
    }
}
