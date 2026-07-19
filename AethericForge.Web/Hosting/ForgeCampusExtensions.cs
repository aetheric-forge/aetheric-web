using AethericForge.Runtime.Abstractions.Interfaces.Archive.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Providers;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Serialization;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Lifecycle;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Provisioning;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Providers;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Library.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Providers;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Staging.Providers;
using AethericForge.Runtime.Abstractions.Interfaces.Staging.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Workbench.Services;
using AethericForge.Runtime.Institutions.Abstractions.Builders;
using AethericForge.Runtime.Institutions.Abstractions.Composition;
using AethericForge.Runtime.Institutions.Abstractions.Models;
using AethericForge.Runtime.Institutions.Abstractions.Primitives;
using AethericForge.Runtime.Institutions.Archive;
using AethericForge.Runtime.Institutions.Campus;
using AethericForge.Runtime.Institutions.Library;
using AethericForge.Runtime.Institutions.PostOffice;
using AethericForge.Runtime.Institutions.Registry;
using AethericForge.Runtime.Institutions.Workbench;
using AethericForge.Runtime.Models.Archive.Serialization;
using AethericForge.Runtime.Models.Authorities;
using AethericForge.Runtime.Providers.Archive.MongoDb;
using AethericForge.Runtime.Providers.Archive.S3;
using AethericForge.Runtime.Providers.Identity.Keycloak;
using AethericForge.Runtime.Providers.Knowledge.MongoDb;
using AethericForge.Runtime.Providers.Post.RabbitMq;
using AethericForge.Runtime.Providers.Staging.Redis;
using AethericForge.Runtime.Services.Archive;
using AethericForge.Runtime.Services.Identity;
using AethericForge.Runtime.Services.Identity.Lifecycle;
using AethericForge.Runtime.Services.Knowledge;
using AethericForge.Runtime.Services.Library;
using AethericForge.Runtime.Services.Post;
using AethericForge.Runtime.Services.Registry;
using AethericForge.Runtime.Services.Staging;
using AethericForge.Runtime.Services.Workbench;
using AethericForge.Web.Abstractions.Person;
using AethericForge.Web.Services;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using MongoDB.Driver;
using StackExchange.Redis;

namespace AethericForge.Web.Hosting;

public static class ForgeCampusExtensions 
{
    private static string BuildMongoUri(IConfiguration configuration)
    {
        var host = GetRequiredSetting(configuration, "MongoDb:Host");
        var username = GetRequiredSetting(configuration, "MongoDb:Username");
        var password = GetRequiredSetting(configuration, "MongoDb:Password");
        var databaseName = GetRequiredSetting(configuration, "MongoDb:DatabaseName");
        var authenticationDatabase = GetRequiredSetting(
            configuration,
            "MongoDb:AuthenticationDatabase");

        var port = configuration.GetValue<int?>("MongoDb:Port")
                   ?? throw new InvalidOperationException("MongoDb:Port is required.");

        var builder = new MongoUrlBuilder
        {
            Server = new MongoServerAddress(host, port),
            Username = username,
            Password = password,
            DatabaseName = databaseName,
            AuthenticationSource = authenticationDatabase,
            AuthenticationMechanism = configuration.GetValue<string>(
                "MongoDb:AuthenticationMechanism",
                "SCRAM-SHA-256"),
            DirectConnection = configuration.GetValue(
                "MongoDb:DirectConnection",
                true)
        };

        return builder.ToMongoUrl().ToString();
    }

    private static string BuildRabbitMqUrl(IConfiguration configuration)
    {
        var useSsl = configuration.GetValue("RabbitMq:Ssl", false);
        var builder = new UriBuilder
        {
            Scheme = useSsl ? "amqps" : "amqp",
            Host = GetRequiredSetting(configuration, "RabbitMq:Host"),
            Port = configuration.GetValue<int?>("RabbitMq:Port")
                   ?? (useSsl ? 5671 : 5672),
            UserName = GetRequiredSetting(configuration, "RabbitMq:Username"),
            Password = GetRequiredSetting(configuration, "RabbitMq:Password"),
            Path = Uri.EscapeDataString(GetRequiredSetting(
                configuration,
                "RabbitMq:VirtualHost"))
        };

        return builder.Uri.ToString();
    }
    
