using System.IdentityModel.Tokens.Jwt;
using Documents.Application.Commands;
using Documents.Application.Queries;
using Documents.Core.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Documents.Api.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    public class AuthController(ISender sender) : ApiControllerBase
    {
        [AllowAnonymous]
        [HttpGet("registration-status")]
        public async Task<ActionResult<RegistrationStatusDto>> RegistrationStatus(CancellationToken ct) =>
            await sender.Send(new GetRegistrationStatusQuery(), ct);

        [AllowAnonymous]
        [HttpPost("signup")]
        [EnableRateLimiting(RateLimits.Signup)]
        public async Task<IActionResult> Signup(SignupRequest request, CancellationToken ct) =>
            ToActionResult(await sender.Send(new SignupCommand(request), ct), () => StatusCode(StatusCodes.Status201Created));

        [AllowAnonymous]
        [HttpPost("login")]
        [EnableRateLimiting(RateLimits.Login)]
        public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct) =>
            ToActionResult(await sender.Send(new LoginCommand(request), ct), Ok);

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me(CancellationToken ct) =>
            ToActionResult(await sender.Send(new GetMeQuery(UserId), ct), Ok);

        /// <summary>Hamesha 202 — email registered ho ya na ho.</summary>
        [AllowAnonymous]
        [HttpPost("forgot-password")]
        [EnableRateLimiting(RateLimits.ForgotPassword)]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
        {
            await sender.Send(new ForgotPasswordCommand(request.Email), ct);
            return Accepted();
        }

        [AllowAnonymous]
        [HttpPost("reset-password")]
        [EnableRateLimiting(RateLimits.ResetPassword)]
        public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct) =>
            ToActionResult(await sender.Send(new ResetPasswordCommand(request), ct), NoContent);

        /// <summary>Server-side logout: ye token turant band (204). Dobara bulane par 401, kyunki token ab valid nahi.</summary>
        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout(CancellationToken ct)
        {
            var jti = User.FindFirst(JwtRegisteredClaimNames.Jti)!.Value;
            var exp = long.Parse(User.FindFirst(JwtRegisteredClaimNames.Exp)!.Value);
            await sender.Send(new LogoutCommand(UserId, jti, DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime), ct);
            return NoContent();
        }

        /// <summary>
        /// 200 { token, expiresOn, user } — login jaisa hi shape. Naya token is tab ko signed-in rakhta hai; baaki sab
        /// tokens (call karne wala purana token bhi) pwv mismatch ki wajah se agli request par 401.
        /// </summary>
        [Authorize]
        [HttpPost("change-password")]
        [EnableRateLimiting(RateLimits.ChangePassword)]
        public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct) =>
            ToActionResult(await sender.Send(new ChangePasswordCommand(UserId, request), ct), Ok);
    }
}
