using AethericForge.Web.Components.Pages;
using AethericForge.Web.Marketing;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace AethericForge.Web.Tests;
public sealed class CampaignProjectsRenderingTests
{
    [Fact]
    public async Task PageRendersCampaignSafelyAndPreservesBaselineWhenEnded()
    {
        var catalog = new TestCatalog { Content = new("Campaign <script>bad</script>","Published summary","Body",new("Contact","https://example.com"),null,null,new("Title","Description",null,null,null,null)) };
        using var services = new ServiceCollection().AddLogging().AddSingleton<NavigationManager>(new TestNavigation())
            .AddSingleton<ICampaignCatalog>(catalog).BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        var published = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<Projects>()).ToHtmlString());
        Assert.Contains("Campaign &lt;script&gt;bad&lt;/script&gt;",published);
        Assert.Contains("Published summary",published);
        Assert.Contains("Contact",published);
        Assert.DoesNotContain("<script>bad</script>",published);
        catalog.Content=null;
        var ended = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<Projects>()).ToHtmlString());
        Assert.Contains("Ideas become useful",ended);
        Assert.DoesNotContain("Published summary",ended);
    }
    private sealed class TestCatalog : ICampaignCatalog
    {
        public CampaignPageContent? Content { get; set; }
        public Task<CampaignPageContent?> GetActiveAsync(string path,CancellationToken cancellationToken=default) => Task.FromResult(Content);
    }
    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("http://localhost/","http://localhost/projects");
        protected override void NavigateToCore(string uri,bool forceLoad) { }
    }
}
