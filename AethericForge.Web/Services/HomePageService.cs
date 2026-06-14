namespace AethericForge.Web.Services;

using AethericForge.Web.Models;
using AethericForge.Web.Models.Home;

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
                    Title = "Building Systems. Sharing Knowledge. Growing Together.",

                    Subtitle =
                        "Aetheric Forge is an open initiative dedicated to the creation of software, infrastructure, educational resources, and communities of practice. Through real-world projects, open documentation, and collaborative learning, we help transform curiosity into capability.",

                    PrimaryAction = new HeroActionModel
                    {
                        Text = "Explore Projects",
                        Href = "/projects"
                    },

                    SecondaryAction = new HeroActionModel
                    {
                        Text = "Browse Documentation",
                        Href = "/documentation"
                        }
                    },

                    Mission = new MissionSectionModel
                    {
                        Header = new SectionHeaderModel
                        {
                            Title = "Our Mission",
                            Subtitle = "Creating the resources, knowledge, and communities that enable people to build exceptional systems."
                        },

                        Description =
                            "Aetheric Forge exists to lower the barriers to learning and creating. " +
                            "We develop open-source projects, publish practical educational material, " +
                            "and cultivate communities where students and professionals can learn by building together.",

                        Pillars =
                        [
                            new MissionPillarModel
                            {
                                Title = "Build Open Infrastructure",
                                Description =
                                    "Create high-quality software, platform designs, and reference architectures " +
                                    "that anyone can study, use, and improve."
                            },

                            new MissionPillarModel
                            {
                                Title = "Share Knowledge",
                                Description =
                                    "Transform experience into documentation, tutorials, design guides, " +
                                    "and educational resources that help others grow."
                            },

                            new MissionPillarModel
                            {
                                Title = "Grow Communities",
                                Description =
                                    "Bring students, mentors, and practitioners together through collaboration, " +
                                "discussion, and real-world projects."
                        }
                    ]
                },

                Projects = new ProjectsSectionModel
                {
                    Header = new SectionHeaderModel
                    {
                        Title = "Projects",
                        Subtitle =
                            "Open-source initiatives focused on education, infrastructure, and practical engineering."
                    },

                    Projects =
                    [
                        new ProjectCardModel
                        {
                            Title = "Infrastructure Designs",
                            Description =
                                "Reference architectures, deployment patterns, and infrastructure guidance developed through real-world experience.",
                            Url = "/projects"
                        },

                        new ProjectCardModel
                        {
                            Title = "Educational Resources",
                            Description =
                                "Documentation, tutorials, and learning material created to help students and practitioners build capability.",
                            Url = "/projects"
                        },

                        new ProjectCardModel
                        {
                            Title = "Community Initiatives",
                            Description =
                                "Collaborative projects that bring students, mentors, and professionals together to learn by building.",
                            Url = "/projects"
                        }
                    ]
                },

                CommunityHeader = new SectionHeaderModel
                {
                    Title = "Community",
                    Subtitle = "Students, mentors, and professionals working together to learn and build."
                }
            }
        );
    }
}
