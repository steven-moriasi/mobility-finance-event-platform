using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MobilityFinance.ServiceDefaults;

public static class ServiceDefaultsExtensions
{
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks();

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks(
                "/health/live",
                new HealthCheckOptions { Predicate = static _ => false })
            .AllowAnonymous();

        app.MapHealthChecks("/health/ready")
            .AllowAnonymous();

        app.MapGet(
                "/_system",
                (IHostEnvironment environment) => Results.Ok(
                    new
                    {
                        service = environment.ApplicationName,
                        environment = environment.EnvironmentName,
                        version = typeof(ServiceDefaultsExtensions).Assembly
                            .GetName()
                            .Version?
                            .ToString(),
                    }))
            .AllowAnonymous()
            .ExcludeFromDescription();

        return app;
    }
}
