namespace AethericForge.Web.Services;

using AethericForge.Web.Models;
using AethericForge.Web.Models.Home;
using AethericForge.Web.Models.Shared;

public interface IHomePageService
{
    Task<HomePageModel> GetAsync();
}

public sealed class HomePageService : IHomePageService
{
    public Task<HomePageModel> GetAsync()
    {
        return Task.FromResult(
            new HomePageModel
            {
                Hero = new HeroModel
                {
                    Eyebrow = "A Workshop for Builders, Thinkers, and Innovators",

                    Title = "Building Systems. Sharing Knowledge. Growing Together.",

                    Subtitle =
                        "Aetheric Forge is an open initiative dedicated to the creation of software, infrastructure, educational resources, and communities of practice. Through real-world projects, open documentation, and collaborative learning, we help transform curiosity into capability.",

                    PrimaryAction = new ActionLinkModel
                    {
                        Text = "Explore Projects",
                        Url = "/projects"
                    },

                    SecondaryAction = new ActionLinkModel
                    {
                        Text = "Browse Documentation",
                        Url = "/documentation"
                    }
                },
            }
        );
    }
}
