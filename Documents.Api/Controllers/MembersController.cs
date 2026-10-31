using Documents.Application.Commands;
using Documents.Application.Queries;
using Documents.Core.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Documents.Api.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    public class MembersController(ISender sender) : ApiControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<FamilyMemberDto>>> List(CancellationToken ct) =>
            Ok(await sender.Send(new GetMembersQuery(UserId), ct));

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
            ToActionResult(await sender.Send(new GetMemberQuery(UserId, id), ct), Ok);

        [HttpPost]
        public async Task<IActionResult> Create(MemberRequest request, CancellationToken ct) =>
            ToActionResult(await sender.Send(new CreateMemberCommand(UserId, request), ct),
                member => CreatedAtAction(nameof(Get), new { id = member.Id }, member));

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, MemberRequest request, CancellationToken ct) =>
            ToActionResult(await sender.Send(new UpdateMemberCommand(UserId, id, request), ct), Ok);

        /// <summary>Member + uske documents + files.</summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
            ToActionResult(await sender.Send(new DeleteMemberCommand(UserId, id), ct), NoContent);
    }
}
