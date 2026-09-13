using TalentCampus.Core.Roles;

namespace TalentCampus.Core.Tests;

public sealed class RoleDefinitionTests
{
    [Fact]
    public void RequiresPurposeAndCapabilities()
    {
        Assert.Throws<ArgumentException>(() =>
            new RoleDefinition(RoleId.New(), "Architect", "", ["Systems thinking"]));
        Assert.Throws<ArgumentException>(() =>
            new RoleDefinition(RoleId.New(), "Architect", "Shape systems", []));
    }

    [Fact]
    public void NormalizesAndDeduplicatesCapabilities()
    {
        var role = new RoleDefinition(
            RoleId.New(),
            " Agent Evaluator ",
            " Establish trustworthy evidence. ",
            ["Safety evaluation", " reasoning ", "SAFETY EVALUATION"]);

        Assert.Equal("Agent Evaluator", role.Name);
        Assert.Equal("Establish trustworthy evidence.", role.Purpose);
        Assert.Equal(
            ["reasoning", "Safety evaluation"],
            role.RequiredCapabilities);
    }
}
