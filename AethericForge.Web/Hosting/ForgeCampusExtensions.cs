using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Providers;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Serialization;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Faculty.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Governance.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Lifecycle;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Provisioning;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Providers;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Library.Services;
using AethericForge.Runtime.Abstractions.Interfaces.IssueReports.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Security.Services;
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
using AethericForge.Runtime.Institutions.Faculty;
using AethericForge.Runtime.Institutions.Governance;
using AethericForge.Runtime.Institutions.Library;
using AethericForge.Runtime.Institutions.IssueReports;
using AethericForge.Runtime.Institutions.Security;
using AethericForge.Runtime.Institutions.PostOffice;
using AethericForge.Runtime.Institutions.Registry;
using AethericForge.Runtime.Institutions.Workbench;
using AethericForge.Runtime.Models.Archive.Serialization;
using AethericForge.Runtime.Models.Authorities;
using AethericForge.Runtime.Models.Library.Articles;
using AethericForge.Runtime.Providers.Archive.S3;
using AethericForge.Runtime.Providers.Identity.Keycloak;
using AethericForge.Runtime.Providers.Knowledge.MongoDb;
using AethericForge.Runtime.Providers.Post.RabbitMq;
using AethericForge.Runtime.Providers.Staging.Redis;
using AethericForge.Runtime.Services.Archive;
using AethericForge.Runtime.Services.Faculty;
using AethericForge.Runtime.Services.Governance;
using AethericForge.Runtime.Services.Identity;
using AethericForge.Runtime.Services.Identity.Lifecycle;
using AethericForge.Runtime.Services.Knowledge;
using AethericForge.Runtime.Services.Library;
using AethericForge.Runtime.Services.IssueReports;
using AethericForge.Runtime.Services.Security;
using AethericForge.Runtime.Services.Post;
using AethericForge.Runtime.Services.Registry;
using AethericForge.Runtime.Services.Staging;
using AethericForge.Runtime.Services.Workbench;
using AethericForge.Web.Abstractions.Person;
using AethericForge.Web.Services;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Forge.Primitives.MongoDb;
using Forge.Primitives.Redis;
using MongoDB.Driver;
using StackExchange.Redis;
using AethericForge.Web.Hosting.Modules;
using TalentCampus.Core.Personas;
using TalentCampus.Core.Recruitment;
using TalentCampus.Core.Roles;
using TalentCampus.Institutions.Talent;
using TalentCampus.Providers.MongoDb;

namespace AethericForge.Web.Hosting;

public static class ForgeCampusExtensions 
{
    private const string DecisionsArchiveStore = "adr-campus";
    private const string DecisionsKnowledgeScheme = "adr-campus";
    private const string DecisionsWorkbenchStage = "adr-campus-workbench";
    private const string DecisionsMaintenanceDomain = "adr-campus-maintenance";

    internal static string BuildRabbitMqUrl(IConfiguration configuration)
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

    private static TGovernanceBoard RegisterGovernanceBoard<TGovernanceBoard>(
        Campus campus,
        InstitutionTemplate campusTemplate,
        IServiceProvider serviceProvider,
        string name,
        Func<IGovernanceContext, ICouncil, TGovernanceBoard> factory)
        where TGovernanceBoard : class, IGovernanceBoard
    {
        var template = campusTemplate with
        {
            Descriptor = new InstitutionDescriptor(name, campusTemplate.Descriptor.Version, $"{name} board")
        };
        var context = new GovernanceContext(template, serviceProvider, campus);
        var council = new Council(new Team<ICouncilMember>(Array.Empty<ICouncilMember>()));
        var board = factory(context, council);
        campus.Register<TGovernanceBoard>(board);
        return board;
    }
    
    internal static AmazonS3Config BuildS3Config(IConfiguration configuration)
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

    internal static IAmazonS3 BuildS3Client(IConfiguration configuration)
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
        foreach (var institution in new[] { "ParallelYou" })
            services.AddKeyedSingleton<IAmazonS3>(institution, (sp, _) => BuildS3Client(
                InstitutionServiceConfiguration.Resolve(sp.GetRequiredService<IConfiguration>(), institution, "S3")));

        foreach (var institution in new[] { "Talent" })
        {
            services.AddKeyedSingleton<IMongoClient>(institution, (sp, _) => new MongoClient(
                InstitutionServiceConfiguration.Resolve(sp.GetRequiredService<IConfiguration>(), institution, "MongoDb")
                    .GetSection("MongoDb").Get<MongoOptions>()!.ToConnectionString()));
            services.AddKeyedSingleton<IMongoDatabase>(institution, (sp, _) =>
                sp.GetRequiredKeyedService<IMongoClient>(institution).GetDatabase(GetRequiredSetting(
                    InstitutionServiceConfiguration.Resolve(sp.GetRequiredService<IConfiguration>(), institution, "MongoDb"),
                    "MongoDb:DatabaseName")));
        }

