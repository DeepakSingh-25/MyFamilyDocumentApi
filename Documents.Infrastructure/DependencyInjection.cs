using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Documents.Core.Interfaces;
using Documents.Core.Options;
using Documents.Infrastructure.Data;
using Documents.Infrastructure.Repositories;
using Documents.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Documents.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructureDI(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<AppDbContext>((provider, options) =>
            {
                options.UseSqlServer(provider.GetRequiredService<IOptionsSnapshot<ConnectionStringOptions>>().Value.DefaultConnection);
            });

            // Repositories
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IFamilyMemberRepository, FamilyMemberRepository>();
            services.AddScoped<IDocumentRepository, DocumentRepository>();
            services.AddScoped<ICategoryRepository, CategoryRepository>();
            services.AddScoped<IRelationshipRepository, RelationshipRepository>();

            // Services
            services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
            services.AddSingleton<ITokenService, JwtTokenService>();
            services.AddSingleton<IFileStorage, LocalFileStorage>();
            services.AddScoped<IEmailSender, SmtpEmailSender>();

            // Clock — DateTime.UtcNow ke bajaye injectable TimeProvider
            services.TryAddSingleton(TimeProvider.System);

            // Private file storage — bina folder ke documents save nahi ho sakte
            var storageRoot = configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>()?.RootPath;
            if (string.IsNullOrWhiteSpace(storageRoot))
                throw new InvalidOperationException("Storage:RootPath must be set (a private folder outside the repo and web root).");

            // JWT Authentication
            var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                ?? throw new InvalidOperationException($"Missing '{JwtOptions.SectionName}' configuration section.");
            if (string.IsNullOrWhiteSpace(jwtOptions.SecretKey) || jwtOptions.SecretKey.Length < 32)
                throw new InvalidOperationException("Jwt:SecretKey must be set (at least 32 characters). Use user-secrets or an environment variable.");

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    // "sub" jaisa hai waisa rahe (ClaimTypes.NameIdentifier mein map na ho)
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwtOptions.Issuer,
                        ValidAudience = jwtOptions.Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
                        ClockSkew = JwtOptions.ClockSkew,
                        NameClaimType = JwtRegisteredClaimNames.Name,
                    };
                    options.Events = new JwtBearerEvents
                    {
                        // Token tabhi valid jab: pwv == DB ka PasswordVersion (password badla nahi) aur jti logout nahi hua
                        OnTokenValidated = async context =>
                        {
                            var principal = context.Principal!;
                            var jti = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                            // sub = user GUID (spec 014); purane int-sub tokens yahin 401 (500 kabhi nahi)
                            if (!Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId)
                                || !int.TryParse(principal.FindFirst(JwtTokenService.PasswordVersionClaim)?.Value, out var version)
                                || string.IsNullOrEmpty(jti))
                            {
                                context.Fail("Invalid token.");
                                return;
                            }

                            var users = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
                            var (current, revoked) = await users.GetSessionStateAsync(userId, jti, context.HttpContext.RequestAborted);
                            if (current != version || revoked)
                            {
                                AuthLogger(context.HttpContext).LogWarning(
                                    "JWT rejected for user {UserId}: {Reason}", userId,
                                    current is null ? "user not found" : revoked ? "token revoked (logged out)" : "password changed (pwv mismatch)");
                                context.Fail("Session has ended.");
                            }
                        },
                        // Signature / expiry / DB error — sab yahan aate hain; 401 ki asli wajah console mein dikhe
                        // (token khud kabhi log nahi hota)
                        OnAuthenticationFailed = context =>
                        {
                            AuthLogger(context.HttpContext).LogWarning(
                                context.Exception, "JWT authentication failed for {Path}: {Reason}",
                                context.Request.Path, context.Exception.Message);
                            return Task.CompletedTask;
                        },
                    };
                });

            return services;
        }

        private static ILogger AuthLogger(HttpContext http) =>
            http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Documents.Auth");
    }
}
