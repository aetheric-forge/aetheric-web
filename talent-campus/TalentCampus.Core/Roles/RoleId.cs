namespace TalentCampus.Core.Roles;

public sealed record RoleId(Guid Value)
{
    public static RoleId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
