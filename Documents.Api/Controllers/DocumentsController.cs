using System.Net.Http.Headers;
using Documents.Api.Models;
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
    public class DocumentsController(ISender sender) : ApiControllerBase
    {
        // 10 MB file + form fields ke liye thodi jagah
        private const long MaxRequestBytes = 11L * 1024 * 1024;

        /// <summary>Ek page, newest first. Response hamesha envelope: items, page, pageSize, totalCount, totalPages.</summary>
        [HttpGet]
        [ProducesResponseType<PagedResult<FamilyDocumentDto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> List(
            [FromQuery] string? search, [FromQuery] Guid? memberId, [FromQuery] Guid? categoryId,
            CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = PageRequest.DefaultSize) =>
            ToActionResult(await sender.Send(
                new GetDocumentsQuery(UserId, new DocumentFilter(search, memberId, categoryId), new PageRequest(page, pageSize)), ct), Ok);

        /// <summary>Tabs / chips ke counts (search nahi). "counts" literal hai, isliye {id:guid} se clash nahi.</summary>
        [HttpGet("counts")]
        public async Task<ActionResult<DocumentCountsDto>> Counts([FromQuery] Guid? memberId, [FromQuery] Guid? categoryId, CancellationToken ct) =>
            Ok(await sender.Send(new GetDocumentCountsQuery(UserId, memberId, categoryId), ct));

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
            ToActionResult(await sender.Send(new GetDocumentQuery(UserId, id), ct), Ok);

        [HttpPost]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(MaxRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
        public async Task<IActionResult> Create([FromForm] DocumentFormRequest form, CancellationToken ct)
        {
            await using var content = form.File?.OpenReadStream();
            var file = form.File is null ? null : new UploadedFile(content!, form.File.FileName, form.File.Length);
            return ToActionResult(await sender.Send(new CreateDocumentCommand(UserId, form.ToDetails(), file), ct),
                doc => CreatedAtAction(nameof(Get), new { id = doc.Id }, doc));
        }

        [HttpPut("{id:guid}")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(MaxRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
        public async Task<IActionResult> Update(Guid id, [FromForm] DocumentFormRequest form, CancellationToken ct)
        {
            await using var content = form.File?.OpenReadStream();
            var file = form.File is null ? null : new UploadedFile(content!, form.File.FileName, form.File.Length);
            return ToActionResult(await sender.Send(new UpdateDocumentCommand(UserId, id, form.ToDetails(), file), ct), Ok);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
            ToActionResult(await sender.Send(new DeleteDocumentCommand(UserId, id), ct), NoContent);

        /// <summary>Private file — sirf owner ko, API ke through. Preview ke liye inline.</summary>
        [HttpGet("{id:guid}/file")]
        public async Task<IActionResult> File(Guid id, CancellationToken ct) =>
            ToActionResult(await sender.Send(new GetDocumentFileQuery(UserId, id), ct), file =>
            {
                Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline") { FileNameStar = file.FileName }.ToString();
                Response.Headers.XContentTypeOptions = "nosniff";
                Response.Headers.CacheControl = "private, no-store";
                // Polyglot file (PDF + HTML) API origin par script na chala sake
                Response.Headers.ContentSecurityPolicy = "sandbox";
                return File(file.Content, file.ContentType);
            });
    }
}
