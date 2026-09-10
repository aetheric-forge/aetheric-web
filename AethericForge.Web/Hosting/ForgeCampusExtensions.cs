using AdrCampus.Application.Administration;
using AdrCampus.Application.Discovery;
using AdrCampus.Application.Drafts;
using AdrCampus.Application.Identity;
using AdrCampus.Application.Maintenance;
using AdrCampus.Application.Membership;
using AdrCampus.Application.Proposals;
using AdrCampus.Core.Administration;
using AdrCampus.Core.Discovery;
using AdrCampus.Core.Domain;
using AdrCampus.Core.Drafts;
using AdrCampus.Core.Maintenance;
using AdrCampus.Core.Membership;
using AdrCampus.Core.Proposals;
using AdrCampus.Providers.Archive;
using AdrCampus.Providers.Drafts.Workbench;
using AdrCampus.Providers.Library;
using AdrCampus.Providers.PostOffice;
using AdrCampus.Web.Drafts;
using AdrCampus.Web.Maintenance;
using AdrCampus.Web.Members;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Providers;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Serialization;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Faculty.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Lifecycle;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Provisioning;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Providers;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Library.Services;
using AethericForge.Runtime.Abstractions.Interfaces.IssueReports.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Security.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Maintenance.Services;
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
using AethericForge.Runtime.Institutions.Library;
using AethericForge.Runtime.Institutions.IssueReports;
using AethericForge.Runtime.Institutions.Security;
using AethericForge.Runtime.Institutions.Maintenance;
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
using AethericForge.Runtime.Services.Identity;
using AethericForge.Runtime.Services.Identity.Lifecycle;
using AethericForge.Runtime.Services.Knowledge;
using AethericForge.Runtime.Services.Library;
using AethericForge.Runtime.Services.IssueReports;
using AethericForge.Runtime.Services.Security;
using AethericForge.Runtime.Services.Maintenance;
using AethericForge.Runtime.Services.Post;
using AethericForge.Runtime.Services.Registry;
using AethericForge.Runtime.Services.Staging;
using AethericForge.Runtime.Services.Workbench;
using AethericForge.Web.Abstractions.Person;
using AethericForge.Web.Decisions.Institution;
using AethericForge.Web.Services;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using MongoDB.Driver;
using StackExchange.Redis;
using ParallelYou.Abstractions;
using ParallelYou.Abstractions.Capture;
using ParallelYou.Abstractions.Intention;
using ParallelYou.Abstractions.Plan;
using ParallelYou.Abstractions.Recommendation;
using ParallelYou.Abstractions.Reflection;
using ParallelYou.Abstractions.Tracking;
using ParallelYou.Services;
using ParallelYou.Services.Capture;
using ParallelYou.Services.Intention;
using ParallelYou.Services.Plan;
using ParallelYou.Services.Recommendation;
using ParallelYou.Services.Reflection;
using ParallelYou.Services.Tracking;

namespace AethericForge.Web.Hosting;

public static class ForgeCampusExtensions 
{
    private const string DecisionsArchiveStore = "adr-campus";
    private const string DecisionsKnowledgeScheme = "adr-campus";
    private const string DecisionsWorkbenchStage = "adr-campus-workbench";
    private const string DecisionsMaintenanceDomain = "adr-campus-maintenance";

    internal static string BuildMongoUri(IConfiguration configuration)
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

