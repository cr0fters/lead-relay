using LeadRelay.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class HomeControllerTests
{
    [Test]
    public void index_sets_customer_facing_search_and_share_metadata()
    {
        var controller = new HomeController();

        var result = controller.Index(CancellationToken.None);

        Assert.That(result, Is.TypeOf<ViewResult>());
        Assert.That(controller.ViewData["Title"], Is.EqualTo("LeadRelay | WhatsApp lead qualification and lightweight CRM"));
        Assert.That(controller.ViewData["Description"]?.ToString(), Does.Contain("lightweight CRM inbox"));
        Assert.That(controller.ViewData["CanonicalUrl"], Is.EqualTo("https://leadrelay.dev/"));
        Assert.That(controller.ViewData["OpenGraphImage"]?.ToString(), Does.StartWith("https://leadrelay.dev/"));
    }
}
