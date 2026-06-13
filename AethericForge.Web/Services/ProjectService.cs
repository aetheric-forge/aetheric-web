public sealed class ProjectService
{
    public IReadOnlyList<ProjectModel> GetProjects()
    {
        return
        [
            new()
            {
                Name = "Aetheric Runtime",
                Description = "Distributed application runtime."
            }
        ];
    }
}