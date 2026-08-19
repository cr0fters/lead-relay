namespace LeadRelay.Web.Widgets;

public interface IWidgetInstallationTracker
{
    Task RecordSuccessfulLoadAsync(string siteId, string domain, CancellationToken ct);
}
