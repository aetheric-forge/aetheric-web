using System.Reflection;
using AethericForge.Web.Components.Pages;
using AethericForge.Web.Hosting;
using TalentHome = AethericForge.Web.Talent.Pages.Home;
using TalentRoles = AethericForge.Web.Talent.Pages.Roles;
using TalentPersonas = AethericForge.Web.Talent.Pages.Personas;
using TalentRecruitment = AethericForge.Web.Talent.Pages.Recruitment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using TalentCampus.Core.Personas;
using TalentCampus.Core.Recruitment;
using TalentCampus.Core.Roles;
using Xunit;

namespace AethericForge.Web.Tests;

public sealed class InstitutionWorkspacePageTests
{
    [Theory]
    [InlineData("/talent")]
    [InlineData("/talent/roles")]
    [InlineData("/talent/personas")]
    [InlineData("/talent/recruitment")]
    [InlineData("/operations")]
    [InlineData("/operations/issue-reports")]
    [InlineData("/operations/security")]
    [InlineData("/campus/admin/issue-reports")]
    [InlineData("/campus/admin/security")]
    public void Management_routes_have_the_administrator_policy(string path)
    {
        var page = Assert.Single(typeof(TalentHome).Assembly.GetTypes()
            .Where(type => type.GetCustomAttributes<RouteAttribute>().Any(route => route.Template == path)));
        Assert.Contains(page.GetCustomAttributes<AuthorizeAttribute>(),
            attribute => attribute.Policy == ForgeAuthorizationExtensions.AdministratorPolicy);
    }

    [Fact]
    public async Task Talent_screens_render_existing_provider_data_and_use_host_routes()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<ITalentSteward, Roles>();
        services.AddSingleton<IPersonaDirectory, Personas>();
        services.AddSingleton<IRecruitmentOffice, Recruitment>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new TestRenderer(provider, provider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>());
        async Task<string> Render<T>() where T : IComponent => await renderer.Dispatcher.InvokeAsync(async () =>
            await RenderPage<T>(renderer));
        var roles = await Render<TalentRoles>();
        Assert.Contains("Campus custodian", roles);
        Assert.Contains("Keep services available", roles);
        Assert.Contains("/talent", roles);
        var personas = await Render<TalentPersonas>();
        Assert.Contains("Research assistant", personas);
        Assert.Contains("Never change production", personas);
        var recruitment = await Render<TalentRecruitment>();
        Assert.Contains("Existing candidate", recruitment);
        Assert.Contains("Administration Decisions", recruitment);
        Assert.Contains("prepared considerations have not been sent or decided", recruitment);
        Assert.DoesNotContain("href=\"roles\"", recruitment);
    }

    private sealed class TestRenderer(IServiceProvider services, Microsoft.Extensions.Logging.ILoggerFactory logger)
        : Microsoft.AspNetCore.Components.HtmlRendering.Infrastructure.StaticHtmlRenderer(services, logger)
    {
        protected override IComponent ResolveComponentForRenderMode(Type componentType, int? parentComponentId,
            IComponentActivator activator, IComponentRenderMode renderMode) => activator.CreateInstance(componentType);
    }
    private static async Task<string> RenderPage<T>(TestRenderer renderer) where T : IComponent
    {
        var component = renderer.BeginRenderingComponent(typeof(T), ParameterView.Empty);
        await component.QuiescenceTask;
        return component.ToHtmlString();
    }

    private sealed class Roles : ITalentSteward
    {
        public IReadOnlyCollection<RoleDefinition> ListRoles() =>
            [new(RoleId.New(), "Campus custodian", "Keep services available", ["Operations"], "Review incidents", "Resolve issues")];
        public RoleDefinition DefineRole(string name, string purpose, IEnumerable<string> requiredCapabilities, string responsibilities = "", string successCriteria = "") => throw new NotSupportedException();
    }
    private sealed class Personas : IPersonaDirectory
    {
        public IReadOnlyCollection<PersonaDefinition> List() =>
            [new(Guid.NewGuid(), "Research assistant", "Research", "Careful", "Analysis", "Never change production")];
        public PersonaDefinition Create(string name, string purpose, string workingStyle, string strengths, string boundaries) => throw new NotSupportedException();
    }
    private sealed class Recruitment : IRecruitmentOffice
    {
        public IReadOnlyCollection<HumanCandidate> ListHumans() => [new(Guid.NewGuid(), "Existing candidate", "Operations")];
        public IReadOnlyCollection<DecisionsDestination> ListDestinations() => [new("administration-decisions", "Administration Decisions")];
        public IReadOnlyCollection<Consideration> ListConsiderations() => [];
        public HumanCandidate RegisterHuman(string name, string background) => throw new NotSupportedException();
        public Consideration Prepare(Guid roleId, ParticipantKind kind, Guid participantId, string evidence, string recommendation, string destinationId) => throw new NotSupportedException();
    }
}
