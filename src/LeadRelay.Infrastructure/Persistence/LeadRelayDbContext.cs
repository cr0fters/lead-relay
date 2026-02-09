using System.Text.Json;
using LeadRelay.Domain.Leads;
using LeadRelay.Domain.Sites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LeadRelay.Infrastructure.Persistence;

public sealed class LeadRelayDbContext(DbContextOptions<LeadRelayDbContext> options) : DbContext(options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public DbSet<SiteRecord> Sites => Set<SiteRecord>();
    public DbSet<LeadRecord> Leads => Set<LeadRecord>();
    public DbSet<CustomerRecord> Customers => Set<CustomerRecord>();
    public DbSet<ProjectRecord> Projects => Set<ProjectRecord>();
    public DbSet<ConversationStateRecord> ConversationStates => Set<ConversationStateRecord>();
    public DbSet<OwnerAccountRecord> OwnerAccounts => Set<OwnerAccountRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var stringListConverter = BuildJsonConverter<List<string>>();
        var conversationFieldListConverter = BuildJsonConverter<List<ConversationField>>();
        var leadConversationConverter = BuildJsonConverter<List<LeadConversationTurn>>();
        var dictionaryConverter = BuildJsonConverter<Dictionary<string, string>>();
        var conversationTurnConverter = BuildJsonConverter<List<ConversationTurnRecord>>();

        modelBuilder.Entity<SiteRecord>(entity =>
        {
            entity.ToTable("Sites");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("Id");
            entity.Property(x => x.Name).HasColumnName("Name").IsRequired();
            entity.Property(x => x.BusinessSummary).HasColumnName("BusinessSummary");
            entity.Property(x => x.IntroMessage).HasColumnName("IntroMessage");
            entity.Property(x => x.OwnerEmail).HasColumnName("OwnerEmail").IsRequired();
            entity.Property(x => x.WhatsAppNumber).HasColumnName("WhatsAppNumber").IsRequired();

            entity.Property(x => x.AllowedDomains)
                .HasColumnName("AllowedDomainsJson")
                .HasConversion(stringListConverter)
                .Metadata.SetValueComparer(BuildJsonComparer<List<string>>());

            entity.Property(x => x.Fields)
                .HasColumnName("FieldsJson")
                .HasConversion(conversationFieldListConverter)
                .Metadata.SetValueComparer(BuildJsonComparer<List<ConversationField>>());

            entity.Property(x => x.OptionalFields)
                .HasColumnName("OptionalFieldsJson")
                .HasConversion(conversationFieldListConverter)
                .Metadata.SetValueComparer(BuildJsonComparer<List<ConversationField>>());
        });

        modelBuilder.Entity<LeadRecord>(entity =>
        {
            entity.ToTable("Leads");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("Id");
            entity.Property(x => x.SiteId).HasColumnName("SiteId").HasMaxLength(255).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("CreatedAtUtc");
            entity.Property(x => x.CustomerId).HasColumnName("CustomerId").IsRequired();
            entity.Property(x => x.ProjectId).HasColumnName("ProjectId").IsRequired();
            entity.Property(x => x.Channel).HasColumnName("Channel").HasMaxLength(32).IsRequired();
            entity.Property(x => x.Status).HasColumnName("Status").HasMaxLength(32).IsRequired();

            entity.Property(x => x.Utm)
                .HasColumnName("UtmJson")
                .HasConversion(dictionaryConverter)
                .Metadata.SetValueComparer(BuildJsonComparer<Dictionary<string, string>>());

            entity.Property(x => x.Conversation)
                .HasColumnName("ConversationJson")
                .HasConversion(leadConversationConverter)
                .Metadata.SetValueComparer(BuildJsonComparer<List<LeadConversationTurn>>());

            entity.HasIndex(x => new { x.SiteId, x.CustomerId });
            entity.HasIndex(x => new { x.SiteId, x.ProjectId });
            entity.HasIndex(x => new { x.SiteId, x.Id }).IsUnique();

            entity.HasOne<CustomerRecord>()
                .WithMany()
                .HasForeignKey(x => new { x.SiteId, x.CustomerId })
                .HasPrincipalKey(x => new { x.SiteId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ProjectRecord>()
                .WithMany()
                .HasForeignKey(x => new { x.SiteId, x.ProjectId })
                .HasPrincipalKey(x => new { x.SiteId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<SiteRecord>()
                .WithMany()
                .HasForeignKey(x => x.SiteId)
                .HasPrincipalKey(x => x.Id)
                .OnDelete(DeleteBehavior.Restrict);

        });

        modelBuilder.Entity<CustomerRecord>(entity =>
        {
            entity.ToTable("Customers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("Id");
            entity.Property(x => x.SiteId).HasColumnName("SiteId").HasMaxLength(255).IsRequired();
            entity.Property(x => x.Name).HasColumnName("Name");
            entity.Property(x => x.Email).HasColumnName("Email").HasMaxLength(255);
            entity.Property(x => x.Phone).HasColumnName("Phone").HasMaxLength(64);
            entity.Property(x => x.ExternalContactId).HasColumnName("ExternalContactId").HasMaxLength(64);
            entity.Property(x => x.CreatedAtUtc).HasColumnName("CreatedAtUtc");
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("UpdatedAtUtc");

            entity.HasIndex(x => new { x.SiteId, x.ExternalContactId });
            entity.HasIndex(x => new { x.SiteId, x.Phone });
            entity.HasIndex(x => new { x.SiteId, x.Email });
            entity.HasIndex(x => new { x.SiteId, x.Id }).IsUnique();

            entity.HasOne<SiteRecord>()
                .WithMany()
                .HasForeignKey(x => x.SiteId)
                .HasPrincipalKey(x => x.Id)
                .OnDelete(DeleteBehavior.Restrict);

        });

        modelBuilder.Entity<ProjectRecord>(entity =>
        {
            entity.ToTable("Projects");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("Id");
            entity.Property(x => x.SiteId).HasColumnName("SiteId").HasMaxLength(255).IsRequired();
            entity.Property(x => x.CustomerId).HasColumnName("CustomerId").IsRequired();
            entity.Property(x => x.Name).HasColumnName("Name").IsRequired();
            entity.Property(x => x.Summary).HasColumnName("Summary");
            entity.Property(x => x.Status).HasColumnName("Status").HasMaxLength(32).IsRequired();
            entity.Property(x => x.Fields)
                .HasColumnName("FieldsJson")
                .HasConversion(dictionaryConverter)
                .Metadata.SetValueComparer(BuildJsonComparer<Dictionary<string, string>>());
            entity.Property(x => x.CreatedAtUtc).HasColumnName("CreatedAtUtc");
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("UpdatedAtUtc");

            entity.HasIndex(x => new { x.SiteId, x.CustomerId });
            entity.HasIndex(x => new { x.SiteId, x.Id }).IsUnique();

            entity.HasOne<CustomerRecord>()
                .WithMany()
                .HasForeignKey(x => new { x.SiteId, x.CustomerId })
                .HasPrincipalKey(x => new { x.SiteId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<SiteRecord>()
                .WithMany()
                .HasForeignKey(x => x.SiteId)
                .HasPrincipalKey(x => x.Id)
                .OnDelete(DeleteBehavior.Restrict);

        });

        modelBuilder.Entity<ConversationStateRecord>(entity =>
        {
            entity.ToTable("ConversationStates");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("Id");
            entity.Property(x => x.SiteId).HasColumnName("SiteId").HasMaxLength(255).IsRequired();
            entity.Property(x => x.WaId).HasColumnName("WaId").IsRequired();
            entity.Property(x => x.StepIndex).HasColumnName("StepIndex");
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("UpdatedAtUtc");
            entity.Property(x => x.SessionStartedAtUtc).HasColumnName("SessionStartedAtUtc");
            entity.Property(x => x.LastActivityAtUtc).HasColumnName("LastActivityAtUtc");
            entity.Property(x => x.IsPaused).HasColumnName("IsPaused");
            entity.Property(x => x.ContactName).HasColumnName("ContactName");
            entity.Property(x => x.SystemPromptOverride).HasColumnName("SystemPromptOverride");
            entity.Property(x => x.LeadId).HasColumnName("LeadId");
            entity.Property(x => x.LeadCreatedAtUtc).HasColumnName("LeadCreatedAtUtc");

            entity.Property(x => x.Collected)
                .HasColumnName("CollectedJson")
                .HasConversion(dictionaryConverter)
                .Metadata.SetValueComparer(BuildJsonComparer<Dictionary<string, string>>());

            entity.Property(x => x.History)
                .HasColumnName("HistoryJson")
                .HasConversion(conversationTurnConverter)
                .Metadata.SetValueComparer(BuildJsonComparer<List<ConversationTurnRecord>>());

            entity.HasIndex(x => new { x.SiteId, x.WaId }).IsUnique();
            entity.HasIndex(x => new { x.SiteId, x.LeadId });

            entity.HasOne<SiteRecord>()
                .WithMany()
                .HasForeignKey(x => x.SiteId)
                .HasPrincipalKey(x => x.Id)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<LeadRecord>()
                .WithMany()
                .HasForeignKey(x => new { x.SiteId, x.LeadId })
                .HasPrincipalKey(x => new { x.SiteId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OwnerAccountRecord>(entity =>
        {
            entity.ToTable("OwnerAccounts");
            entity.HasKey(x => x.SiteId);
            entity.Property(x => x.SiteId).HasColumnName("SiteId").HasMaxLength(255);
            entity.Property(x => x.PasswordHash).HasColumnName("PasswordHash");
            entity.Property(x => x.ResetTokenHash).HasColumnName("ResetTokenHash");
            entity.Property(x => x.ResetTokenExpiresAtUtc).HasColumnName("ResetTokenExpiresAtUtc");
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("UpdatedAtUtc");

            entity.HasOne<SiteRecord>()
                .WithMany()
                .HasForeignKey(x => x.SiteId)
                .HasPrincipalKey(x => x.Id)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static ValueConverter<TValue, string> BuildJsonConverter<TValue>()
        where TValue : class, new()
    {
        return new ValueConverter<TValue, string>(
            value => JsonSerializer.Serialize(value ?? new TValue(), JsonOptions),
            json => string.IsNullOrWhiteSpace(json)
                ? new TValue()
                : JsonSerializer.Deserialize<TValue>(json, JsonOptions) ?? new TValue());
    }

    private static ValueComparer<TValue> BuildJsonComparer<TValue>()
        where TValue : class, new()
    {
        return new ValueComparer<TValue>(
            (left, right) => JsonSerializer.Serialize(left ?? new TValue(), JsonOptions) ==
                             JsonSerializer.Serialize(right ?? new TValue(), JsonOptions),
            value => JsonSerializer.Serialize(value ?? new TValue(), JsonOptions).GetHashCode(),
            value => JsonSerializer.Deserialize<TValue>(
                         JsonSerializer.Serialize(value ?? new TValue(), JsonOptions),
                         JsonOptions) ?? new TValue());
    }
}
