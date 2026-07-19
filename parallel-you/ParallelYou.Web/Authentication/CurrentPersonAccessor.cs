using ParallelYou.Abstractions.Person;

namespace ParallelYou.Web.Authentication;

public interface ICurrentPersonAccessor
{
    Task<IPerson> GetPersonAsync(
        CancellationToken cancellationToken = default);
}

public sealed class CurrentPersonAccessor(
    ICurrentIdentityAccessor currentIdentity,
    IPersonService personService)
    : ICurrentPersonAccessor
{
    public async Task<IPerson> GetPersonAsync(
        CancellationToken cancellationToken = default)
    {
        var identity = await currentIdentity.GetIdentityAsync();
        return await personService.GetOrCreatePersonAsync(
            identity,
            cancellationToken);
    }
}
