using AethericForge.Runtime.Abstractions.Interfaces.Maintenance.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Maintenance.Services;
using AethericForge.Runtime.Institutions.Campus;
using AethericForge.Runtime.Institutions.Maintenance;
using AethericForge.Web.Hosting;

namespace AethericForge.Web.Maintenance;

/// <summary>
/// Periodically posts one command per registered <see cref="IMaintenanceWorker"/>'s job to the
/// Maintenance institution's Caretaker, collects it straight back, runs the matching worker, and
/// records the outcome. The Runtime only supplies the custody/outcome mechanism (Caretaker); this is
/// the app-level policy - what jobs exist, and how often they run.
/// </summary>
public sealed class MaintenanceDispatchService(
    ICampus campus,
    IEnumerable<IMaintenanceWorker> workers,
    TimeProvider timeProvider,
    ILogger<MaintenanceDispatchService> logger) : BackgroundService
{
    private const string Domain = "aetheric-web";
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workersByJob = workers.ToDictionary(worker => worker.Job, StringComparer.Ordinal);
        var caretaker = campus.Resolve<IOperationsFaculty>().Resolve<IMaintenance>().Caretaker;

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var (job, worker) in workersByJob)
            {
                await RunOnceAsync(caretaker, job, worker, stoppingToken).ConfigureAwait(false);
            }

            try
            {
                await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunOnceAsync(
        ICaretaker caretaker,
        string job,
        IMaintenanceWorker worker,
        CancellationToken ct)
    {
        try
        {
            var command = new MaintenanceCommand(
                Guid.NewGuid(),
                Domain,
                job,
                timeProvider.GetUtcNow(),
                Source: nameof(MaintenanceDispatchService));
            await caretaker.PostAsync(Domain, command, ct).ConfigureAwait(false);

            var collected = await caretaker.CollectNextAsync(Domain, job, ct).ConfigureAwait(false);
            if (collected is null)
            {
                return;
            }

            var outcome = await worker.RunAsync(collected, ct).ConfigureAwait(false);
            await caretaker.RecordOutcomeAsync(Domain, outcome, ct).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "A maintenance dispatch iteration failed for job {Job}.", job);
        }
    }
}
