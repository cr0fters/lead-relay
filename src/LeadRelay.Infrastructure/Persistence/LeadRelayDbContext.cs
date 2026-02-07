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
            entity.Property(x => x.SiteId).HasColumnName("SiteId").IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("CreatedAtUtc");
            entity.Property(x => x.Name).HasColumnName("Name");
            entity.Property(x => x.Email).HasColumnName("Email");
            entity.Property(x => x.Phone).HasColumnName("Phone");
            entity.Property(x => x.Intent).HasColumnName("Intent");
            entity.Property(x => x.Notes).HasColumnName("Notes");
            entity.Property(x => x.PageUrl).HasColumnName("PageUrl");
            entity.Property(x => x.Referrer).HasColumnName("Referrer");

            entity.Property(x => x.Utm)
                .HasColumnName("UtmJson")
                .HasConversion(dictionaryConverter)
                .Metadata.SetValueComparer(BuildJsonComparer<Dictionary<string, string>>());

            entity.Property(x => x.Fields)
                .HasColumnName("FieldsJson")
                .HasConversion(dictionaryConverter)
                .Metadata.SetValueComparer(BuildJsonComparer<Dictionary<string, string>>());

            entity.Property(x => x.Conversation)
                .HasColumnName("ConversationJson")
                .HasConversion(leadConversationConverter)
                .Metadata.SetValueComparer(BuildJsonComparer<List<LeadConversationTurn>>());
        });

        modelBuilder.Entity<ConversationStateRecord>(entity =>
        {
            entity.ToTable("ConversationStates");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("Id");
            entity.Property(x => x.SiteId).HasColumnName("SiteId").IsRequired();
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
        });

        modelBuilder.Entity<OwnerAccountRecord>(entity =>
        {
            entity.ToTable("OwnerAccounts");
            entity.HasKey(x => x.SiteId);
            entity.Property(x => x.SiteId).HasColumnName("SiteId");
            entity.Property(x => x.PasswordHash).HasColumnName("PasswordHash");
            entity.Property(x => x.ResetTokenHash).HasColumnName("ResetTokenHash");
            entity.Property(x => x.ResetTokenExpiresAtUtc).HasColumnName("ResetTokenExpiresAtUtc");
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("UpdatedAtUtc");
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
