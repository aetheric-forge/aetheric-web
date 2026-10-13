using ParallelYou.Abstractions.Person;

namespace AethericForge.Web.Authentication;

public interface ICurrentParallelYouPersonAccessor
{
    Task<IPerson> GetPersonAsync(CancellationToken cancellationToken = default);
}

public sealed class CurrentParallelYouPersonAccessor(ICurrentIdentityAccessor identity, IPersonService people)
    : ICurrentParallelYouPersonAccessor
{
    public async Task<IPerson> GetPersonAsync(CancellationToken cancellationToken = default) =>
        await people.GetOrCreatePersonAsync(await identity.GetIdentityAsync(), cancellationToken);
}
