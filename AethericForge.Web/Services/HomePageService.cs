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
                        Label = "Explore Projects",
                        Url = "/projects"
                    },

                    SecondaryAction = new ActionLinkModel
                    {
                        Label = "Browse Documentation",
                        Url = "/documentation"
                    }
                },

                ProjectsSection = new ProjectsSectionModel
                {
                    Header = new SectionHeaderModel
                    {
                        Title = "Projects and Initiatives",

                        Subtitle =
                            "Our projects bring together software development, infrastructure design, education, and community engagement to create practical solutions and meaningful learning opportunities."
                    },

                    Projects =
                    [
                        new CardModel
                        {
                            Title = "Open Source Software",

                            Description =
                                "Applications, libraries, and tools designed to solve real-world problems while demonstrating modern engineering practices.",

                            Action = new ActionLinkModel
                            {
                                Label = "View Projects",
                                Url = "/projects/software"
                            }
                        },

                        new CardModel
                        {
                            Title = "Infrastructure & Architecture",

                            Description =
                                "Reference architectures, deployment patterns, and operational practices that help organizations build reliable and sustainable systems.",

                            Action = new ActionLinkModel
                            {
                                Label = "Explore Roadmaps",
                                Url = "/projects/infrastructure"
                            }
                        },

                        new CardModel
                        {
                            Title = "Educational Resources",

                            Description =
                                "Guides, workshops, videos, and learning materials created to help builders develop practical skills through real-world examples.",

                            Action = new ActionLinkModel
                            {
                                Label = "Browse Resources",
                                Url = "/documentation"
                            }
                        }
                    ]
                },
                MissionSection = new MissionSectionModel
                {
                    Header = new SectionHeaderModel
                    {
                        Title = "Our Mission",

                        Subtitle =
                            "Creating opportunities to learn, build, and grow through open collaboration."
                    },

                    Description =
                        "Aetheric Forge exists to foster practical learning, open knowledge, and collaborative problem-solving. Through software, infrastructure, education, and community engagement, we help individuals and organizations develop the skills, tools, and relationships needed to create lasting impact.",

                    Pillars =
                    [
                        new MissionPillarModel
                        {
                            Title = "Learn",

                            Description =
                                "Develop practical skills through real-world projects, mentorship, and shared experiences."
                        },

                        new MissionPillarModel
                        {
                            Title = "Build",

                            Description =
                                "Create software, infrastructure, and educational resources that provide meaningful value."
                        },

                        new MissionPillarModel
                        {
                            Title = "Share",

                            Description =
                                "Strengthen communities through open knowledge, collaboration, and continuous improvement."
                        }
                    ]
                },
                ResourcesSection = new ResourcesSectionModel
                {
                    Header = new SectionHeaderModel
                    {
                        Title = "Resources",

                        Subtitle =
                            "Explore articles, videos, workshops, and open-source repositories created to support learning, collaboration, and practical experimentation."
                    },

                    Cards =
                    [
                        new CardModel
                        {
                            Title = "Articles",

                            Description =
                                "In-depth explorations of software architecture, infrastructure design, organizational development, and practical engineering.",

                            Action = new ActionLinkModel
                            {
                                Label = "Read Articles",
                                Url = "/articles"
                            }
                        },

                        new CardModel
                        {
                            Title = "Videos",

                            Description =
                                "Presentations, demonstrations, workshops, and discussions that bring ideas to life through visual learning.",

                            Action = new ActionLinkModel
                            {
                                Label = "Watch Videos",
                                Url = "/videos"
                            }
                        },

                        new CardModel
                        {
                            Title = "Workshops",

                            Description =
                                "Hands-on learning experiences designed to help participants develop practical skills through guided projects and collaboration.",

                            Action = new ActionLinkModel
                            {
                                Label = "Explore Workshops",
                                Url = "/workshops"
                            }
                        },

                        new CardModel
                        {
                            Title = "GitHub Projects",

                            Description =
                                "Open-source repositories, reference implementations, and active development efforts from the Aetheric Forge community.",

                            Action = new ActionLinkModel
                            {
                                Label = "Browse Repositories",
                                Url = "/github"
                            }
                        }
                    ]
                },
                CallToActionSection = new CallToActionSectionModel
                {
                    Header = new SectionHeaderModel
                    {
                        Title = "Ready to Build With Us?"
                    },

                    Description =
                        "Whether you're looking to learn, contribute, collaborate, or simply follow along, we'd love to have you join the journey.",

                    PrimaryAction = new ActionLinkModel
                    {
                        Label = "Explore Projects",
                        Url = "/projects"
                    },

                    SecondaryAction = new ActionLinkModel
                    {
                        Label = "Browse Resources",
                        Url = "/resources"
                    }
                },
            }
        );
    }
}
