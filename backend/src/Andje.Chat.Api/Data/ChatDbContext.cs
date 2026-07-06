using Andje.Chat.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Andje.Chat.Api.Data;

public sealed class ChatDbContext(DbContextOptions<ChatDbContext> options) : DbContext(options)
{
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatMessage> Messages => Set<ChatMessage>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<CannedResponse> CannedResponses => Set<CannedResponse>();
    public DbSet<ConversationTag> ConversationTags => Set<ConversationTag>();
    public DbSet<ConversationTagAssignment> ConversationTagAssignments => Set<ConversationTagAssignment>();
    public DbSet<InternalNote> InternalNotes => Set<InternalNote>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var seedInstant = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

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

        modelBuilder.Entity<CannedResponse>(entity =>
        {
            entity.Property(r => r.Title).HasMaxLength(80);
            entity.Property(r => r.Body).HasMaxLength(2000);
            entity.HasIndex(r => new { r.IsActive, r.SortOrder });
            entity.HasData(
                new CannedResponse
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000110001"),
                    Title = "Saludo institucional",
                    Body = "Hola, con gusto te orientamos. Cuentanos en que tramite o consulta necesitas apoyo.",
                    IsActive = true,
                    SortOrder = 10,
                    CreatedAtUtc = seedInstant,
                    UpdatedAtUtc = seedInstant,
                },
                new CannedResponse
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000110002"),
                    Title = "Solicitud de datos generales",
                    Body = "Para orientarte mejor, por favor comparte el tipo de solicitud y la entidad relacionada. No envies datos sensibles por este canal demo.",
                    IsActive = true,
                    SortOrder = 20,
                    CreatedAtUtc = seedInstant,
                    UpdatedAtUtc = seedInstant,
                },
                new CannedResponse
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000110003"),
                    Title = "Cierre amable",
                    Body = "Gracias por comunicarte. Dejamos registrada la orientacion brindada en esta conversacion demo.",
                    IsActive = true,
                    SortOrder = 30,
                    CreatedAtUtc = seedInstant,
                    UpdatedAtUtc = seedInstant,
                });
        });

        modelBuilder.Entity<ConversationTag>(entity =>
        {
            entity.Property(t => t.Name).HasMaxLength(40);
            entity.Property(t => t.Color).HasMaxLength(20);
            entity.HasIndex(t => t.Name).IsUnique();
            entity.HasData(
                new ConversationTag
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000120001"),
                    Name = "Orientacion",
                    Color = "#10316b",
                    IsActive = true,
                    CreatedAtUtc = seedInstant,
                },
                new ConversationTag
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000120002"),
                    Name = "Urgente",
                    Color = "#ab091e",
                    IsActive = true,
                    CreatedAtUtc = seedInstant,
                },
                new ConversationTag
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000120003"),
                    Name = "Tramite",
                    Color = "#7c5e10",
                    IsActive = true,
                    CreatedAtUtc = seedInstant,
                },
                new ConversationTag
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000120004"),
                    Name = "Seguimiento",
                    Color = "#0f7b3f",
                    IsActive = true,
                    CreatedAtUtc = seedInstant,
                },
                new ConversationTag
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000120005"),
                    Name = "PQRS",
                    Color = "#52606d",
                    IsActive = true,
                    CreatedAtUtc = seedInstant,
                });
        });

        modelBuilder.Entity<ConversationTagAssignment>(entity =>
        {
            entity.HasKey(a => new { a.ConversationId, a.TagId });
            entity.HasOne(a => a.Conversation)
                  .WithMany(c => c.TagAssignments)
                  .HasForeignKey(a => a.ConversationId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(a => a.Tag)
                  .WithMany(t => t.Assignments)
                  .HasForeignKey(a => a.TagId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(a => a.TagId);
        });

        modelBuilder.Entity<InternalNote>(entity =>
        {
            entity.Property(n => n.Body).HasMaxLength(1000);
            entity.Property(n => n.AgentDisplayName).HasMaxLength(80);
            entity.HasOne(n => n.Conversation)
                  .WithMany(c => c.InternalNotes)
                  .HasForeignKey(n => n.ConversationId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(n => new { n.ConversationId, n.CreatedAtUtc });
        });
    }
}
