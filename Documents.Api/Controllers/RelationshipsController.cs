using Documents.Application.Commands;
using Documents.Application.Queries;
using Documents.Core.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Documents.Api.Controllers
{
    /// <summary>System + user ke apne relationships (spec 013). System rows read-only (403), kisi aur ka → 404.</summary>
    [Authorize]
    [Route("api/[controller]")]
    public class RelationshipsController(ISender sender) : ApiControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<RelationshipDto>>> List(CancellationToken ct) =>
            Ok(await sender.Send(new GetRelationshipsQuery(UserId), ct));

        [HttpPost, EnableRateLimiting(RateLimits.LookupWrites)]
        public async Task<IActionResult> Create(RelationshipRequest request, CancellationToken ct) =>
            ToActionResult(await sender.Send(new CreateRelationshipCommand(UserId, request), ct),
                relationship => StatusCode(StatusCodes.Status201Created, relationship));

        [HttpPut("{id:guid}"), EnableRateLimiting(RateLimits.LookupWrites)]
        public async Task<IActionResult> Update(Guid id, RelationshipRequest request, CancellationToken ct) =>
            ToActionResult(await sender.Send(new UpdateRelationshipCommand(UserId, id, request), ct), Ok);

        /// <summary>Deactivate; kisi member ke use mein ho to 409.</summary>
        [HttpDelete("{id:guid}"), EnableRateLimiting(RateLimits.LookupWrites)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
            ToActionResult(await sender.Send(new DeleteRelationshipCommand(UserId, id), ct), NoContent);
    }
}
