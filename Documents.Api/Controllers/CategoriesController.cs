using Documents.Application.Commands;
using Documents.Application.Queries;
using Documents.Core.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Documents.Api.Controllers
{
    /// <summary>System + user ki apni categories (spec 013). System rows read-only (403), kisi aur ki → 404.</summary>
    [Authorize]
    [Route("api/[controller]")]
    public class CategoriesController(ISender sender) : ApiControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<DocumentCategoryDto>>> List(CancellationToken ct) =>
            Ok(await sender.Send(new GetCategoriesQuery(UserId), ct));

        [HttpPost, EnableRateLimiting(RateLimits.LookupWrites)]
        public async Task<IActionResult> Create(CategoryRequest request, CancellationToken ct) =>
            ToActionResult(await sender.Send(new CreateCategoryCommand(UserId, request), ct),
                category => StatusCode(StatusCodes.Status201Created, category));

        [HttpPut("{id:guid}"), EnableRateLimiting(RateLimits.LookupWrites)]
        public async Task<IActionResult> Update(Guid id, CategoryRequest request, CancellationToken ct) =>
            ToActionResult(await sender.Send(new UpdateCategoryCommand(UserId, id, request), ct), Ok);

        /// <summary>Deactivate; documents wali category par 409.</summary>
        [HttpDelete("{id:guid}"), EnableRateLimiting(RateLimits.LookupWrites)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
            ToActionResult(await sender.Send(new DeleteCategoryCommand(UserId, id), ct), NoContent);
    }
}
