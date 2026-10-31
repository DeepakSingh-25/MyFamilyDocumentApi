using System.IdentityModel.Tokens.Jwt;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Documents.Api
{
    /// <summary>
    /// Auth endpoints par brute-force rokne ke liye limits (ASP.NET Core built-in, koi package nahi). Anonymous endpoints
    /// per-IP; change-password aur lookup writes per user (JWT "sub").
    /// </summary>
    public static class RateLimits
    {
        public const string Login = "login";
        public const string Signup = "signup";
        public const string ForgotPassword = "forgot-password";
        public const string ResetPassword = "reset-password";
        public const string ChangePassword = "change-password";
        /// <summary>Apni categories / relationships banana, badalna, hatana (spec 013) — delete + create loop se tables na bharein.</summary>
        public const string LookupWrites = "lookup-writes";

        public static IServiceCollection AddAuthRateLimits(this IServiceCollection services)
        {
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                AddPerIp(options, Login, permits: 5, TimeSpan.FromMinutes(1));
                AddPerIp(options, ForgotPassword, permits: 3, TimeSpan.FromMinutes(1));
                AddPerIp(options, ResetPassword, permits: 5, TimeSpan.FromMinutes(1));
                AddPerIp(options, Signup, permits: 5, TimeSpan.FromHours(1));
                AddPerUser(options, ChangePassword, permits: 5, TimeSpan.FromMinutes(1));
                AddPerUser(options, LookupWrites, permits: 60, TimeSpan.FromMinutes(1));
            });
            return services;
        }

        private static void AddPerIp(RateLimiterOptions options, string policy, int permits, TimeSpan window) =>
            options.AddPolicy(policy, context => RateLimitPartition.GetFixedWindowLimiter(
                IpKey(context),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = window, QueueLimit = 0 }));

        // UseRateLimiter UseAuthentication ke baad chalta hai, isliye User yahan bhar chuka hota hai.
        // "sub" na mile (hona nahi chahiye — endpoint [Authorize] hai) to IP par fallback.
        private static void AddPerUser(RateLimiterOptions options, string policy, int permits, TimeSpan window) =>
            options.AddPolicy(policy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value is { Length: > 0 } sub ? $"user:{sub}" : $"ip:{IpKey(context)}",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = window, QueueLimit = 0 }));

        private static string IpKey(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
