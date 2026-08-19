using LeadRelay.Web.Security;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class DomainAllowListTests
{
    [Test]
    public void request_domain_comes_only_from_browser_source_headers()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DomainAllowList.GetRequestDomain("https://customer.example/page", null), Is.EqualTo("customer.example"));
            Assert.That(DomainAllowList.GetRequestDomain(null, "https://shop.example"), Is.EqualTo("shop.example"));
            Assert.That(DomainAllowList.GetRequestDomain(null, null), Is.Null);
        });
    }

    [Test]
    public void allows_any_domain_when_allow_list_empty()
    {
        var allowed = Array.Empty<string>();
        Assert.That(DomainAllowList.IsAllowedDomain(allowed, "https://example.com/page", null), Is.True);
    }

    [Test]
    public void allows_exact_domain_match()
    {
        var allowed = new[] { "example.com" };
        Assert.That(DomainAllowList.IsAllowedDomain(allowed, "https://example.com/page", null), Is.True);
    }

    [Test]
    public void allows_subdomain_match()
    {
        var allowed = new[] { "example.com" };
        Assert.That(DomainAllowList.IsAllowedDomain(allowed, "https://foo.example.com/page", null), Is.True);
    }

    [Test]
    public void rejects_sibling_domain()
    {
        var allowed = new[] { "example.com" };
        Assert.That(DomainAllowList.IsAllowedDomain(allowed, "https://example.net/page", null), Is.False);
    }

    [Test]
    public void falls_back_to_origin_when_referer_missing()
    {
        var allowed = new[] { "example.com" };
        Assert.That(DomainAllowList.IsAllowedDomain(allowed, null, "https://foo.example.com"), Is.True);
    }

    [Test]
    public void falls_back_to_request_host_when_headers_missing()
    {
        var allowed = new[] { "example.com" };
        Assert.That(DomainAllowList.IsAllowedDomain(allowed, null, null, "example.com"), Is.True);
    }

    [Test]
    public void trims_whitespace_and_dots_in_allow_list()
    {
        var allowed = new[] { "  .example.com.  " };
        Assert.That(DomainAllowList.IsAllowedDomain(allowed, "https://bar.example.com", null), Is.True);
    }

    [Test]
    public void allows_port_in_referer()
    {
        var allowed = new[] { "example.com" };
        Assert.That(DomainAllowList.IsAllowedDomain(allowed, "https://example.com:8443/page", null), Is.True);
    }
}
