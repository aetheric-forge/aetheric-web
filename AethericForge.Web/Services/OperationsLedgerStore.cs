using System.Text.Json;
using AethericForge.Runtime.Abstractions.Interfaces.Staging.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Workbench.Services;
using AethericForge.Runtime.Models.Staging;
using AethericForge.Runtime.Services.Workbench;

namespace AethericForge.Web.Services;

/// <summary>Operations catalogs survive web process restarts in the owner's existing Workbench stage.</summary>
public sealed class OperationsLedgerStore(IStagingService staging, string stage) : IWorkbenchService
{
    private readonly WorkbenchService notifications = new();
    private static string Key(object key) => key is string value && !string.IsNullOrWhiteSpace(value)
        ? "operations/ledger-v1/" + Uri.EscapeDataString(value)
        : throw new ArgumentException("An Operations catalog key is required.", nameof(key));

    public async Task PutAsync<T>(object key, T work, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(work);
        using var content = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(work));
        await staging.PutAsync(stage, Key(key), content, ct: ct);
        await notifications.PutAsync(key, work, ct);
    }

    public async Task<T?> GetAsync<T>(object key, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        var reference = new StagingReference(stage, Key(key));
        if (!await staging.ExistsAsync(reference, ct)) return default;
        await using var content = await staging.OpenReadAsync(reference, ct);
        return await JsonSerializer.DeserializeAsync<T>(content, cancellationToken: ct);
    }

    public IDisposable Subscribe<T>(Func<T, CancellationToken, Task> receiver) => notifications.Subscribe(receiver);
}
