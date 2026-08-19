using LeadRelay.Web.Security;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class WebsiteDomainNormalizerTests
{
    [Test]
    public void normalize_list_accepts_urls_deduplicates_and_preserves_order()
    {
        var result = WebsiteDomainNormalizer.NormalizeList(
            "https://Example.com/path\nshop.example.com,EXAMPLE.COM");

        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Is.Null);
            Assert.That(result.Domains, Is.EqualTo(new[] { "example.com", "shop.example.com" }));
        });
    }

    [TestCase("ftp://example.com")]
    [TestCase("https://user:password@example.com")]
    [TestCase("not a domain")]
    public void normalize_list_rejects_unsafe_or_invalid_values(string value)
    {
        var result = WebsiteDomainNormalizer.NormalizeList(value);

        Assert.That(result.Error, Is.Not.Null);
        Assert.That(result.Domains, Is.Empty);
    }

    [Test]
    public void normalize_list_rejects_more_than_the_supported_domain_count()
    {
        var input = string.Join('\n', Enumerable.Range(1, WebsiteDomainNormalizer.MaximumDomains + 1)
            .Select(x => $"site{x}.example.com"));

        var result = WebsiteDomainNormalizer.NormalizeList(input);

        Assert.That(result.Error, Does.Contain(WebsiteDomainNormalizer.MaximumDomains.ToString()));
    }
}
