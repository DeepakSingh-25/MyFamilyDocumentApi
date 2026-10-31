using Documents.Application.Commands;
using Documents.Application.Queries;
using Documents.Core.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Documents.Api.Controllers
{
    /// <summary>My Account — profile dekhna, naam aur photo badalna (spec 005 §B.1). GET /api/auth/me waisa hi rehta hai.</summary>
    [Authorize]
    [Route("api/[controller]")]
    public class AccountController(ISender sender) : ApiControllerBase
    {
        // 2 MB photo + multipart ke liye thodi jagah
        private const long MaxPhotoRequestBytes = 3L * 1024 * 1024;

        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile(CancellationToken ct) =>
            ToActionResult(await sender.Send(new GetProfileQuery(UserId), ct), Ok);

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request, CancellationToken ct) =>
            ToActionResult(await sender.Send(new UpdateProfileCommand(UserId, request), ct), Ok);

        /// <summary>Apni photo — sirf token wale user ki, private storage se. Photo nahi to 404.</summary>
        [HttpGet("photo")]
        public async Task<IActionResult> GetPhoto(CancellationToken ct) =>
            ToActionResult(await sender.Send(new GetProfilePhotoQuery(UserId), ct), photo =>
            {
                Response.Headers.XContentTypeOptions = "nosniff";
                Response.Headers.CacheControl = "private, no-store";
                Response.Headers.ContentSecurityPolicy = "sandbox";
                return File(photo.Content, photo.ContentType);
            });

        /// <summary>Photo upload / replace (multipart, field "file"). Naya profile lautata hai.</summary>
        [HttpPut("photo")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(MaxPhotoRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxPhotoRequestBytes)]
        public async Task<IActionResult> UploadPhoto(IFormFile? file, CancellationToken ct)
        {
            if (file is null)
                return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "Please choose a photo to upload.");
            await using var content = file.OpenReadStream();
            return ToActionResult(await sender.Send(
                new SetProfilePhotoCommand(UserId, new UploadedFile(content, file.FileName, file.Length)), ct), Ok);
        }

        [HttpDelete("photo")]
        public async Task<IActionResult> DeletePhoto(CancellationToken ct) =>
            ToActionResult(await sender.Send(new DeleteProfilePhotoCommand(UserId), ct), Ok);
    }
}
