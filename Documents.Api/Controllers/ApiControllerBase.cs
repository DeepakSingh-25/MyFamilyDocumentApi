using System.IdentityModel.Tokens.Jwt;
using Documents.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Documents.Api.Controllers
{
    /// <summary>Result → HTTP status + ProblemDetails (message "detail" mein, frontend wahi dikhata hai).</summary>
    [ApiController]
    public abstract class ApiControllerBase : ControllerBase
    {
        /// <summary>Signed-in user ka id — sirf JWT "sub" claim se, kabhi request body/query se nahi.</summary>
        protected Guid UserId => Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
            ? id
            // [Authorize] ke bina yahan pahunchna programming bug hai — kabhi anonymous user ka id mat banao
            : throw new InvalidOperationException("UserId used on an endpoint without [Authorize].");

        protected IActionResult ToActionResult(Result result, Func<IActionResult> onSuccess) =>
            result.IsSuccess ? onSuccess() : Failure(result);

        protected IActionResult ToActionResult<T>(Result<T> result, Func<T, IActionResult> onSuccess) =>
            result.IsSuccess ? onSuccess(result.Value!) : Failure(result);

        private ObjectResult Failure(Result result)
        {
            var status = result.Kind switch
            {
                ErrorKind.Validation => StatusCodes.Status400BadRequest,
                ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
                ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
                ErrorKind.NotFound => StatusCodes.Status404NotFound,
                ErrorKind.Conflict => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status500InternalServerError,
            };
            return Problem(detail: result.Error, statusCode: status);
        }
    }
}