        services.AddKeyedSingleton<IConnectionMultiplexer>("Workbench", (sp, _) =>
        {
            var redisSection = InstitutionServiceConfiguration.Resolve(
                    sp.GetRequiredService<IConfiguration>(), "Workbench", "Redis")
                .GetSection("Redis");
            var options = redisSection.Get<RedisOptions>()!;
            options.DefaultDatabase = redisSection.GetValue("Database", 0);
            return ConnectionMultiplexer.Connect(options.ToConfigurationOptions());
        });

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
                .With<KeycloakOptions>(sp => InstitutionServiceConfiguration.Resolve(
                    sp.GetRequiredService<IConfiguration>(), "Registry", "Keycloak")
                    .GetSection("Keycloak").Get<KeycloakOptions>()!)
                .With<IIdentityProvider, KeycloakIdentityProvider>()
                .With<IRegistryClerk, KeycloakRegistryClerk>()
                .With<IRegistryService, RegistryService>()
                .With<ITeam<IRegistryClerk>>(_ => new Team<IRegistryClerk>(Array.Empty<IRegistryClerk>()))
                .With<IRegistrar, Registrar>()
                .With<IRegistryContext, RegistryContext>()
                .With<IRegistry, Registry>()
                .With<IArchiveService>(sp => sp.GetRequiredKeyedService<IArchiveService>("Archive"))
                .With<IArchivist>(sp => sp.GetRequiredKeyedService<IArchivist>("Archive"))
                .With<ITeam<IIssueReportsClerk>>(_ => new Team<IIssueReportsClerk>(Array.Empty<IIssueReportsClerk>()))
                .With<OperationsLedgerStore>(sp => new OperationsLedgerStore(
                    sp.GetRequiredService<IStagingService>(),
                    sp.GetRequiredService<IConfiguration>()["Decisions:Workbench:Stage"] ?? DecisionsWorkbenchStage))
                .With<IWarden>(sp => new Warden(sp.GetRequiredService<OperationsLedgerStore>(),
                    sp.GetRequiredService<ITeam<IIssueReportsClerk>>()))
                .With<ITeam<ISecurityClerk>>(_ => new Team<ISecurityClerk>(Array.Empty<ISecurityClerk>()))
                .With<ISentinel>(sp => new Sentinel(sp.GetRequiredService<OperationsLedgerStore>(),
                    sp.GetRequiredService<ITeam<ISecurityClerk>>()))
                .With<IArchiveVault>(sp => sp.GetRequiredKeyedService<IArchiveVault>("Archive"))
                .With<IKnowledgeService>(sp => sp.GetRequiredKeyedService<IKnowledgeService>("Library"))
                .With<ITeam<ICuratorClerk>>(_ => new Team<ICuratorClerk>(Array.Empty<ICuratorClerk>()))
                .With<ICurator, Curator>()
                .With<ILibraryService, LibraryService>()
                .With<ITeam<ILibraryClerk>>(_ => new Team<ILibraryClerk>(Array.Empty<ILibraryClerk>()))
                .With<ILibrarian, Librarian>()
                .With<IPostProvider>(sp => new RabbitMqPostProvider( "ForgeCampus", 
                    BuildRabbitMqUrl(
                       InstitutionServiceConfiguration.Resolve(sp.GetRequiredService<IConfiguration>(), "PostOffice", "RabbitMq"))))
                .With<IPostProvider>(sp => new RabbitMqPostProvider(
                    DecisionsMaintenanceDomain,
                    BuildRabbitMqUrl(InstitutionServiceConfiguration.Resolve(sp.GetRequiredService<IConfiguration>(), "Decisions", "RabbitMq"))))
                .With<IPostService, PostService>()
                .With<ITeam<IPostClerk>>(_ => new Team<IPostClerk>(Array.Empty<IPostClerk>()))
                .With<IPostExchange, PostExchange>()
                .With<IPostmaster, Postmaster>()
                .With<IPostOfficeContext, PostOfficeContext>()
                .With<IPostOffice, PostOffice>()
                .With<IStagingProvider>(sp => new RedisStagingProvider(sp.GetRequiredKeyedService<IConnectionMultiplexer>("Workbench"), "Default"))
                .With<IStagingProvider>(sp => new RedisStagingProvider(sp.GetRequiredKeyedService<IConnectionMultiplexer>("Workbench"), "ReflectionMapping"))
                .With<IStagingProvider>(sp => new RedisStagingProvider(sp.GetRequiredKeyedService<IConnectionMultiplexer>("Workbench"), "TrackingCurrent"))
                .With<IStagingProvider>(sp => new RedisStagingProvider(sp.GetRequiredKeyedService<IConnectionMultiplexer>("Workbench"), "IntentionCurrent"))
                .With<IStagingProvider>(sp => new RedisStagingProvider(sp.GetRequiredKeyedService<IConnectionMultiplexer>("Workbench"), "PlanCurrent"))
                .With<IStagingProvider>(sp => new RedisStagingProvider(sp.GetRequiredKeyedService<IConnectionMultiplexer>("Workbench"), "RecommendationCurrent"))
                .With<IStagingService, StagingService>()
                .With<IStagingProvider>(sp => new RedisStagingProvider(
                    sp.GetRequiredKeyedService<IConnectionMultiplexer>("Workbench"),
                    sp.GetRequiredService<IConfiguration>()["Decisions:Workbench:Stage"] ?? DecisionsWorkbenchStage))
                .With<IWorkbenchService, WorkbenchService>()
                .With<ITeam<IWorkbenchWorker>>(_ => new Team<IWorkbenchWorker>(Array.Empty<IWorkbenchWorker>()))
                .With<IArtificer, Artificer>()
                .With<IWorkbenchContext, WorkbenchContext>()
                .With<IWorkbench, Workbench>()
                .With<ITalentSteward>(sp => new MongoTalentSteward(sp.GetRequiredKeyedService<IMongoDatabase>("Talent")))
                .With<IPersonaDirectory>(sp => new MongoPersonaDirectory(sp.GetRequiredKeyedService<IMongoDatabase>("Talent")))
                .With<IRecruitmentOffice>(sp => new MongoRecruitmentOffice(
                    sp.GetRequiredKeyedService<IMongoDatabase>("Talent"),
                    sp.GetRequiredService<ITalentSteward>(),
                    sp.GetRequiredService<IPersonaDirectory>(),
                    sp.GetRequiredService<IConfiguration>()
                        .GetSection("Talent:Recruitment:DecisionsOffices").Get<DecisionsDestination[]>() ?? []));
        });

        services.AddSingleton<ICampus>(serviceProvider =>
        {
            var campusTemplate = (InstitutionTemplate)serviceProvider.GetRequiredService<IInstitutionTemplate>();
            var campusContext = new CampusContext(campusTemplate, serviceProvider);

            var campus = new Campus(campusContext);

            campus.RegisterInstitution<IRegistry, Registry, RegistryContext>(
                campusTemplate, serviceProvider, "Registry",
                static (template, sp, parent) => new RegistryContext(template, sp, parent));

            serviceProvider.GetRequiredKeyedService<GitInstitutionWebModule>("library").Register(campus, serviceProvider);
            serviceProvider.GetRequiredKeyedService<GitInstitutionWebModule>("archive").Register(campus, serviceProvider);

            campus.RegisterInstitution<IPostOffice, PostOffice, PostOfficeContext>(
                campusTemplate, serviceProvider, "PostOffice",
                static (template, sp, parent) => new PostOfficeContext(template, sp, parent));

            campus.RegisterInstitution<IWorkbench, Workbench, WorkbenchContext>(
                campusTemplate, serviceProvider, "Workbench",
                static (template, sp, parent) => new WorkbenchContext(template, sp, parent));

            serviceProvider.GetRequiredKeyedService<GitInstitutionWebModule>("parallel-you").Register(campus, serviceProvider);

            campus.RegisterFaculty<IArchitectureFaculty>(
                campusTemplate, serviceProvider, "Architecture", "Principal Architect",
                static (context, dean) => new ArchitectureFaculty(context, dean));

            var administrationFaculty = campus.RegisterFaculty<IAdministrationFaculty>(
                campusTemplate, serviceProvider, "Administration", "Administrator",
                static (context, dean) => new AdministrationFaculty(context, dean));

            serviceProvider.GetRequiredKeyedService<GitInstitutionWebModule>("decisions").Register(administrationFaculty, serviceProvider);

            var designFaculty = campus.RegisterFaculty<IDesignFaculty>(
                campusTemplate, serviceProvider, "Design", "Director",
                static (context, dean) => new DesignFaculty(context, dean));

            var talentTemplate = campusTemplate with
            {
                Descriptor = new InstitutionDescriptor(
                    "Talent",
                    new Version(0, 1, 0),
                    "Defines roles and stewards evidence-based talent lifecycles.")
            };
            designFaculty.Register<ITalent>(new global::TalentCampus.Institutions.Talent.Talent(
                new TalentContext(talentTemplate, serviceProvider, designFaculty),
                serviceProvider.GetRequiredService<ITalentSteward>()));

            campus.RegisterFaculty<IEngineeringFaculty>(
                campusTemplate, serviceProvider, "Engineering", "Chief Engineer",
                static (context, dean) => new EngineeringFaculty(context, dean));
            campus.RegisterFaculty<IManufacturingFaculty>(
                campusTemplate, serviceProvider, "Manufacturing", "Chief Fabricator",
                static (context, dean) => new ManufacturingFaculty(context, dean));
            var operations = campus.RegisterFaculty<IOperationsFaculty>(
                campusTemplate, serviceProvider, "Operations", "Quartermaster",
                static (context, dean) => new OperationsFaculty(context, dean));
            campus.RegisterFaculty<IFinanceFaculty>(
                campusTemplate, serviceProvider, "Finance", "Chief Accountant",
                static (context, dean) => new FinanceFaculty(context, dean));
            campus.RegisterFaculty<IInsuranceFaculty>(
                campusTemplate, serviceProvider, "Insurance", "Chief Underwriter",
                static (context, dean) => new InsuranceFaculty(context, dean));
            campus.RegisterFaculty<IFederationFaculty>(
                campusTemplate, serviceProvider, "Federation", "Envoy",
                static (context, dean) => new FederationFaculty(context, dean));
            RegisterGovernanceBoard<IGovernanceBoard>(
                campus, campusTemplate, serviceProvider, "Governance",
                static (context, council) => new GovernanceBoard(context, council));

            var issueReportsTemplate = campusTemplate with
            {
                Descriptor = new InstitutionDescriptor("Issue Reports", campusTemplate.Descriptor.Version, "Issue Reports institution")
            };
            var issueReportsContext = new IssueReportsContext(issueReportsTemplate, serviceProvider, operations);
            operations.Register<IIssueReports>(
                ActivatorUtilities.CreateInstance<global::AethericForge.Runtime.Institutions.IssueReports.IssueReports>(
                    serviceProvider,
                    issueReportsContext));

            var securityTemplate = campusTemplate with
            {
                Descriptor = new InstitutionDescriptor("Security", campusTemplate.Descriptor.Version, "Security institution")
            };
            var securityContext = new SecurityContext(securityTemplate, serviceProvider, operations);
            operations.Register<ISecurity>(
                ActivatorUtilities.CreateInstance<global::AethericForge.Runtime.Institutions.Security.Security>(
                    serviceProvider,
                    securityContext));

            return campus;
        });


        services.AddScoped<OperationsWorkspace>(sp =>
        {
            var operations = sp.GetRequiredService<ICampus>().Resolve<IOperationsFaculty>();
            return new OperationsWorkspace(
                operations.Resolve<IIssueReports>().Warden,
                operations.Resolve<ISecurity>().Sentinel,
                sp.GetRequiredService<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider>(),
                sp.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationService>());
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
            (ICampus campus, ForgeCampusHost host, [FromKeyedServices("parallel-you")] GitInstitutionWebModule module) =>
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
                    },
                    parallelYou = new
                    {
                        name = module.Resolve(campus).Context.Template.Descriptor.Name,
                        version = module.Resolve(campus).Context.Template.Descriptor.Version.ToString()
                    },
                    faculties = new[]
                    {
                        FacultyStatus(campus.Resolve<IAdministrationFaculty>()),
                        FacultyStatus(campus.Resolve<IArchitectureFaculty>()),
                        FacultyStatus(campus.Resolve<IDesignFaculty>()),
                        FacultyStatus(campus.Resolve<IEngineeringFaculty>()),
                        FacultyStatus(campus.Resolve<IManufacturingFaculty>()),
                        FacultyStatus(campus.Resolve<IOperationsFaculty>())
                    },
                    issueReports = new
                    {
                        name = campus.Resolve<IOperationsFaculty>().Resolve<IIssueReports>()
                            .Context.Template.Descriptor.Name,
                        version = campus.Resolve<IOperationsFaculty>().Resolve<IIssueReports>()
                            .Context.Template.Descriptor.Version.ToString()
                    },
                    security = new
                    {
                        name = campus.Resolve<IOperationsFaculty>().Resolve<ISecurity>()
                            .Context.Template.Descriptor.Name,
                        version = campus.Resolve<IOperationsFaculty>().Resolve<ISecurity>()
                            .Context.Template.Descriptor.Version.ToString()
                    }
                }));

        return endpoints;
    }

    private static object FacultyStatus(IFaculty faculty) => new
    {
        name = faculty.Context.Template.Descriptor.Name,
        version = faculty.Context.Template.Descriptor.Version.ToString(),
        dean = faculty.Dean.Title,
        institutions = faculty switch
        {
            IAdministrationFaculty => new[] { faculty.ResolveOrganization<IOrganization>("decisions").Context.Template.Descriptor.Name },
            IDesignFaculty => new[] { faculty.Resolve<ITalent>().Context.Template.Descriptor.Name },
            _ => []
        }
    };
}
