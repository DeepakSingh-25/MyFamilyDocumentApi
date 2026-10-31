using Documents.Application.Common;
using Documents.Core.Interfaces;
using Documents.Core.Models;
using Documents.Core.Options;
using MediatR;
using Microsoft.Extensions.Options;

namespace Documents.Application.Queries
{
    public record GetDashboardQuery(Guid UserId) : IRequest<DashboardSummaryDto>;

    public class GetDashboardQueryHandler(IDocumentRepository documents, IOptions<AppOptions> app, TimeProvider clock)
        : IRequestHandler<GetDashboardQuery, DashboardSummaryDto>
    {
        public Task<DashboardSummaryDto> Handle(GetDashboardQuery request, CancellationToken ct) =>
            documents.GetSummaryAsync(request.UserId, LocalClock.MonthStartUtc(clock, app.Value), ct);
    }
}
