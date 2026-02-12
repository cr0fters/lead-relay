using System.Net;
using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Sites;
using LeadRelay.Web.WhatsApp;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class WhatsAppClientTenantRoutingTests
{
    [Test]
    public async Task send_uses_sender_specific_configuration_when_site_has_phone_number_id()
    {
        var handler = new RecordingHandler();
        var http = new HttpClient(handler);
        var options = Options.Create(new WhatsAppOptions
        {
            Senders = new Dictionary<string, WhatsAppSenderOptions>(StringComparer.Ordinal)
            {
                ["phone-1"] = new()
                {
                    AccessToken = "sender_token",
                    MessagesEndpoint = "https://graph.facebook.com/v20.0/phone-1/messages"
                }
            }
        });
        var sites = new FixedSiteRepository(new Site
        {
            Id = "site_a",
            Name = "Site A",
            OwnerEmail = "owner@example.com",
            WhatsAppNumber = "447000000000",
            WhatsAppPhoneNumberId = "phone-1"
        });
        var client = new WhatsAppClient(http, options, sites, NullLogger<WhatsAppClient>.Instance);

        var sent = await client.SendTextAsync("447111111111", "hello", "site_a", CancellationToken.None);

        Assert.That(sent, Is.True);
        Assert.That(handler.Requests.Count, Is.EqualTo(1));
        Assert.That(handler.Requests[0].RequestUri?.ToString(), Is.EqualTo("https://graph.facebook.com/v20.0/phone-1/messages"));
        Assert.That(handler.Requests[0].Headers.Authorization?.Scheme, Is.EqualTo("Bearer"));
        Assert.That(handler.Requests[0].Headers.Authorization?.Parameter, Is.EqualTo("sender_token"));
    }

    [Test]
    public async Task send_replaces_phone_number_placeholder_in_default_endpoint()
    {
        var handler = new RecordingHandler();
        var http = new HttpClient(handler);
        var options = Options.Create(new WhatsAppOptions
        {
            AccessToken = "global_token",
            MessagesEndpoint = "https://graph.facebook.com/v20.0/{phone_number_id}/messages"
        });
        var sites = new FixedSiteRepository(new Site
        {
            Id = "site_a",
            Name = "Site A",
            OwnerEmail = "owner@example.com",
            WhatsAppNumber = "447000000000",
            WhatsAppPhoneNumberId = "phone-2"
        });
        var client = new WhatsAppClient(http, options, sites, NullLogger<WhatsAppClient>.Instance);

        var sent = await client.SendTextAsync("447111111111", "hello", "site_a", CancellationToken.None);

        Assert.That(sent, Is.True);
        Assert.That(handler.Requests[0].RequestUri?.ToString(), Is.EqualTo("https://graph.facebook.com/v20.0/phone-2/messages"));
        Assert.That(handler.Requests[0].Headers.Authorization?.Scheme, Is.EqualTo("Bearer"));
        Assert.That(handler.Requests[0].Headers.Authorization?.Parameter, Is.EqualTo("global_token"));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed class FixedSiteRepository(Site site) : ISiteRepository
    {
        public Task<Site?> GetByIdAsync(string siteId, CancellationToken ct)
            => Task.FromResult<Site?>(string.Equals(site.Id, siteId, StringComparison.Ordinal) ? site : null);

        public Task<Site?> GetByWhatsAppPhoneNumberIdAsync(string phoneNumberId, CancellationToken ct)
            => Task.FromResult<Site?>(string.Equals(site.WhatsAppPhoneNumberId, phoneNumberId, StringComparison.Ordinal) ? site : null);

        public Task<IReadOnlyList<Site>> GetAllAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Site>>(new[] { site });

        public Task UpsertAsync(Site updated, CancellationToken ct) => Task.CompletedTask;
    }
}