    private static TFaculty RegisterFaculty<TFaculty>(
        Campus campus,
        InstitutionTemplate campusTemplate,
        IServiceProvider serviceProvider,
        string name,
        string deanTitle,
        Func<IFacultyContext, IDean, TFaculty> factory)
        where TFaculty : class, IFaculty
    {
        var template = campusTemplate with
        {
            Descriptor = new InstitutionDescriptor(name, campusTemplate.Descriptor.Version, $"{name} faculty")
        };
        var context = new FacultyContext(template, serviceProvider, campus);
        var dean = new Dean(deanTitle, new Team<IFacultyClerk>(Array.Empty<IFacultyClerk>()));
        var faculty = factory(context, dean);
        campus.Register<TFaculty>(faculty);
        return faculty;
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
        foreach (var institution in new[] { "Archive", "ParallelYou", "Decisions" })
            services.AddKeyedSingleton<IAmazonS3>(institution, (sp, _) => BuildS3Client(
                InstitutionServiceConfiguration.Resolve(sp.GetRequiredService<IConfiguration>(), institution, "S3")));

        foreach (var institution in new[] { "Library", "ParallelYou", "Decisions" })
        {
            services.AddKeyedSingleton<IMongoClient>(institution, (sp, _) => new MongoClient(BuildMongoUri(
                InstitutionServiceConfiguration.Resolve(sp.GetRequiredService<IConfiguration>(), institution, "MongoDb"))));
            services.AddKeyedSingleton<IMongoDatabase>(institution, (sp, _) =>
                sp.GetRequiredKeyedService<IMongoClient>(institution).GetDatabase(GetRequiredSetting(
                    InstitutionServiceConfiguration.Resolve(sp.GetRequiredService<IConfiguration>(), institution, "MongoDb"),
                    "MongoDb:DatabaseName")));
        }

        foreach (var institution in new[] { "Workbench", "ParallelYou", "Decisions" })
            services.AddKeyedSingleton<IConnectionMultiplexer>(institution, (sp, _) =>
            {
                var configuration = InstitutionServiceConfiguration.Resolve(
                    sp.GetRequiredService<IConfiguration>(), institution, "Redis");
                return ConnectionMultiplexer.Connect(new ConfigurationOptions
                {
                    EndPoints = { { GetRequiredSetting(configuration, "Redis:Host"), configuration.GetValue<int?>("Redis:Port") ?? 6379 } },
                    User = configuration["Redis:User"],
                    Password = configuration["Redis:Password"],
                    Ssl = configuration.GetValue<bool>("Redis:Ssl"),
                    DefaultDatabase = configuration.GetValue<int?>("Redis:Database") ?? 0,
                    AbortOnConnectFail = false
                });
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
                .With<IRegistryService, RegistryService>()
                .With<ITeam<IRegistryClerk>>(_ => new Team<IRegistryClerk>(Array.Empty<IRegistryClerk>()))
                .With<IRegistrar, Registrar>()
                .With<IRegistryContext, RegistryContext>()
                .With<IRegistry, Registry>()
                .With<IArchiveSerializer, JsonArchiveSerializer>()
                .With<IArchiveProvider>(sp => new S3ArchiveProvider(
                    sp.GetRequiredKeyedService<IAmazonS3>("Archive"),
                    "MinIO",
                    InstitutionServiceConfiguration.Resolve(sp.GetRequiredService<IConfiguration>(), "Archive", "S3").GetValue(
                        "S3:BucketName",
                        "forge-campus-archive")))
                .With<IArchiveProvider>(sp => new S3ArchiveProvider(
                    sp.GetRequiredKeyedService<IAmazonS3>("ParallelYou"),
                    "ParallelYou",
                    InstitutionServiceConfiguration.Resolve(sp.GetRequiredService<IConfiguration>(), "ParallelYou", "S3").GetValue(
                        "S3:BucketName",
                        "forge-campus-archive"),
                    "parallel-you"))
                .With<IArchiveProvider>(sp => new S3ArchiveProvider(
                    sp.GetRequiredKeyedService<IAmazonS3>("Decisions"),
                    DecisionsArchiveStore,
                    InstitutionServiceConfiguration.Resolve(sp.GetRequiredService<IConfiguration>(), "Decisions", "S3").GetValue(
                        "S3:BucketName",
                        "forge-campus-archive"),
                    "adr-campus"))
                .With<IArchiveService, ArchiveService>()
                .With<ITeam<IArchiveClerk>>(_ => new Team<IArchiveClerk>(Array.Empty<IArchiveClerk>()))
                .With<IArchivist, Archivist>()
                .With<ITeam<IMaintenanceClerk>>(_ => new Team<IMaintenanceClerk>(Array.Empty<IMaintenanceClerk>()))
                .With<ICaretaker, Caretaker>()
                .With<ITeam<IIssueReportsClerk>>(_ => new Team<IIssueReportsClerk>(Array.Empty<IIssueReportsClerk>()))
                .With<IWarden, Warden>()
                .With<ITeam<ISecurityClerk>>(_ => new Team<ISecurityClerk>(Array.Empty<ISecurityClerk>()))
                .With<ISentinel, Sentinel>()
                .With<IArchiveVault, ArchiveVault>()
                .With<IArchiveContext, ArchiveContext>()
                .With<IArchive, Archive>()
                .With<IKnowledgeProvider>(sp => new MongoDbKnowledgeProvider(
                    sp.GetRequiredKeyedService<IMongoDatabase>("Library"), "ForgeCampus", "knowledge"))
                .With<IKnowledgeProvider>(sp => new MongoDbKnowledgeProvider(
                    sp.GetRequiredKeyedService<IMongoDatabase>("ParallelYou"), "parallel-you", "knowledge"))
                .With<IKnowledgeProvider>(sp => new MongoDbKnowledgeProvider(
                    sp.GetRequiredKeyedService<IMongoDatabase>("Decisions"),
                    DecisionsKnowledgeScheme,
                    "knowledge"))
                .With<IKnowledgeService, KnowledgeService>()
                .With<ITeam<ICuratorClerk>>(_ => new Team<ICuratorClerk>(Array.Empty<ICuratorClerk>()))
                .With<ICurator, Curator>()
                .With<ILibraryService, LibraryService>()
                .With(_ => new ArticleLibraryOptions
                {
                    KnowledgeScheme = "ForgeCampus",
                    Authority = new InstitutionReference
                    {
                        Id = "forge-library",
                        Name = "Library"
                    },
                    DefaultOrigin = new InstitutionReference
                    {
                        Id = "forge-campus",
                        Name = "Forge Campus"
                    }
                })
                .With<IArticleLibrary, ArticleLibrary>()
                .With<ITeam<ILibraryClerk>>(_ => new Team<ILibraryClerk>(Array.Empty<ILibraryClerk>()))
                .With<ILibrarian, Librarian>()
                .With<ILibraryContext, LibraryContext>()
                .With<ILibrary, global::AethericForge.Runtime.Institutions.Library.Library>()
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
                .With<IStagingProvider>(sp => new RedisStagingProvider(sp.GetRequiredKeyedService<IConnectionMultiplexer>("ParallelYou"), "ReflectionMapping"))
                .With<IStagingProvider>(sp => new RedisStagingProvider(sp.GetRequiredKeyedService<IConnectionMultiplexer>("ParallelYou"), "TrackingCurrent"))
                .With<IStagingProvider>(sp => new RedisStagingProvider(sp.GetRequiredKeyedService<IConnectionMultiplexer>("ParallelYou"), "IntentionCurrent"))
                .With<IStagingProvider>(sp => new RedisStagingProvider(sp.GetRequiredKeyedService<IConnectionMultiplexer>("ParallelYou"), "PlanCurrent"))
                .With<IStagingProvider>(sp => new RedisStagingProvider(sp.GetRequiredKeyedService<IConnectionMultiplexer>("ParallelYou"), "RecommendationCurrent"))
                .With<IStagingService, StagingService>()
                .With<IStagingProvider>(sp => new RedisStagingProvider(
                    sp.GetRequiredKeyedService<IConnectionMultiplexer>("Decisions"),
                    DecisionsWorkbenchStage))
                .With<IWorkbenchService, WorkbenchService>()
                .With<ITeam<IWorkbenchWorker>>(_ => new Team<IWorkbenchWorker>(Array.Empty<IWorkbenchWorker>()))
                .With<IArtificer, Artificer>()
                .With<IWorkbenchContext, WorkbenchContext>()
                .With<IWorkbench, Workbench>()
                .With<global::ParallelYou.Abstractions.Person.IPersonService,
                    global::ParallelYou.Services.Person.PersonService>()
                .With<ICaptureService, CaptureService>()
                .With<IReflectionService, ReflectionService>()
                .With<ITrackingService, TrackingService>()
                .With<IIntentionService, IntentionService>()
                .With<IPlanningService, PlanningService>()
                .With<IRecommendationService, RecommendationService>();
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
            campus.Register<ILibrary>(ActivatorUtilities.CreateInstance<global::AethericForge.Runtime.Institutions.Library.Library>(
                serviceProvider,
                new LibraryContext(libraryTemplate, serviceProvider, campus)));

            var postOfficeTemplate = campusTemplate with { Descriptor = new InstitutionDescriptor("PostOffice", campusTemplate.Descriptor.Version, "Post Office institution") };
            campus.Register<IPostOffice>(ActivatorUtilities.CreateInstance<PostOffice>(serviceProvider, new PostOfficeContext(postOfficeTemplate, serviceProvider, campus)));

            var workbenchTemplate = campusTemplate with { Descriptor = new InstitutionDescriptor("Workbench", campusTemplate.Descriptor.Version, "Workbench institution") };
            campus.Register<IWorkbench>(ActivatorUtilities.CreateInstance<Workbench>(serviceProvider, new WorkbenchContext(workbenchTemplate, serviceProvider, campus)));

            var parallelYouTemplate = campusTemplate with
            {
                Descriptor = new InstitutionDescriptor(
                    "ParallelYou",
                    new Version(1, 0, 0),
                    "A private practice for preserving observations, reflection, and chosen direction.")
            };
            campus.Register<IParallelYou>(ActivatorUtilities.CreateInstance<ParallelYouInstitution>(
                serviceProvider,
                new ParallelYouContext(parallelYouTemplate, serviceProvider, campus)));

            var architectureFaculty = RegisterFaculty<IArchitectureFaculty>(
                campus, campusTemplate, serviceProvider, "Architecture", "Principal Architect",
                static (context, dean) => new ArchitectureFaculty(context, dean));

            var decisionsTemplate = campusTemplate with
            {
                Descriptor = new InstitutionDescriptor(
                    "Decisions",
                    new Version(1, 0, 0),
                    "Shared architectural decision records.")
            };
            architectureFaculty.Register<IDecisions>(new DecisionsInstitution(
                new DecisionsContext(decisionsTemplate, serviceProvider, architectureFaculty)));

            RegisterFaculty<IDesignFaculty>(
                campus, campusTemplate, serviceProvider, "Design", "Director",
                static (context, dean) => new DesignFaculty(context, dean));
            RegisterFaculty<IEngineeringFaculty>(
                campus, campusTemplate, serviceProvider, "Engineering", "Chief Engineer",
                static (context, dean) => new EngineeringFaculty(context, dean));
            RegisterFaculty<IManufacturingFaculty>(
                campus, campusTemplate, serviceProvider, "Manufacturing", "Chief Fabricator",
                static (context, dean) => new ManufacturingFaculty(context, dean));
            var operations = RegisterFaculty<IOperationsFaculty>(
                campus, campusTemplate, serviceProvider, "Operations", "Quartermaster",
                static (context, dean) => new OperationsFaculty(context, dean));

            // The first institution actually nested under a Faculty rather than sitting flat on
            // Campus - Context.Parent is `operations`, not `campus`, and it's registered on
            // `operations`, so it resolves via the parent-chain walk InstitutionBase already
            // provides (proven generically by AethericForge.Runtime.Tests.Faculty.FacultyTests).
            var maintenanceTemplate = campusTemplate with
            {
                Descriptor = new InstitutionDescriptor("Maintenance", campusTemplate.Descriptor.Version, "Maintenance institution")
            };
            var maintenanceContext = new MaintenanceContext(maintenanceTemplate, serviceProvider, operations);
            operations.Register<IMaintenance>(
                ActivatorUtilities.CreateInstance<global::AethericForge.Runtime.Institutions.Maintenance.Maintenance>(
                    serviceProvider,
                    maintenanceContext));

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

        services.AddSingleton<ForgeCampusHost>();
        services.AddHostedService<ForgeCampusHost>(serviceProvider =>
            serviceProvider.GetRequiredService<ForgeCampusHost>());

        services.AddSingleton<IDraftRepository, WorkbenchDraftRepository>();
        services.AddSingleton<IDraftRecoveryRepository>(serviceProvider =>
            (WorkbenchDraftRepository)serviceProvider.GetRequiredService<IDraftRepository>());
        services.AddSingleton<IExpiredDraftPurgeRepository>(serviceProvider =>
            (WorkbenchDraftRepository)serviceProvider.GetRequiredService<IDraftRepository>());

        services.AddSingleton<LibraryProposalRepository>(serviceProvider => new LibraryProposalRepository(
            serviceProvider.GetRequiredService<ICampus>().Library,
            serviceProvider.GetRequiredService<IDraftRepository>()));
        services.AddSingleton<IProposalRepository>(serviceProvider =>
            serviceProvider.GetRequiredService<LibraryProposalRepository>());
        services.AddSingleton<ISharedRecordRepository>(serviceProvider =>
            serviceProvider.GetRequiredService<LibraryProposalRepository>());

        services.AddSingleton<IOrganizationAdministrationRepository, ArchiveOrganizationAdministrationRepository>();
        services.AddSingleton<IMembershipRepository, ArchiveMembershipRepository>();
        services.AddSingleton<IMaintenancePostOffice>(serviceProvider =>
            new PostOfficeMaintenanceDispatcher(serviceProvider.GetRequiredService<ICampus>().PostOffice));

        services.AddScoped<DraftApplicationService>();
        services.AddScoped<ProposalApplicationService>();
        services.AddScoped<DiscoveryApplicationService>();
        services.AddScoped<OrganizationAdministrationService>();
        services.AddScoped<DraftRecoveryApplicationService>();
        services.AddScoped<IDraftRecoveryCoordinator>(serviceProvider =>
            serviceProvider.GetRequiredService<DraftRecoveryApplicationService>());
        services.AddScoped<MaintenanceApplicationService>();
        services.AddScoped<AdministrationHistoryService>();
        services.AddScoped<MembershipObservationService>();
        services.AddHttpClient(MemberRosterService.HttpClientName);
        services.AddSingleton<MemberRosterService>();
        services.AddScoped<IMemberAuthority, KeycloakMemberAuthority>();
        services.AddScoped<IMemberDisplayNameDirectory, KeycloakMemberDisplayNameDirectory>();
        services.AddScoped<IDirectoryRosterSource, KeycloakDirectoryRosterSource>();
        services.AddScoped<IOrganizationBootstrapVerifier, KeycloakOrganizationBootstrapVerifier>();
        services.AddSingleton(serviceProvider => new CurrentOrganization(new OrganizationId(
            GetRequiredSetting(
                serviceProvider.GetRequiredService<IConfiguration>(),
                "Organization:Id"))));
        services.AddSingleton(serviceProvider =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            return new OrganizationBootstrapConfiguration(
                new OrganizationId(GetRequiredSetting(configuration, "Organization:Id")),
                GetRequiredSetting(configuration, "Organization:DisplayName"),
                GetRequiredSetting(InstitutionServiceConfiguration.Resolve(configuration, "Registry", "Keycloak"), "Keycloak:Authority"),
                GetRequiredSetting(configuration, "Organization:MemberGroupId"),
                GetRequiredSetting(configuration, "Organization:MaintainerGroupId"));
        });
        services.AddSingleton<OrganizationBootstrapHealth>();
        services.AddScoped<OrganizationDisplayState>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<AdrCampus.Core.Maintenance.IMaintenanceWorker,
            ExpiredDraftPurgeWorker>();
        services.AddHostedService<OrganizationBootstrapHostedService>();
        services.AddHostedService<MembershipSyncBackgroundService>();
        services.AddHostedService<MaintenanceDispatchService>();
        
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
                    },
                    parallelYou = new
                    {
                        name = campus.Resolve<IParallelYou>().Context.Template.Descriptor.Name,
                        version = campus.Resolve<IParallelYou>().Context.Template.Descriptor.Version.ToString()
                    },
                    faculties = new[]
                    {
                        FacultyStatus(campus.Resolve<IArchitectureFaculty>()),
                        FacultyStatus(campus.Resolve<IDesignFaculty>()),
                        FacultyStatus(campus.Resolve<IEngineeringFaculty>()),
                        FacultyStatus(campus.Resolve<IManufacturingFaculty>()),
                        FacultyStatus(campus.Resolve<IOperationsFaculty>())
                    },
                    maintenance = new
                    {
                        // Nested under Operations rather than a direct Campus child, so it's
                        // resolved from the Faculty instance, not the Campus - Resolve<T> only
                        // walks up the parent chain from the caller, never down into children.
                        name = campus.Resolve<IOperationsFaculty>().Resolve<IMaintenance>()
                            .Context.Template.Descriptor.Name,
                        version = campus.Resolve<IOperationsFaculty>().Resolve<IMaintenance>()
                            .Context.Template.Descriptor.Version.ToString()
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
        institutions = faculty is IArchitectureFaculty
            ? new[] { faculty.Resolve<IDecisions>().Context.Template.Descriptor.Name }
            : []
    };
}
