namespace Bermondsey.Options;
public class JwtOptions
{
    public required string Secret { get; set; }
    public required string Issuer { get; set; }
    public required string Audience { get; set; }
    public double ExpirationInMinutes { get; set; }
}
