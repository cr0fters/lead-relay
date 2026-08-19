using System.Net.Http.Headers;
using System.Text.Json;
using LeadRelay.Application.Abstractions;
using LeadRelay.Domain.Sites;
using LeadRelay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace LeadRelay.Web.WhatsApp;

public sealed class WhatsAppOnboardingService(
    HttpClient http,
    LeadRelayDbContext db,
    ISiteRepository sites,
    WhatsAppCredentialProtector protector,
    WhatsAppClient whatsAppClient,
    IClock clock,
    IOptions<WhatsAppOptions> options,
    ILogger<WhatsAppOnboardingService> logger)
{
    private readonly WhatsAppOptions _options = options.Value;

    public async Task<WhatsAppConnectionSummary> GetSummaryAsync(string siteId, CancellationToken ct)
    {
        var connection = await db.WhatsAppConnections.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SiteId == siteId, ct);
        return ToSummary(connection);
    }

    public async Task<WhatsAppConnectionResult> ConnectEmbeddedSignupAsync(
        string siteId,
        WhatsAppEmbeddedSignupRequest request,
        CancellationToken ct)
    {
        var site = await sites.GetByIdAsync(siteId, ct);
        if (site is null)
            return new WhatsAppConnectionResult(false, "Site not found.");
        if (!protector.IsConfigured)
            return new WhatsAppConnectionResult(false, "Secure WhatsApp credential storage is not configured.");
        if (!_options.IsEmbeddedSignupConfigured)
        {
            return new WhatsAppConnectionResult(false, "WhatsApp Embedded Signup is not configured yet.");
        }

        var authorizationCode = request.AuthorizationCode?.Trim();
        var wabaId = NormalizeIdentifier(request.WabaId);
        var requestedPhoneNumberId = string.IsNullOrWhiteSpace(request.PhoneNumberId)
            ? null
            : NormalizeIdentifier(request.PhoneNumberId);
        if (string.IsNullOrWhiteSpace(authorizationCode) || authorizationCode.Length > 8192 || wabaId is null)
            return new WhatsAppConnectionResult(false, "Meta did not return a complete authorization. Please restart WhatsApp setup.");
        if (!string.IsNullOrWhiteSpace(request.PhoneNumberId) && requestedPhoneNumberId is null)
            return new WhatsAppConnectionResult(false, "Meta returned an invalid phone number identifier. Please restart WhatsApp setup.");

        try
        {
            var tokenResult = await ExchangeAuthorizationCodeAsync(authorizationCode, ct);
            if (!tokenResult.Succeeded || string.IsNullOrWhiteSpace(tokenResult.AccessToken))
                return new WhatsAppConnectionResult(false, tokenResult.Error);

            var phoneResult = await ResolveCoexistencePhoneAsync(
                wabaId,
                requestedPhoneNumberId,
                tokenResult.AccessToken,
                ct);
            if (!phoneResult.Succeeded || phoneResult.PhoneNumberId is null || phoneResult.DisplayPhoneNumber is null)
                return new WhatsAppConnectionResult(false, phoneResult.Error);

            var availability = await EnsurePhoneAvailableAsync(siteId, phoneResult.PhoneNumberId, ct);
            if (!availability.Succeeded)
                return availability;

            var subscription = await SubscribeAppAsync(wabaId, tokenResult.AccessToken, ct);
            if (!subscription.Succeeded)
            {
                await RecordFailureAsync(siteId, subscription.Error ?? "Webhook subscription failed.", ct);
                return subscription;
            }

            return await PersistConnectionAsync(
                site,
                wabaId,
                phoneResult.PhoneNumberId,
                phoneResult.DisplayPhoneNumber,
                tokenResult.AccessToken,
                ct);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Meta Embedded Signup completion failed for site {SiteId}.", siteId);
            const string error = "Meta could not be reached while completing WhatsApp setup. Please try again.";
            await RecordFailureAsync(siteId, error, ct);
            return new WhatsAppConnectionResult(false, error);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            const string error = "Meta did not respond in time. Please restart WhatsApp setup.";
            await RecordFailureAsync(siteId, error, ct);
            return new WhatsAppConnectionResult(false, error);
        }
    }

    public async Task<WhatsAppConnectionResult> ConnectAsync(
        string siteId,
        WhatsAppConnectRequest request,
        CancellationToken ct)
    {
        var site = await sites.GetByIdAsync(siteId, ct);
        if (site is null)
            return new WhatsAppConnectionResult(false, "Site not found.");
        if (!protector.IsConfigured)
            return new WhatsAppConnectionResult(false, "Secure WhatsApp credential storage is not configured.");

        var wabaId = NormalizeIdentifier(request.WabaId);
        var phoneNumberId = NormalizeIdentifier(request.PhoneNumberId);
        var suppliedDisplayNumber = NormalizePhone(request.DisplayPhoneNumber);
        var accessToken = request.AccessToken?.Trim();
        if (wabaId is null || phoneNumberId is null || string.IsNullOrWhiteSpace(accessToken) || accessToken.Length > 8192)
            return new WhatsAppConnectionResult(false, "WABA ID, phone number ID, and access token are required.");

        var availability = await EnsurePhoneAvailableAsync(siteId, phoneNumberId, ct);
        if (!availability.Succeeded)
            return availability;

        (bool Succeeded, string? DisplayPhoneNumber, string? Error) validation;
        WhatsAppConnectionResult subscription;
        try
        {
            validation = await ValidatePhoneNumberAsync(phoneNumberId, accessToken, ct);
            if (validation.Succeeded)
                subscription = await SubscribeAppAsync(wabaId, accessToken, ct);
            else
                subscription = new WhatsAppConnectionResult(false, validation.Error);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Meta WhatsApp connection request failed for site {SiteId}.", siteId);
            const string error = "Meta could not be reached. Check the connection details and try again.";
            await RecordFailureAsync(siteId, error, ct);
            return new WhatsAppConnectionResult(false, error);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            const string error = "Meta did not respond in time. Please try again.";
            await RecordFailureAsync(siteId, error, ct);
            return new WhatsAppConnectionResult(false, error);
        }

        if (!validation.Succeeded)
        {
            await RecordFailureAsync(siteId, validation.Error ?? "WhatsApp validation failed.", ct);
            return new WhatsAppConnectionResult(false, validation.Error);
        }

        if (!subscription.Succeeded)
        {
            await RecordFailureAsync(siteId, subscription.Error ?? "Webhook subscription failed.", ct);
            return new WhatsAppConnectionResult(false, subscription.Error);
        }

        var displayNumber = NormalizePhone(validation.DisplayPhoneNumber) ?? suppliedDisplayNumber;
        if (displayNumber is null)
            return new WhatsAppConnectionResult(false, "Meta did not return a usable display phone number. Enter it manually and retry.");

        return await PersistConnectionAsync(site, wabaId, phoneNumberId, displayNumber, accessToken, ct);
    }

    private async Task<(bool Succeeded, string? AccessToken, string? Error)> ExchangeAuthorizationCodeAsync(
        string authorizationCode,
        CancellationToken ct)
    {
        var query = QueryString.Create(new Dictionary<string, string?>
        {
            ["client_id"] = _options.MetaAppId,
            ["client_secret"] = _options.AppSecret,
            ["code"] = authorizationCode,
            ["redirect_uri"] = ""
        });
        var endpoint = WhatsAppGraphApiEndpoints.Build(_options, $"oauth/access_token{query}");
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Meta rejected an Embedded Signup code exchange with status {StatusCode}.", (int)response.StatusCode);
            return (false, null, "Meta could not authorize LeadRelay. Please restart WhatsApp setup.");
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var token = document.RootElement.TryGetProperty("access_token", out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()?.Trim()
                : null;
            return string.IsNullOrWhiteSpace(token)
                ? (false, null, "Meta did not return a usable authorization. Please restart WhatsApp setup.")
                : (true, token, null);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Meta returned invalid JSON while exchanging an Embedded Signup code.");
            return (false, null, "Meta returned an unreadable authorization response. Please restart WhatsApp setup.");
        }
    }

    private async Task<(bool Succeeded, string? PhoneNumberId, string? DisplayPhoneNumber, string? Error)> ResolveCoexistencePhoneAsync(
        string wabaId,
        string? requestedPhoneNumberId,
        string accessToken,
        CancellationToken ct)
    {
        try
        {
            var listEndpoint = WhatsAppGraphApiEndpoints.Build(_options, $"{wabaId}/phone_numbers?fields=id&limit=100");
            using var listRequest = CreateRequest(HttpMethod.Get, listEndpoint, accessToken);
            using var listResponse = await http.SendAsync(listRequest, ct);
            var listBody = await listResponse.Content.ReadAsStringAsync(ct);
            if (!listResponse.IsSuccessStatusCode)
                return (false, null, null, ExtractSafeError(listBody, "Meta could not discover the connected WhatsApp number."));

            using var listDocument = JsonDocument.Parse(listBody);
            if (!listDocument.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return (false, null, null, "Meta did not return the WhatsApp numbers shared during setup.");

            var sharedPhoneIds = data.EnumerateArray()
                .Select(item => item.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String
                    ? NormalizeIdentifier(id.GetString())
                    : null)
                .Where(id => id is not null)
                .Cast<string>()
                .ToList();
            if (sharedPhoneIds.Count == 0)
                return (false, null, null, "Meta did not share a WhatsApp number with LeadRelay.");
            if (requestedPhoneNumberId is null && sharedPhoneIds.Count != 1)
                return (false, null, null, "Meta returned multiple WhatsApp numbers without identifying the selected one. Please restart setup.");

            var selectedPhoneId = requestedPhoneNumberId ?? sharedPhoneIds[0];
            if (!sharedPhoneIds.Contains(selectedPhoneId, StringComparer.Ordinal))
                return (false, null, null, "The WhatsApp number selected in Meta was not shared with LeadRelay. Please restart setup.");

            var detailsEndpoint = WhatsAppGraphApiEndpoints.Build(
                _options,
                $"{selectedPhoneId}?fields=id,display_phone_number,is_on_biz_app");
            using var detailsRequest = CreateRequest(HttpMethod.Get, detailsEndpoint, accessToken);
            using var detailsResponse = await http.SendAsync(detailsRequest, ct);
            var detailsBody = await detailsResponse.Content.ReadAsStringAsync(ct);
            if (!detailsResponse.IsSuccessStatusCode)
                return (false, null, null, ExtractSafeError(detailsBody, "Meta could not validate the connected WhatsApp number."));

            using var detailsDocument = JsonDocument.Parse(detailsBody);
            var root = detailsDocument.RootElement;
            var returnedPhoneId = root.TryGetProperty("id", out var returnedId) && returnedId.ValueKind == JsonValueKind.String
                ? NormalizeIdentifier(returnedId.GetString())
                : null;
            if (!string.Equals(returnedPhoneId, selectedPhoneId, StringComparison.Ordinal))
                return (false, null, null, "Meta returned a different WhatsApp number while validating coexistence.");
            var displayNumber = root.TryGetProperty("display_phone_number", out var display) && display.ValueKind == JsonValueKind.String
                ? NormalizePhone(display.GetString())
                : null;
            if (displayNumber is null)
                return (false, null, null, "Meta did not return a usable display number for the selected WhatsApp account.");
            var isOnBusinessApp = root.TryGetProperty("is_on_biz_app", out var coexistence) && coexistence.ValueKind == JsonValueKind.True;
            if (!isOnBusinessApp)
                return (false, null, null, "That number is not eligible for WhatsApp Business App coexistence. Keep it in the Business App and restart setup.");

            return (true, selectedPhoneId, displayNumber, null);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Meta returned invalid JSON while discovering WhatsApp numbers for WABA {WabaId}.", wabaId);
            return (false, null, null, "Meta returned an unreadable WhatsApp account response. Please restart setup.");
        }
    }

    private async Task<WhatsAppConnectionResult> EnsurePhoneAvailableAsync(
        string siteId,
        string phoneNumberId,
        CancellationToken ct)
    {
        var conflictingConnection = await db.WhatsAppConnections.AsNoTracking()
            .AnyAsync(x => x.PhoneNumberId == phoneNumberId && x.SiteId != siteId, ct);
        var existingSenderSite = await sites.GetByWhatsAppPhoneNumberIdAsync(phoneNumberId, ct);
        return conflictingConnection || (existingSenderSite is not null && existingSenderSite.Id != siteId)
            ? new WhatsAppConnectionResult(false, "That WhatsApp phone number is already connected to another account.")
            : new WhatsAppConnectionResult(true);
    }

    private async Task<WhatsAppConnectionResult> PersistConnectionAsync(
        Site site,
        string wabaId,
        string phoneNumberId,
        string displayNumber,
        string accessToken,
        CancellationToken ct)
    {
        var now = clock.UtcNow;
        var connection = await db.WhatsAppConnections.FirstOrDefaultAsync(x => x.SiteId == site.Id, ct);
        var senderIdentityChanged = connection is not null &&
            (!string.Equals(connection.WabaId, wabaId, StringComparison.Ordinal) ||
             !string.Equals(connection.PhoneNumberId, phoneNumberId, StringComparison.Ordinal));
        if (connection is null)
        {
            connection = new WhatsAppConnectionRecord { SiteId = site.Id };
            db.WhatsAppConnections.Add(connection);
        }

        connection.WabaId = wabaId;
        connection.PhoneNumberId = phoneNumberId;
        connection.DisplayPhoneNumber = displayNumber;
        connection.AccessTokenCiphertext = protector.Protect(site.Id, accessToken);
        connection.Status = WhatsAppConnectionStatuses.Connected;
        connection.WebhookSubscribedAtUtc = now;
        connection.LastValidatedAtUtc = now;
        connection.LastOutboundTestAtUtc = null;
        connection.LastOutboundTestRecipient = null;
        if (senderIdentityChanged)
            connection.LastInboundAtUtc = null;
        connection.LastError = null;
        connection.UpdatedAtUtc = now;

        try
        {
            await db.SaveChangesAsync(ct);
            await sites.UpsertAsync(CopySiteWithWhatsApp(site, displayNumber, phoneNumberId), ct);
        }
        catch (DbUpdateException exception) when (IsPhoneNumberConflict(exception))
        {
            logger.LogWarning(exception, "WhatsApp phone number uniqueness conflict for site {SiteId}.", site.Id);
            return new WhatsAppConnectionResult(false, "That WhatsApp phone number is already connected to another account.");
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(exception, "WhatsApp connection database write failed for site {SiteId}.", site.Id);
            return new WhatsAppConnectionResult(false, "WhatsApp could not be saved right now. Please try again.");
        }
        return new WhatsAppConnectionResult(true);
    }

    public async Task<WhatsAppConnectionResult> SendTestAsync(string siteId, string? recipient, CancellationToken ct)
    {
        var normalizedRecipient = NormalizePhone(recipient);
        if (normalizedRecipient is null)
            return new WhatsAppConnectionResult(false, "Enter a valid test recipient including country code.");

        var connection = await db.WhatsAppConnections.FirstOrDefaultAsync(x => x.SiteId == siteId, ct);
        if (connection is null || !string.Equals(connection.Status, WhatsAppConnectionStatuses.Connected, StringComparison.Ordinal))
            return new WhatsAppConnectionResult(false, "Connect WhatsApp before sending a test message.");

        var sent = await whatsAppClient.SendTextAsync(
            normalizedRecipient,
            "Your LeadRelay WhatsApp connection is working.",
            siteId,
            ct);
        if (!sent)
        {
            connection.Status = WhatsAppConnectionStatuses.ActionRequired;
            connection.LastError = "Test message failed. Check the access token, sender registration, and recipient eligibility.";
            connection.UpdatedAtUtc = clock.UtcNow;
            await db.SaveChangesAsync(ct);
            return new WhatsAppConnectionResult(false, connection.LastError);
        }

        connection.Status = WhatsAppConnectionStatuses.Connected;
        connection.LastOutboundTestAtUtc = clock.UtcNow;
        connection.LastOutboundTestRecipient = normalizedRecipient;
        connection.LastError = null;
        connection.UpdatedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return new WhatsAppConnectionResult(true);
    }

    public async Task<bool> RecordInboundAsync(string siteId, string? sender, CancellationToken ct)
    {
        var connection = await db.WhatsAppConnections.FirstOrDefaultAsync(x => x.SiteId == siteId, ct);
        if (connection is null) return false;
        var normalizedSender = NormalizePhone(sender);
        var isTest = normalizedSender is not null &&
            string.Equals(connection.LastOutboundTestRecipient, normalizedSender, StringComparison.Ordinal);
        connection.LastInboundAtUtc = clock.UtcNow;
        connection.UpdatedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return isTest;
    }

    public async Task DisconnectAsync(string siteId, CancellationToken ct)
    {
        var connection = await db.WhatsAppConnections.FirstOrDefaultAsync(x => x.SiteId == siteId, ct);
        if (connection is not null)
        {
            var wabaUsedElsewhere = await db.WhatsAppConnections.AsNoTracking()
                .AnyAsync(x => x.SiteId != siteId && x.WabaId == connection.WabaId, ct);
            if (!wabaUsedElsewhere && protector.TryUnprotect(siteId, connection.AccessTokenCiphertext, out var accessToken))
            {
                try
                {
                    var endpoint = WhatsAppGraphApiEndpoints.Build(_options, $"{connection.WabaId}/subscribed_apps");
                    using var request = CreateRequest(HttpMethod.Delete, endpoint, accessToken);
                    using var response = await http.SendAsync(request, ct);
                    if (!response.IsSuccessStatusCode)
                        logger.LogWarning("Meta app unsubscribe failed with status {StatusCode} for site {SiteId}.", (int)response.StatusCode, siteId);
                }
                catch (HttpRequestException exception)
                {
                    logger.LogWarning(exception, "Meta app unsubscribe request failed for site {SiteId}; local credential removal will continue.", siteId);
                }
                catch (TaskCanceledException) when (!ct.IsCancellationRequested)
                {
                    logger.LogWarning("Meta app unsubscribe timed out for site {SiteId}; local credential removal will continue.", siteId);
                }
            }

            db.WhatsAppConnections.Remove(connection);
            await db.SaveChangesAsync(ct);
        }

        var site = await sites.GetByIdAsync(siteId, ct);
        if (site is not null)
            await sites.UpsertAsync(CopySiteWithWhatsApp(site, "", null), ct);
    }

    private async Task<(bool Succeeded, string? DisplayPhoneNumber, string? Error)> ValidatePhoneNumberAsync(
        string phoneNumberId,
        string accessToken,
        CancellationToken ct)
    {
        var endpoint = WhatsAppGraphApiEndpoints.Build(_options, $"{phoneNumberId}?fields=id,display_phone_number,verified_name");
        using var request = CreateRequest(HttpMethod.Get, endpoint, accessToken);
        using var response = await http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            return (false, null, ExtractSafeError(body, "Meta could not validate that phone number."));

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var returnedId = root.TryGetProperty("id", out var id) ? id.GetString() : null;
            if (!string.Equals(returnedId, phoneNumberId, StringComparison.Ordinal))
                return (false, null, "Meta returned a different phone number ID.");
            var displayNumber = root.TryGetProperty("display_phone_number", out var display)
                ? display.GetString()
                : null;
            return (true, displayNumber, null);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Meta returned invalid JSON while validating WhatsApp phone number {PhoneNumberId}.", phoneNumberId);
            return (false, null, "Meta returned an unreadable validation response.");
        }
    }

    private async Task<WhatsAppConnectionResult> SubscribeAppAsync(string wabaId, string accessToken, CancellationToken ct)
    {
        var endpoint = WhatsAppGraphApiEndpoints.Build(_options, $"{wabaId}/subscribed_apps");
        using var request = CreateRequest(HttpMethod.Post, endpoint, accessToken);
        request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
            return new WhatsAppConnectionResult(true);

        var body = await response.Content.ReadAsStringAsync(ct);
        return new WhatsAppConnectionResult(false, ExtractSafeError(body, "Meta could not subscribe the app to that WhatsApp account."));
    }

    private async Task RecordFailureAsync(string siteId, string error, CancellationToken ct)
    {
        var connection = await db.WhatsAppConnections.FirstOrDefaultAsync(x => x.SiteId == siteId, ct);
        if (connection is null) return;
        connection.Status = WhatsAppConnectionStatuses.ActionRequired;
        connection.LastError = Truncate(error, 1000);
        connection.UpdatedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string endpoint, string accessToken)
    {
        var request = new HttpRequestMessage(method, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static string ExtractSafeError(string body, string fallback)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message) &&
                message.ValueKind == JsonValueKind.String)
            {
                return Truncate(message.GetString() ?? fallback, 500);
            }
        }
        catch (JsonException)
        {
        }

        return fallback;
    }

    private static string? NormalizeIdentifier(string? value)
    {
        var normalized = value?.Trim();
        return !string.IsNullOrWhiteSpace(normalized) && normalized.Length <= 64 && normalized.All(char.IsDigit)
            ? normalized
            : null;
    }

    private static string? NormalizePhone(string? value)
    {
        var digits = new string((value ?? "").Where(char.IsDigit).ToArray());
        return digits.Length is >= 7 and <= 20 ? digits : null;
    }

    private static Site CopySiteWithWhatsApp(Site site, string displayNumber, string? phoneNumberId) => new()
    {
        Id = site.Id,
        Name = site.Name,
        BusinessSummary = site.BusinessSummary,
        AllowedDomains = site.AllowedDomains,
        Fields = site.Fields,
        IntroMessage = site.IntroMessage,
        OwnerEmail = site.OwnerEmail,
        WhatsAppNumber = displayNumber,
        WhatsAppPhoneNumberId = phoneNumberId
    };

    private WhatsAppConnectionSummary ToSummary(WhatsAppConnectionRecord? connection)
    {
        if (connection is null)
            return new WhatsAppConnectionSummary(false, "not_connected", null, null, null, null, null, null, null, null);

        var credentialReadable = protector.TryUnprotect(connection.SiteId, connection.AccessTokenCiphertext, out _);
        var status = credentialReadable ? connection.Status : WhatsAppConnectionStatuses.ActionRequired;
        var error = credentialReadable ? connection.LastError : "The stored WhatsApp credential cannot be decrypted. Reconnect WhatsApp.";
        return new WhatsAppConnectionSummary(
                true,
                status,
                connection.WabaId,
                connection.PhoneNumberId,
                connection.DisplayPhoneNumber,
                connection.WebhookSubscribedAtUtc,
                connection.LastValidatedAtUtc,
                connection.LastInboundAtUtc,
                connection.LastOutboundTestAtUtc,
                error);
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private static bool IsPhoneNumberConflict(DbUpdateException exception)
        => exception.InnerException is MySqlException mysqlException &&
           mysqlException.ErrorCode == MySqlErrorCode.DuplicateKeyEntry &&
           (mysqlException.Message.Contains("IX_WhatsAppConnections_PhoneNumberId", StringComparison.OrdinalIgnoreCase) ||
            mysqlException.Message.Contains("IX_Sites_WhatsAppPhoneNumberId", StringComparison.OrdinalIgnoreCase));
}
