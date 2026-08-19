using System.Text.Json;
using LeadRelay.Domain.Leads;
using LeadRelay.Domain.Projects;
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
    public DbSet<OwnerAccountRecord> OwnerAccounts => Set<OwnerAccountRecord>();
    public DbSet<WhatsAppConnectionRecord> WhatsAppConnections => Set<WhatsAppConnectionRecord>();
    public DbSet<WhatsAppMessageReceiptRecord> WhatsAppMessageReceipts => Set<WhatsAppMessageReceiptRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var stringListConverter = BuildJsonConverter<List<string>>();
        var conversationFieldListConverter = BuildJsonConverter<List<ConversationField>>();
        var leadConversationConverter = BuildJsonConverter<List<LeadConversationTurn>>();
        var projectStageChangeConverter = BuildJsonConverter<List<ProjectStageChange>>();
        var dictionaryConverter = BuildJsonConverter<Dictionary<string, string>>();

        modelBuilder.Entity<SiteRecord>(entity =>
        {
            entity.ToTable("Sites");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("Id");
            entity.Property(x => x.Name).HasColumnName("Name").IsRequired();
            entity.Property(x => x.BusinessSummary).HasColumnName("BusinessSummary");
            entity.Property(x => x.IntroMessage).HasColumnName("IntroMessage");
            entity.Property(x => x.OwnerEmail).HasColumnName("OwnerEmail").HasMaxLength(255).IsRequired();
            entity.Property(x => x.WhatsAppNumber).HasColumnName("WhatsAppNumber").HasMaxLength(64).IsRequired();
            entity.Property(x => x.WhatsAppPhoneNumberId).HasColumnName("WhatsAppPhoneNumberId").HasMaxLength(64);
            entity.HasIndex(x => x.OwnerEmail).IsUnique();
            entity.HasIndex(x => x.WhatsAppPhoneNumberId).IsUnique();

            entity.Property(x => x.AllowedDomains)
                .HasColumnName("AllowedDomainsJson")
                .HasConversion(stringListConverter)
                .Metadata.SetValueComparer(BuildJsonComparer<List<string>>());

            entity.Property(x => x.Fields)
                .HasColumnName("FieldsJson")
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
            entity.Property(x => x.OwnerViewedAtUtc).HasColumnName("OwnerViewedAtUtc");
            entity.Property(x => x.CustomerId).HasColumnName("CustomerId").IsRequired();
            entity.Property(x => x.ProjectId).HasColumnName("ProjectId").IsRequired();
            entity.Property(x => x.Channel).HasColumnName("Channel").HasMaxLength(32).IsRequired();
            entity.Property(x => x.IsTest).HasColumnName("IsTest").IsRequired();
            entity.Property(x => x.Status).HasColumnName("Status").HasMaxLength(32).IsRequired();
            entity.Property(x => x.IsBotPaused).HasColumnName("IsBotPaused").IsRequired();

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
            entity.HasIndex(x => new { x.SiteId, x.CreatedAtUtc, x.Id });
            entity.HasIndex(x => new { x.SiteId, x.OwnerViewedAtUtc });
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
            entity.Property(x => x.OwnerNotes).HasColumnName("OwnerNotes").HasMaxLength(4000);
            entity.Property(x => x.NextAction).HasColumnName("NextAction").HasMaxLength(500);
            entity.Property(x => x.NextActionAtUtc).HasColumnName("NextActionAtUtc");
            entity.Property(x => x.Status).HasColumnName("Status").HasMaxLength(32).IsRequired();
            entity.Property(x => x.StageChanges)
                .HasColumnName("StageChangesJson")
                .HasConversion(projectStageChangeConverter)
                .Metadata.SetValueComparer(BuildJsonComparer<List<ProjectStageChange>>());
            entity.Property(x => x.Fields)
                .HasColumnName("FieldsJson")
                .HasConversion(dictionaryConverter)
                .Metadata.SetValueComparer(BuildJsonComparer<Dictionary<string, string>>());
            entity.Property(x => x.CreatedAtUtc).HasColumnName("CreatedAtUtc");
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("UpdatedAtUtc");

            entity.HasIndex(x => new { x.SiteId, x.CustomerId });
            entity.HasIndex(x => new { x.SiteId, x.Status });
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

        modelBuilder.Entity<OwnerAccountRecord>(entity =>
        {
            entity.ToTable("OwnerAccounts");
            entity.HasKey(x => x.SiteId);
            entity.Property(x => x.SiteId).HasColumnName("SiteId").HasMaxLength(255);
            entity.Property(x => x.PasswordHash).HasColumnName("PasswordHash");
            entity.Property(x => x.ResetTokenHash).HasColumnName("ResetTokenHash");
            entity.Property(x => x.ResetTokenExpiresAtUtc).HasColumnName("ResetTokenExpiresAtUtc");
            entity.Property(x => x.EmailVerificationTokenHash).HasColumnName("EmailVerificationTokenHash").HasMaxLength(64).IsConcurrencyToken();
            entity.Property(x => x.EmailVerificationTokenExpiresAtUtc).HasColumnName("EmailVerificationTokenExpiresAtUtc");
            entity.Property(x => x.EmailVerificationSentAtUtc).HasColumnName("EmailVerificationSentAtUtc");
            entity.Property(x => x.EmailVerifiedAtUtc).HasColumnName("EmailVerifiedAtUtc");
            entity.Property(x => x.LegalDocumentsAcceptedAtUtc).HasColumnName("LegalDocumentsAcceptedAtUtc");
            entity.Property(x => x.TermsVersion).HasColumnName("TermsVersion").HasMaxLength(32);
            entity.Property(x => x.PrivacyPolicyVersion).HasColumnName("PrivacyPolicyVersion").HasMaxLength(32);
            entity.Property(x => x.SessionVersion).HasColumnName("SessionVersion").HasDefaultValue(1L).IsConcurrencyToken();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("UpdatedAtUtc");

            entity.HasOne<SiteRecord>()
                .WithMany()
                .HasForeignKey(x => x.SiteId)
                .HasPrincipalKey(x => x.Id)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WhatsAppConnectionRecord>(entity =>
        {
            entity.ToTable("WhatsAppConnections");
            entity.HasKey(x => x.SiteId);
            entity.Property(x => x.SiteId).HasColumnName("SiteId").HasMaxLength(255);
            entity.Property(x => x.WabaId).HasColumnName("WabaId").HasMaxLength(64).IsRequired();
            entity.Property(x => x.PhoneNumberId).HasColumnName("PhoneNumberId").HasMaxLength(64).IsRequired();
            entity.Property(x => x.DisplayPhoneNumber).HasColumnName("DisplayPhoneNumber").HasMaxLength(64).IsRequired();
            entity.Property(x => x.AccessTokenCiphertext).HasColumnName("AccessTokenCiphertext").IsRequired();
            entity.Property(x => x.Status).HasColumnName("Status").HasMaxLength(32).IsRequired();
            entity.Property(x => x.WebhookSubscribedAtUtc).HasColumnName("WebhookSubscribedAtUtc");
            entity.Property(x => x.LastValidatedAtUtc).HasColumnName("LastValidatedAtUtc");
            entity.Property(x => x.LastInboundAtUtc).HasColumnName("LastInboundAtUtc");
            entity.Property(x => x.LastOutboundTestAtUtc).HasColumnName("LastOutboundTestAtUtc");
            entity.Property(x => x.LastOutboundTestRecipient).HasColumnName("LastOutboundTestRecipient").HasMaxLength(20);
            entity.Property(x => x.LastError).HasColumnName("LastError").HasMaxLength(1000);
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("UpdatedAtUtc");
            entity.HasIndex(x => x.PhoneNumberId).IsUnique();

            entity.HasOne<SiteRecord>()
                .WithOne()
                .HasForeignKey<WhatsAppConnectionRecord>(x => x.SiteId)
                .HasPrincipalKey<SiteRecord>(x => x.Id)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WhatsAppMessageReceiptRecord>(entity =>
        {
            entity.ToTable("WhatsAppMessageReceipts");
            entity.HasKey(x => new { x.SiteId, x.MessageId });
            entity.Property(x => x.SiteId).HasColumnName("SiteId").HasMaxLength(255);
            entity.Property(x => x.MessageId).HasColumnName("MessageId").HasMaxLength(255);
            entity.Property(x => x.StartedAtUtc).HasColumnName("StartedAtUtc");
            entity.Property(x => x.SideEffectsStartedAtUtc).HasColumnName("SideEffectsStartedAtUtc");
            entity.Property(x => x.ProcessedAtUtc).HasColumnName("ProcessedAtUtc");
            entity.HasIndex(x => x.StartedAtUtc);
            entity.HasIndex(x => x.SideEffectsStartedAtUtc);
            entity.HasIndex(x => x.ProcessedAtUtc);

            entity.HasOne<SiteRecord>()
                .WithMany()
                .HasForeignKey(x => x.SiteId)
                .HasPrincipalKey(x => x.Id)
                .OnDelete(DeleteBehavior.Cascade);
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
