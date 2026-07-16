using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authentication;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Lifecycle;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Provisioning;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Services;
using AethericForge.Runtime.Institutions.Abstractions.Builders;
using AethericForge.Runtime.Institutions.Campus;
using AethericForge.Runtime.Institutions.PostOffice;
using AethericForge.Runtime.Institutions.Registrar;
using AethericForge.Runtime.Providers.Identity.InMemory;
using AethericForge.Runtime.Services.Identity;
using AethericForge.Runtime.Services.Identity.Lifecycle;

namespace AethericForge.Web.Hosting;

public static class ForgeCampusExtensions 
{
    public static IServiceCollection AddForgeCampus(this IServiceCollection services)
    {
        services.AddSingleton<IIdentityProvider>(new InMemoryIdentityProvider("Local", IdentityScheme.Local));
        services.AddSingleton<IIdentityLifecycleService, IdentityLifecycleService>();
        services.AddSingleton<IIdentityService, IdentityService>();

        services.AddSingleton<ICampus>(serviceProvider =>
        {
            var template = InstitutionTemplateBuilder.Create()
                .WithDescriptor(
                    "ForgeCampus",
                    new Version(0, 1, 0),
                    "The Aetheric Forge learning and collaboration campus.")
                .Build();

            var context = new CampusContext(template, serviceProvider);
            var campus = new Campus(context);

            var registrarTemplate = InstitutionTemplateBuilder.Create()
                .WithDescriptor("Registrar", new Version(1, 0, 0), "Campus Registrar")
                .Build();

            var registrarContext = new RegistrarContext(registrarTemplate, serviceProvider, campus);
            var identityService = serviceProvider.GetRequiredService<IIdentityService>();
            var registrar = new Registrar(registrarContext, identityService);

            campus.Register<IRegistrar>(registrar);

            var postOfficeTemplate = InstitutionTemplateBuilder.Create()
                .WithDescriptor("PostOffice", new Version(1, 0, 0), "Campus Post Office")
                .Build();

            var postOfficeContext = new PostOfficeContext(postOfficeTemplate, serviceProvider, campus);
            var postExchange = serviceProvider.GetRequiredService<IPostExchange>();
            var postmaster = serviceProvider.GetRequiredService<IPostmaster>();
            var postOffice = new PostOffice(postOfficeContext, postExchange, postmaster);

            campus.Register<IPostOffice>(postOffice);

            return campus;
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
                    host.IsRunning,
                    registrar = new
                    {
                        name = campus.Registrar.Context.Template.Descriptor.Name,
                        version = campus.Registrar.Context.Template.Descriptor.Version.ToString()
                    },
                    postOffice = new
                    {
                        name = campus.PostOffice.Context.Template.Descriptor.Name,
                        version = campus.PostOffice.Context.Template.Descriptor.Version.ToString()
                    }
                }));

        return endpoints;
    }
}
