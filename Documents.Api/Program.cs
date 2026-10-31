using Documents.Api;
using Documents.Core.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ─── Services ──────────────────────────────────────────────────────────────────

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // Validation errors bhi ProblemDetails; pehla message "detail" mein taaki frontend seedha dikha sake
        options.InvalidModelStateResponseFactory = context =>
        {
            var problem = new ValidationProblemDetails(context.ModelState)
            {
                Status = StatusCodes.Status400BadRequest,
                Detail = context.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                    .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m)) ?? "Please check the form and try again.",
            };
            return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
        };
    });
// JSON: default camelCase (spec 001 D1) — Angular models se match karta hai

builder.Services.AddProblemDetails();
builder.Services.AddAuthRateLimits();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Swagger se protected endpoints try karne ke liye "Authorize" button
    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
    };
    options.AddSecurityDefinition("Bearer", scheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = [] });
});

// CORS — allowed web origins config (Cors:Origins) se
var corsOrigins = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>()?.Origins ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy
            .WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddAppDI(builder.Configuration);

var app = builder.Build();

// ─── Middleware Pipeline ───────────────────────────────────────────────────────

// Unhandled exception → generic 500 ProblemDetails (stack trace kabhi client ko nahi)
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();

// CORS must be before Authentication/Authorization
app.UseCors("AllowAngular");

app.UseAuthentication();
app.UseAuthorization();
// UseAuthentication ke BAAD hi rahe: change-password ki limit JWT "sub" se user-wise lagti hai (RateLimits.cs)
app.UseRateLimiter();

app.MapControllers();

app.Run();

/// <summary>Documents.Api.Tests ka WebApplicationFactory&lt;Program&gt; ise use karta hai.</summary>
public partial class Program;
