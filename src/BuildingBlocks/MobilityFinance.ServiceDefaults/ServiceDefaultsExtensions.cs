using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace MobilityFinance.ServiceDefaults;

public static class ServiceDefaultsExtensions
{
    public const string CorrelationHeaderName = "X-Correlation-ID";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks();

        OpenTelemetryBuilder telemetry = builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(
                resource => resource.AddService(
                    builder.Environment.ApplicationName));

        telemetry.WithTracing(
            tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                if (HasOtlpEndpoint(builder))
                {
                    tracing.AddOtlpExporter();
                }
            });

        telemetry.WithMetrics(
            metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(
                        "MobilityFinance.Activation",
                        "MobilityFinance.Assets",
                        "MobilityFinance.Ledger",
                        "MobilityFinance.Messaging",
                        "MobilityFinance.Origination");

                if (HasOtlpEndpoint(builder))
                {
                    metrics.AddOtlpExporter();
                }
            });

        return builder;
    }

    public static WebApplication UseServiceDefaults(this WebApplication app)
    {
        app.Use(
            async (context, next) =>
            {
                string correlationId = ResolveCorrelationId(context);
                context.Response.Headers[CorrelationHeaderName] = correlationId;
                Activity.Current?.SetTag("mobility.correlation_id", correlationId);

                using IDisposable? scope = context.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("MobilityFinance.Request")
                    .BeginScope(
                        new Dictionary<string, object>
                        {
                            ["CorrelationId"] = correlationId,
                        });

                await next(context);
            });

        return app;
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

    private static bool HasOtlpEndpoint(IHostApplicationBuilder builder)
    {
        return !string.IsNullOrWhiteSpace(
            builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        string? supplied = context.Request.Headers[CorrelationHeaderName]
            .FirstOrDefault();

        return supplied is not null
            && supplied.Length <= 128
            && supplied.All(character => !char.IsControl(character))
                ? supplied
                : Guid.NewGuid().ToString("N");
    }
}
