using System.Net;
using System.Text.Json;
using LeadRelay.Infrastructure.Email;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class PostmarkEmailSenderTests
{
    [Test]
    public async Task send_async_posts_expected_payload_and_headers()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.postmarkapp.com/") };
        var options = Options.Create(new PostmarkOptions
        {
            Enabled = true,
            ServerToken = "pm_server_token",
            FromEmail = "noreply@example.com",
            FromName = "LeadRelay"
        });

        var sender = new PostmarkEmailSender(client, options, NullLogger<PostmarkEmailSender>.Instance);
        await sender.SendAsync("lead@example.com", "Subject", "Hello world", CancellationToken.None);

        Assert.That(handler.Requests.Count, Is.EqualTo(1));
        var request = handler.Requests[0];
        Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
        Assert.That(request.RequestUri?.ToString(), Is.EqualTo("https://api.postmarkapp.com/email"));
        Assert.That(request.Headers.TryGetValues("X-Postmark-Server-Token", out var values), Is.True);
        Assert.That(values?.Single(), Is.EqualTo("pm_server_token"));

        var body = await request.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.That(doc.RootElement.GetProperty("from").GetString(), Is.EqualTo("LeadRelay <noreply@example.com>"));
        Assert.That(doc.RootElement.GetProperty("to").GetString(), Is.EqualTo("lead@example.com"));
        Assert.That(doc.RootElement.GetProperty("subject").GetString(), Is.EqualTo("Subject"));
        Assert.That(doc.RootElement.GetProperty("textBody").GetString(), Is.EqualTo("Hello world"));
    }

    [Test]
    public async Task send_template_async_posts_template_payload()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.postmarkapp.com/") };
        var options = Options.Create(new PostmarkOptions
        {
            Enabled = true,
            ServerToken = "pm_server_token",
            FromEmail = "noreply@example.com",
            FromName = "LeadRelay"
        });

        var sender = new PostmarkEmailSender(client, options, NullLogger<PostmarkEmailSender>.Instance);
        await sender.SendTemplateAsync(
            "lead@example.com",
            "password-reset",
            43533665,
            new Dictionary<string, string>
            {
                ["name"] = "Andrew",
                ["action_url"] = "https://leadrelay.test/reset"
            },
            CancellationToken.None);

        Assert.That(handler.Requests.Count, Is.EqualTo(1));
        var request = handler.Requests[0];
        Assert.That(request.RequestUri?.ToString(), Is.EqualTo("https://api.postmarkapp.com/email/withTemplate"));

        var body = await request.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.That(ReadPropertyIgnoreCase(doc.RootElement, "templateAlias").GetString(), Is.EqualTo("password-reset"));
        Assert.That(ReadPropertyIgnoreCase(doc.RootElement, "templateId").GetInt32(), Is.EqualTo(43533665));
        Assert.That(ReadPropertyIgnoreCase(doc.RootElement, "to").GetString(), Is.EqualTo("lead@example.com"));
    }

    [Test]
    public void send_async_throws_when_postmark_returns_error()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"Message\":\"Bad request\"}")
        });
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.postmarkapp.com/") };
        var options = Options.Create(new PostmarkOptions
        {
            Enabled = true,
            ServerToken = "pm_server_token",
            FromEmail = "noreply@example.com"
        });
        var sender = new PostmarkEmailSender(client, options, NullLogger<PostmarkEmailSender>.Instance);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await sender.SendAsync("lead@example.com", "Subject", "Hello world", CancellationToken.None));
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder = responder;
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responder(request));
        }
    }

    private static JsonElement ReadPropertyIgnoreCase(JsonElement element, string name)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                return property.Value;
        }

        throw new KeyNotFoundException($"Missing JSON property: {name}");
    }
}
