using Documents.Core.Interfaces;
using Documents.Core.Models;
using MediatR;

namespace Documents.Application.Queries
{
    /// <summary>System categories + is user ki apni active categories (spec 013).</summary>
    public record GetCategoriesQuery(Guid UserId) : IRequest<IReadOnlyList<DocumentCategoryDto>>;

    public class GetCategoriesQueryHandler(ICategoryRepository categories) : IRequestHandler<GetCategoriesQuery, IReadOnlyList<DocumentCategoryDto>>
    {
        public Task<IReadOnlyList<DocumentCategoryDto>> Handle(GetCategoriesQuery request, CancellationToken ct) =>
            categories.ListAsync(request.UserId, ct);
    }
}
