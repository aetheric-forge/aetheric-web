using System.Security.Cryptography;
using System.Text;
using Aetheric.Provisioning.Definitions;
using Aetheric.Provisioning.Engine;
using Xunit;

namespace AethericForge.Web.Tests;

public class ParallelYouDefinitionTests
{
    [Fact]
    public void Definition_and_example_bindings_use_the_existing_provisioning_contract()
    {
        SourceDocument Document(string name)
        {
            var content = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "institution", name));
            return new SourceDocument(content, new SourceProvenance("https://github.com/aetheric-forge/aetheric-web",
                new string('a', 40), "parallel-you/institution/" + name,
                Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)))));
        }
        var result = new InstitutionYamlReader().Read(new SourceBundle(
            Document("parallel-you.yaml"), Document("parallel-you.bindings.yaml")));
        Assert.Empty(result.Issues);
        var institution = Assert.IsType<LoadedInstitution>(result.Institution);
        Assert.Equal("parallel-you", institution.Requirements.Id);
        Assert.Equal("1.0.0", institution.Requirements.Version);
        Assert.Equal(new[] { "IRegistrar", "IWorkbench" }, institution.Requirements.ParentContracts);
        var library = Assert.Single(institution.Requirements.Resources);
        Assert.Equal("owned", library.Ownership);
        Assert.Equal("knowledge", library.Category);
        Assert.Equal("mongodb", institution.Bindings.Resources["library"].Provider);
        Assert.Empty(institution.Bindings.Resources["library"].Secrets);
        Assert.Equal("campus.workbench", institution.Bindings.ParentSources["IWorkbench"]);
    }
}
