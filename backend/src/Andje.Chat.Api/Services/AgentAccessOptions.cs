namespace Andje.Chat.Api.Services;

public sealed class AgentAccessOptions
{
    public bool Enabled { get; set; } = true;
    public string DevelopmentAccessCode { get; set; } = string.Empty;
    public int SessionMinutes { get; set; } = 120;
}
