using Documents.Core.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Documents.Core
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddCoreDI(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<ConnectionStringOptions>(configuration.GetSection(ConnectionStringOptions.SectionName));

            // JWT options — auth repository IOptions<JwtOptions> inject karega
            services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

            services.Configure<SignupOptions>(configuration.GetSection(SignupOptions.SectionName));
            services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
            services.Configure<AppOptions>(configuration.GetSection(AppOptions.SectionName));
            services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
            services.Configure<CorsOptions>(configuration.GetSection(CorsOptions.SectionName));

            return services;
        }
    }
}
