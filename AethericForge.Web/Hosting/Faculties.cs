using AethericForge.Runtime.Abstractions.Interfaces.Faculty.Services;
using AethericForge.Runtime.Institutions.Faculty;
using AethericForge.Runtime.Models.Institutions;

namespace AethericForge.Web.Hosting;

// IInstitution.Register<T>/Resolve<T> key on the exact type, so a Campus that needs several
// Faculties (Architecture, Design, ...) needs a distinct interface per one - see IFaculty's doc
// comment in the Runtime. Faculty itself is sealed, so each division gets a tiny concrete type of
// its own rather than subclassing it; for now these are pure org/nav groupings with no children.

public interface IArchitectureFaculty : IFaculty;
public interface IDesignFaculty : IFaculty;
public interface IEngineeringFaculty : IFaculty;
public interface IManufacturingFaculty : IFaculty;
public interface IOperationsFaculty : IFaculty;

public sealed class ArchitectureFaculty(IFacultyContext context, IDean dean)
    : InstitutionBase(context), IArchitectureFaculty
{
    public new IFacultyContext Context => (IFacultyContext)base.Context;
    public IDean Dean { get; } = dean ?? throw new ArgumentNullException(nameof(dean));
}

public sealed class DesignFaculty(IFacultyContext context, IDean dean)
    : InstitutionBase(context), IDesignFaculty
{
    public new IFacultyContext Context => (IFacultyContext)base.Context;
    public IDean Dean { get; } = dean ?? throw new ArgumentNullException(nameof(dean));
}

public sealed class EngineeringFaculty(IFacultyContext context, IDean dean)
    : InstitutionBase(context), IEngineeringFaculty
{
    public new IFacultyContext Context => (IFacultyContext)base.Context;
    public IDean Dean { get; } = dean ?? throw new ArgumentNullException(nameof(dean));
}

public sealed class ManufacturingFaculty(IFacultyContext context, IDean dean)
    : InstitutionBase(context), IManufacturingFaculty
{
    public new IFacultyContext Context => (IFacultyContext)base.Context;
    public IDean Dean { get; } = dean ?? throw new ArgumentNullException(nameof(dean));
}

public sealed class OperationsFaculty(IFacultyContext context, IDean dean)
    : InstitutionBase(context), IOperationsFaculty
{
    public new IFacultyContext Context => (IFacultyContext)base.Context;
    public IDean Dean { get; } = dean ?? throw new ArgumentNullException(nameof(dean));
}
