namespace Andje.Chat.Api.Diagnostics;

public sealed class DiagnosticsOptions
{
    public bool Enabled { get; set; }
    public bool IncludeCounts { get; set; } = true;
}