    private static string GetRequiredSetting(
        IConfiguration configuration,
        string key)
    {
        var value = configuration[key];

        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"{key} is required.");
    }    
    
    private static AmazonS3Config BuildS3Config(IConfiguration configuration)
    {
        return new AmazonS3Config
        {
            ServiceURL = GetRequiredSetting(configuration, "S3:ServiceUrl"),
            ForcePathStyle = configuration.GetValue("S3:ForcePathStyle", true),
            AuthenticationRegion = configuration.GetValue(
                "S3:AuthenticationRegion",
                "us-east-1")
        };
    }

    private static IAmazonS3 BuildS3Client(IConfiguration configuration)
    {
        var credentials = new BasicAWSCredentials(
            GetRequiredSetting(configuration, "S3:AccessKey"),
            GetRequiredSetting(configuration, "S3:SecretKey"));

        return new AmazonS3Client(
            credentials,
            BuildS3Config(configuration));
    }
    
    public static IServiceCollection AddForgeCampus(this IServiceCollection services)
    {
        services.AddInstitutionTemplate(builder =>
        {
            builder.WithDescriptor(
                    "ForgeCampus",
                    new Version(1, 0, 0),
                    "The Aetheric Forge learning and collaboration campus.")
                .With<IIdentityLifecycleService, IdentityLifecycleService>()
                .With<IIdentityService, IdentityService>()
                .With<IPersonService, PersonService>()
                .With<IIdentityRegistry, IdentityRegistry>()
                .With<HttpClient, HttpClient>()
                .With<KeycloakOptions>(sp => new KeycloakOptions
                {
                    ClientId =  sp.GetRequiredService<IConfiguration>().GetValue<string>("Keycloak:ClientId")
                                   ?? throw new InvalidOperationException("Keycloak:ClientId is required"),
                    Realm =  sp.GetRequiredService<IConfiguration>().GetValue<string>("Keycloak:Realm")
                                   ?? throw new InvalidOperationException("Keycloak:Realm is required"),
                    Authority = sp.GetRequiredService<IConfiguration>().GetValue<string>("Keycloak:Authority")
                                ?? throw new InvalidOperationException("Keycloak:Authority is required"),
                    ClientSecret = sp.GetRequiredService<IConfiguration>().GetValue<string>("Keycloak:ClientSecret")
                                   ?? throw new InvalidOperationException("Keycloak:ClientSecret is required")
                })
                .With<IIdentityProvider, KeycloakIdentityProvider>()
                .With<IRegistryService, RegistryService>()
                .With<ITeam<IRegistryClerk>>(_ => new Team<IRegistryClerk>(Array.Empty<IRegistryClerk>()))
                .With<IRegistrar, Registrar>()
                .With<IRegistryContext, RegistryContext>()
                .With<IRegistry, Registry>()
                .With<IArchiveSerializer, JsonArchiveSerializer>()
                .With<AWSCredentials>(sp => new BasicAWSCredentials(
                    sp.GetRequiredService<IConfiguration>().GetValue<string>("S3:AccessKey"),
                    sp.GetRequiredService<IConfiguration>().GetValue<string>("S3:SecretKey")))
                .With<IAmazonS3>(sp => new AmazonS3Client(
                    sp.GetRequiredService<AWSCredentials>(), 
                    BuildS3Config(sp.GetRequiredService<IConfiguration>())))
                .With<IArchiveProvider>(sp => new S3ArchiveProvider(
                    sp.GetRequiredService<IAmazonS3>(),
                    "MinIO",
                    "forge-campus-archive"))
                .With<IArchiveService, ArchiveService>()
                .With<ITeam<IArchiveClerk>>(_ => new Team<IArchiveClerk>(Array.Empty<IArchiveClerk>()))
                .With<IArchivist, Archivist>()
                .With<IArchiveVault, ArchiveVault>()
                .With<IArchiveContext, ArchiveContext>()
                .With<IArchive, Archive>()
                .With<IMongoClient>(sp => new MongoClient(BuildMongoUri(sp.GetRequiredService<IConfiguration>())))
                .With<IMongoDatabase>(sp => sp
                    .GetRequiredService<IMongoClient>()
                    .GetDatabase(GetRequiredSetting(
                        sp.GetRequiredService<IConfiguration>(),
                        "MongoDb:DatabaseName")))
                .With<IKnowledgeProvider>(sp => new MongoDbKnowledgeProvider(
                    sp.GetRequiredService<IMongoDatabase>(), "ForgeCampus", "knowledge"))
                .With<IKnowledgeService, KnowledgeService>()
                .With<ITeam<ICuratorClerk>>(_ => new Team<ICuratorClerk>(Array.Empty<ICuratorClerk>()))
                .With<ICurator, Curator>()
                .With<ILibraryService, LibraryService>()
                .With<ITeam<ILibraryClerk>>(_ => new Team<ILibraryClerk>(Array.Empty<ILibraryClerk>()))
                .With<ILibrarian, Librarian>()
                .With<ILibraryContext, LibraryContext>()
                .With<ILibrary, Library>()
                .With<IPostProvider>(sp => new RabbitMqPostProvider( "ForgeCampus", 
                    BuildRabbitMqUrl(
                       sp.GetRequiredService<IConfiguration>())))
                .With<IPostService, PostService>()
                .With<ITeam<IPostClerk>>(_ => new Team<IPostClerk>(Array.Empty<IPostClerk>()))
                .With<IPostExchange, PostExchange>()
                .With<IPostmaster, Postmaster>()
                .With<IPostOfficeContext, PostOfficeContext>()
                .With<IPostOffice, PostOffice>()
                .With<IConnectionMultiplexer>(serviceProvider =>
                {
                    var configuration =
                        serviceProvider.GetRequiredService<IConfiguration>();

                    var options = new ConfigurationOptions
                    {
                        EndPoints =
                        {
                            {
                                GetRequiredSetting(configuration, "Redis:Host"),
                                configuration.GetValue<int?>("Redis:Port") ?? 6379
                            }
                        },
                        Password = configuration["Redis:Password"],
                        Ssl = configuration.GetValue<bool>("Redis:Ssl"),
                        DefaultDatabase = configuration.GetValue<int?>("Redis:Database") ?? 0,
                        AbortOnConnectFail = false
                    };

                    return ConnectionMultiplexer.Connect(options);
                })                
                .With<IStagingProvider>(sp => new RedisStagingProvider(sp.GetRequiredService<IConnectionMultiplexer>(), "Default"))
                .With<IStagingService, StagingService>()
                .With<IWorkbenchService, WorkbenchService>()
                .With<ITeam<IWorkbenchWorker>>(_ => new Team<IWorkbenchWorker>(Array.Empty<IWorkbenchWorker>()))
                .With<IArtificer, Artificer>()
                .With<IWorkbenchContext, WorkbenchContext>()
                .With<IWorkbench, Workbench>();
        });

        services.AddSingleton<ICampus>(serviceProvider =>
        {
            var campusTemplate = (InstitutionTemplate)serviceProvider.GetRequiredService<IInstitutionTemplate>();
            var campusContext = new CampusContext(campusTemplate, serviceProvider);

            var campus = new Campus(campusContext);

            var registryTemplate = campusTemplate with { Descriptor = new InstitutionDescriptor("Registry", campusTemplate.Descriptor.Version, "Registry institution") };
            campus.Register<IRegistry>(ActivatorUtilities.CreateInstance<Registry>(serviceProvider, new RegistryContext(registryTemplate, serviceProvider, campus)));
            
            var archiveTemplate = campusTemplate with { Descriptor = new InstitutionDescriptor("Archive", campusTemplate.Descriptor.Version, "Archive institution") };
            campus.Register<IArchive>(ActivatorUtilities.CreateInstance<Archive>(serviceProvider, new ArchiveContext(archiveTemplate, serviceProvider, campus)));

            var libraryTemplate = campusTemplate with { Descriptor = new InstitutionDescriptor("Library", campusTemplate.Descriptor.Version, "Library institution") };
            campus.Register<ILibrary>(ActivatorUtilities.CreateInstance<Library>(serviceProvider, new LibraryContext(libraryTemplate, serviceProvider, campus)));

            var postOfficeTemplate = campusTemplate with { Descriptor = new InstitutionDescriptor("PostOffice", campusTemplate.Descriptor.Version, "Post Office institution") };
            campus.Register<IPostOffice>(ActivatorUtilities.CreateInstance<PostOffice>(serviceProvider, new PostOfficeContext(postOfficeTemplate, serviceProvider, campus)));

            var workbenchTemplate = campusTemplate with { Descriptor = new InstitutionDescriptor("Workbench", campusTemplate.Descriptor.Version, "Workbench institution") };
            campus.Register<IWorkbench>(ActivatorUtilities.CreateInstance<Workbench>(serviceProvider, new WorkbenchContext(workbenchTemplate, serviceProvider, campus)));
            
            return campus;
        });

        services.AddSingleton<ForgeCampusHost>();
        services.AddHostedService<ForgeCampusHost>(serviceProvider =>
            serviceProvider.GetRequiredService<ForgeCampusHost>());
        
        return services;
    }

    public static IEndpointRouteBuilder MapForgeCampusDiagnostics(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/api/status",
            (ICampus campus, ForgeCampusHost host) =>
                Results.Ok(new
                {
                    institution = campus.Context.Template.Descriptor.Name,
                    version = campus.Context.Template.Descriptor.Version.ToString(),
                    isRoot = campus.Context.Parent is null,
                    host.IsRunning,
                    registrar = new
                    {
                        name = campus.Registry.Context.Template.Descriptor.Name,
                        version = campus.Registry.Context.Template.Descriptor.Version.ToString()
                    },
                    postOffice = new
                    {
                        name = campus.PostOffice.Context.Template.Descriptor.Name,
                        version = campus.PostOffice.Context.Template.Descriptor.Version.ToString()
                    },
                    archive = new
                    {
                        name = campus.Archive.Context.Template.Descriptor.Name,
                        version = campus.Archive.Context.Template.Descriptor.Version.ToString()
                    },
                    library = new
                    {
                        name = campus.Library.Context.Template.Descriptor.Name,
                        version = campus.Library.Context.Template.Descriptor.Version.ToString()
                    }
                }));

        return endpoints;
    }
}
