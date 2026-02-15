using System.Net;
using System.Text.Json;
using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Sites;
using LeadRelay.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class AdminControllerTests
{
    [Test]
    public async Task send_global_email_test_rejects_invalid_recipient()
    {
        var controller = CreateController(new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

        var result = await controller.SendGlobalEmailTest(
            new AdminController.EmailTestInputModel
            {
                ServerToken = "pm_server_token",
                FromEmail = "noreply@example.com",
                ToEmail = "not-an-email"
            },
            CancellationToken.None);

        Assert.That(result, Is.TypeOf<ViewResult>());
        var model = ((ViewResult)result).Model as AdminController.AdminDashboardModel;
        Assert.That(model, Is.Not.Null);
        Assert.That(model!.EmailTest.Error, Is.EqualTo("Enter a valid recipient email address."));
    }

    [Test]
    public async Task send_global_email_test_sends_postmark_request_with_supplied_credentials()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var controller = CreateController(handler);

        var result = await controller.SendGlobalEmailTest(
            new AdminController.EmailTestInputModel
            {
                ApiBaseUrl = "https://api.postmarkapp.com",
                ServerToken = "pm_server_token",
                FromEmail = "noreply@example.com",
                FromName = "LeadRelay",
                ToEmail = "owner@example.com",
                Subject = "Test subject",
                BodyText = "Test body"
            },
            CancellationToken.None);

        Assert.That(result, Is.TypeOf<ViewResult>());
        Assert.That(handler.Requests.Count, Is.EqualTo(1));

        var request = handler.Requests[0];
        Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
        Assert.That(request.Url, Is.EqualTo("https://api.postmarkapp.com/email"));
        Assert.That(request.PostmarkToken, Is.EqualTo("pm_server_token"));

        using var json = JsonDocument.Parse(request.Body ?? "{}");
        Assert.That(json.RootElement.GetProperty("From").GetString(), Is.EqualTo("LeadRelay <noreply@example.com>"));
        Assert.That(json.RootElement.GetProperty("To").GetString(), Is.EqualTo("owner@example.com"));
        Assert.That(json.RootElement.GetProperty("Subject").GetString(), Is.EqualTo("Test subject"));
        Assert.That(json.RootElement.GetProperty("TextBody").GetString(), Is.EqualTo("Test body"));

        var model = ((ViewResult)result).Model as AdminController.AdminDashboardModel;
        Assert.That(model, Is.Not.Null);
        Assert.That(model!.EmailTest.Success, Does.Contain("Test email sent"));
    }

    [Test]
    public async Task send_global_email_test_surfaces_postmark_error()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
        {
            Content = new StringContent("{\"ErrorCode\":300,\"Message\":\"Sender signature not found.\"}")
        });
        var controller = CreateController(handler);

        var result = await controller.SendGlobalEmailTest(
            new AdminController.EmailTestInputModel
            {
                ServerToken = "bad-token",
                FromEmail = "noreply@example.com",
                ToEmail = "owner@example.com"
            },
            CancellationToken.None);

        Assert.That(result, Is.TypeOf<ViewResult>());
        var model = ((ViewResult)result).Model as AdminController.AdminDashboardModel;
        Assert.That(model, Is.Not.Null);
        Assert.That(model!.EmailTest.Error, Does.Contain("Postmark error 422"));
        Assert.That(model.EmailTest.Error, Does.Contain("Sender signature not found"));
    }

    private static AdminController CreateController(RecordingHandler handler)
    {
        var sites = new FakeSiteRepository(BuildSite());
        var httpFactory = new FakeHttpClientFactory(new HttpClient(handler));
        return new AdminController(sites, httpFactory, NullLogger<AdminController>.Instance);
    }

    private static Site BuildSite() => new()
    {
        Id = "site_demo",
        Name = "Demo site",
        OwnerEmail = "owner@example.com",
        WhatsAppNumber = "447000000000"
    };

    private sealed class FakeSiteRepository(Site site) : ISiteRepository
    {
        private readonly Site _site = site;

        public Task<Site?> GetByIdAsync(string siteId, CancellationToken ct)
            => Task.FromResult(string.Equals(siteId, _site.Id, StringComparison.Ordinal) ? _site : null);

        public Task<Site?> GetByWhatsAppPhoneNumberIdAsync(string phoneNumberId, CancellationToken ct)
            => Task.FromResult<Site?>(null);

        public Task<IReadOnlyList<Site>> GetAllAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Site>>([_site]);

        public Task UpsertAsync(Site site, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder = responder;
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.TryGetValues("X-Postmark-Server-Token", out var tokenValues);
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            Requests.Add(new RecordedRequest(
                request.Method,
                request.RequestUri?.ToString(),
                tokenValues?.SingleOrDefault(),
                body));

            return _responder(request);
        }
    }

    private sealed record RecordedRequest(HttpMethod Method, string? Url, string? PostmarkToken, string? Body);
}
