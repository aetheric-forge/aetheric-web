using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;

namespace ParallelYou.Abstractions.Person;

public interface IPersonService
{
    Task<IPerson> GetOrCreatePersonAsync(
        IIdentitySubject identity,
        CancellationToken cancellationToken = default);
}
