namespace Andje.Chat.Api.Diagnostics;

public interface IDiagnosticsService
{
    Task<DiagnosticsStatus> GetStatusAsync(
        string environmentName,
        bool includeCounts,
        CancellationToken cancellationToken = default);
}
