using AethericForge.Web.Components.Layout;
using AethericForge.Web.Hosting;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AethericForge.Web.Tests;

public class NavMenuTests
{
    private static async Task<string> Render(ShellNavigation shell, bool administrator = false, bool viewAsUser = false)
    {
        var services = new ServiceCollection().AddSingleton(shell).BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);
        return await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<NavMenu>(ParameterView.FromDictionary(new Dictionary<string, object?>
            { [nameof(NavMenu.IsAdministrator)] = administrator, [nameof(NavMenu.ViewAsUser)] = viewAsUser }))).ToHtmlString());
    }

    [Fact]
    public async Task Public_site_links_only_to_loaded_modules()
    {
        var html = await Render(new ShellNavigation(false, new HashSet<string> { "parallel-you" }));
        Assert.Contains("href=\"/parallel-you\"", html);
        Assert.DoesNotContain("href=\"/library\"", html);
        Assert.DoesNotContain("href=\"/decisions\"", html);
        Assert.DoesNotContain("href=\"/talent\"", html);
        Assert.DoesNotContain("href=\"/campus/apply\"", html);
    }

    [Fact]
    public async Task Full_campus_links_to_its_institutions_and_modules()
    {
        var html = await Render(new ShellNavigation(true, new HashSet<string> { "library", "archive", "decisions" }), administrator: true);
        Assert.Contains("href=\"/library\"", html);
        Assert.Contains("href=\"/decisions\"", html);
        Assert.Contains("href=\"/talent\"", html);
        Assert.Contains("href=\"/operations\"", html);
        Assert.Contains("href=\"/campus/apply\"", html);
        Assert.DoesNotContain("href=\"/parallel-you\"", html);
    }
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Management_links_are_hidden_from_users_and_view_as_user(bool administrator, bool viewAsUser)
    {
        var html = await Render(new ShellNavigation(true, new HashSet<string>()), administrator, viewAsUser);
        Assert.DoesNotContain("href=\"/talent\"", html);
        Assert.DoesNotContain("href=\"/operations\"", html);
    }
}
