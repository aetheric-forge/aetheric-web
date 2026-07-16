using AethericForge.Runtime.Institutions.Abstractions.Builders;
using AethericForge.Runtime.Institutions.Campus;

namespace AethericForge.Web.Hosting;

public static class ForgeCampusExtensions
{
    public static IServiceCollection AddForgeCampus(this IServiceCollection services)
    {
        services.AddSingleton<ICampus>(serviceProvider =>
        {
            var template = InstitutionTemplateBuilder.Create()
                .WithDescriptor(
                    "ForgeCampus",
                    new Version(0, 1, 0),
                    "The Aetheric Forge learning and collaboration campus.")
                .Build();

            var context = new CampusContext(template, serviceProvider);
            return new Campus(context);
        });

        services.AddSingleton<ForgeCampusHost>();
        services.AddHostedService<ForgeCampusHost>(
            serviceProvider => serviceProvider.GetRequiredService<ForgeCampusHost>());

        return services;
    }

    public static IEndpointRouteBuilder MapForgeCampusDiagnostics(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/api/runtime",
            (ICampus campus, ForgeCampusHost host) =>
                Results.Ok(new
                {
                    institution = campus.Context.Template.Descriptor.Name,
                    version = campus.Context.Template.Descriptor.Version.ToString(),
                    isRoot = campus.Context.Parent is null,
                    host.IsRunning
                }));

        return endpoints;
    }
}
