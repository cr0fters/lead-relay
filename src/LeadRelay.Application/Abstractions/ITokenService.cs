namespace LeadRelay.Application.Abstractions;

public interface ITokenService
{
    string CreateSignedToken(Dictionary<string, string> claims, TimeSpan ttl);
    bool TryValidate(string token, out Dictionary<string, string> claims);
}
