using Andje.Chat.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Andje.Chat.Api.Data;

public sealed class ChatDbContext(DbContextOptions<ChatDbContext> options) : DbContext(options)
{
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatMessage> Messages => Set<ChatMessage>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.Property(c => c.VisitorDisplayName).HasMaxLength(80);
            entity.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(c => c.CreatedAtUtc);
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.Property(m => m.SenderType).HasConversion<string>().HasMaxLength(20);
            entity.Property(m => m.Body).HasMaxLength(2000);
            entity.HasOne<Conversation>()
                  .WithMany(c => c.Messages)
                  .HasForeignKey(m => m.ConversationId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(m => new { m.ConversationId, m.CreatedAtUtc });
        });

        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.Property(a => a.EventType).HasMaxLength(100);
            entity.Property(a => a.ActorType).HasMaxLength(20);
            entity.Property(a => a.DataJson).HasColumnType("jsonb");
            entity.HasIndex(a => a.ConversationId);
            entity.HasIndex(a => a.CreatedAtUtc);
        });
    }
}
