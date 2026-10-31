using MediatR.NotificationPublishers;
using Microsoft.Extensions.DependencyInjection;

namespace Documents.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationDI(this IServiceCollection services)
        {
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
                cfg.NotificationPublisher = new TaskWhenAllPublisher();
            });

            return services;
        }
    }
}
