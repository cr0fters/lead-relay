using LeadRelay.Web.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class ConfigurationValidationTests
{
    [Test]
    public void production_configuration_accepts_https_public_base_url()
    {
        var builder = CreateProductionBuilder("https://leadrelay.test");

        Assert.DoesNotThrow(() => builder.ValidateRequiredSecrets());
    }

    [Test]
    public void production_configuration_rejects_non_https_public_base_url()
    {
        var builder = CreateProductionBuilder("http://leadrelay.test");

        var exception = Assert.Throws<InvalidOperationException>(() => builder.ValidateRequiredSecrets());
        Assert.That(exception!.Message, Does.Contain("PublicBaseUrl (must be an absolute HTTPS URL)"));
    }

    [TestCase("v23")]
    [TestCase("23.0")]
    [TestCase("vlatest")]
    public void production_configuration_rejects_invalid_graph_api_version(string version)
    {
        var builder = CreateProductionBuilder("https://leadrelay.test");
        builder.Configuration["WhatsApp:GraphApiVersion"] = version;

        var exception = Assert.Throws<InvalidOperationException>(() => builder.ValidateRequiredSecrets());

        Assert.That(exception!.Message, Does.Contain("WhatsApp:GraphApiVersion"));
    }

    [TestCase("http://graph.facebook.com")]
    [TestCase("https://graph.facebook.com/v23.0")]
    [TestCase("https://graph.facebook.com?tenant=one")]
    public void production_configuration_rejects_invalid_graph_api_base_url(string baseUrl)
    {
        var builder = CreateProductionBuilder("https://leadrelay.test");
        builder.Configuration["WhatsApp:GraphApiBaseUrl"] = baseUrl;

        var exception = Assert.Throws<InvalidOperationException>(() => builder.ValidateRequiredSecrets());

        Assert.That(exception!.Message, Does.Contain("WhatsApp:GraphApiBaseUrl"));
    }

    private static WebApplicationBuilder CreateProductionBuilder(string publicBaseUrl)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Production"
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:LeadRelay"] = "Server=localhost;Database=LeadRelay",
            ["AdminAuth:Token"] = "admin-secret",
            ["OwnerPortal:SigningSecret"] = "owner-secret",
            ["PublicBaseUrl"] = publicBaseUrl,
            ["Postmark:ServerToken"] = "postmark-secret",
            ["Postmark:FromEmail"] = "noreply@leadrelay.test",
            ["WhatsApp:VerifyToken"] = "verify-secret",
            ["WhatsApp:AppSecret"] = "app-secret",
            ["WhatsApp:GraphApiBaseUrl"] = "https://graph.facebook.com",
            ["WhatsApp:GraphApiVersion"] = "v23.0",
            ["WhatsApp:CredentialEncryptionKey"] = Convert.ToBase64String(new byte[32]),
            ["WhatsApp:RequireSignatureValidation"] = "true"
        });
        return builder;
    }
}
