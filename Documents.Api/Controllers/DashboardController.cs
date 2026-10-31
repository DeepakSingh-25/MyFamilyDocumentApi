using Documents.Application.Queries;
using Documents.Core.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Documents.Api.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    public class DashboardController(ISender sender) : ApiControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<DashboardSummaryDto>> Get(CancellationToken ct) =>
            Ok(await sender.Send(new GetDashboardQuery(UserId), ct));
    }
}
