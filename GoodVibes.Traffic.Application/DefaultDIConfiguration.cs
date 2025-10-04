using Microsoft.Extensions.DependencyInjection;

namespace GoodVibes.Traffic.Application;

public static class DefaultDIConfiguration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        return services;
    }
}