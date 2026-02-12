using LeadRelay.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;

namespace LeadRelay.UnitTests;

public sealed class LegalControllerTests
{
    [Test]
    public void privacy_policy_returns_expected_view()
    {
        var controller = new LegalController();

        var result = controller.PrivacyPolicy();

        Assert.That(result, Is.TypeOf<ViewResult>());
        Assert.That(result.ViewName, Is.EqualTo("PrivacyPolicy"));
    }

    [Test]
    public void terms_and_conditions_returns_expected_view()
    {
        var controller = new LegalController();

        var result = controller.TermsAndConditions();

        Assert.That(result, Is.TypeOf<ViewResult>());
        Assert.That(result.ViewName, Is.EqualTo("TermsAndConditions"));
    }

    [Test]
    public void user_data_deletion_returns_expected_view()
    {
        var controller = new LegalController();

        var result = controller.UserDataDeletion();

        Assert.That(result, Is.TypeOf<ViewResult>());
        Assert.That(result.ViewName, Is.EqualTo("UserDataDeletion"));
    }
}
