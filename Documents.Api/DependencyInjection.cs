using Documents.Application;
using Documents.Core;
using Documents.Infrastructure;

namespace Documents.Api
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddAppDI(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddApplicationDI()
                .AddInfrastructureDI(configuration)
                .AddCoreDI(configuration);

            return services;
        }
    }
}
