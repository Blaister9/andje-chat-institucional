using Andje.Chat.Api.Data;
using Andje.Chat.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Andje.Chat.Api.Diagnostics;

public sealed class DiagnosticsService(ChatDbContext db, TimeProvider timeProvider)
    : IDiagnosticsService
{
    public async Task<DiagnosticsStatus> GetStatusAsync(
        string environmentName,
        bool includeCounts,
        CancellationToken cancellationToken = default)
    {
        var databaseReachable = await db.Database.CanConnectAsync(cancellationToken);
        OperationalCounts? counts = null;

        if (databaseReachable && includeCounts)
        {
            var conversationsTotal = await db.Conversations.CountAsync(cancellationToken);
            var conversationsClosed = await db.Conversations
                .CountAsync(c => c.Status == ConversationStatus.Closed, cancellationToken);
            counts = new OperationalCounts(
                ConversationsTotal: conversationsTotal,
                ConversationsOpen: conversationsTotal - conversationsClosed,
                ConversationsClosed: conversationsClosed,
                MessagesTotal: await db.Messages.CountAsync(cancellationToken),
                AuditEventsTotal: await db.AuditEvents.CountAsync(cancellationToken));
        }

        return new DiagnosticsStatus(
            Status: databaseReachable ? "Healthy" : "Degraded",
            Environment: environmentName,
            Database: databaseReachable ? "Reachable" : "Unreachable",
            UtcNow: timeProvider.GetUtcNow(),
            Version: "dev",
            Counts: counts);
    }
}
