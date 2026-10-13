using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Providers;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Staging.Providers;
using AethericForge.Runtime.Abstractions.Interfaces.Library.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Workbench.Services;
using AethericForge.Runtime.Models.Authorities;
using AethericForge.Runtime.Providers.Knowledge.MongoDb;
using AethericForge.Runtime.Providers.Staging.Redis;
using AethericForge.Runtime.Services.Knowledge;
using AethericForge.Runtime.Services.Library;
using AethericForge.Runtime.Services.Staging;
using AethericForge.Runtime.Services.Workbench;
using AethericForge.Web.Authentication;
using Forge.Primitives.MongoDb;
using Forge.Primitives.Redis;
using MongoDB.Driver;
using ParallelYou.Abstractions;
using ParallelYou.Services;
using StackExchange.Redis;

namespace AethericForge.Web.Hosting;

public static class ParallelYouHosting
{
    public static IServiceCollection AddStandaloneParallelYou(this IServiceCollection services, IConfiguration configuration)
    {
        // Validate the selected module's configuration without requiring unrelated institutions.
        var mongo = InstitutionServiceConfiguration.Resolve(configuration, "ParallelYou", "MongoDb")
            .GetSection("MongoDb").Get<MongoOptions>()!;
        if (string.IsNullOrWhiteSpace(mongo.Host) || string.IsNullOrWhiteSpace(mongo.DatabaseName))
            throw new InvalidOperationException("ParallelYou MongoDb Host and DatabaseName are required.");
        var redisSection = InstitutionServiceConfiguration.Resolve(configuration, "Workbench", "Redis").GetSection("Redis");
        var redis = redisSection.Get<RedisOptions>()!;
        redis.DefaultDatabase = redisSection.GetValue("Database", 0);
        if (string.IsNullOrWhiteSpace(redis.Host))
            throw new InvalidOperationException("Workbench Redis Host is required.");

        services.AddKeyedSingleton<IMongoClient>("ParallelYou", (_, _) => new MongoClient(mongo.ToConnectionString()));
        services.AddKeyedSingleton<IMongoDatabase>("ParallelYou", (sp, _) =>
            sp.GetRequiredKeyedService<IMongoClient>("ParallelYou").GetDatabase(mongo.DatabaseName));
        services.AddKeyedSingleton<IConnectionMultiplexer>("Workbench", (_, _) =>
            ConnectionMultiplexer.Connect(redis.ToConfigurationOptions()));
        services.AddParallelYouModule(sp => Create(sp, new ParallelYouContext(
            ParallelYouModule.CreateTemplate(), sp)));
        services.AddParallelYouAccessors();
        return services;
    }

    internal static IServiceCollection AddParallelYouAccessors(this IServiceCollection services)
    {
        services.AddScoped<ICurrentIdentityAccessor, CurrentIdentityAccessor>();
        services.AddScoped<ICurrentParallelYouPersonAccessor, CurrentParallelYouPersonAccessor>();
        return services;
    }

    internal static IParallelYou Create(IServiceProvider services, IParallelYouContext context)
    {
        var knowledge = new MongoDbKnowledgeProvider(
            services.GetRequiredKeyedService<IMongoDatabase>("ParallelYou"), "parallel-you", "knowledge");
        var connection = services.GetRequiredKeyedService<IConnectionMultiplexer>("Workbench");
        var staging = ParallelYouModule.Stages.Select(stage => (IStagingProvider)new RedisStagingProvider(connection, stage));
        return ParallelYouModule.Create(context, CreateLibrarian(knowledge), CreateArtificer(staging));
    }

    public static ILibrarian CreateLibrarian(IKnowledgeProvider knowledge) =>
        new Librarian(new KnowledgeService([knowledge], new Team<ICuratorClerk>([])), new Team<ILibraryClerk>([]));

    public static IArtificer CreateArtificer(IEnumerable<IStagingProvider> staging) =>
        new Artificer(new StagingService(staging), new Team<IWorkbenchWorker>([]));
}
