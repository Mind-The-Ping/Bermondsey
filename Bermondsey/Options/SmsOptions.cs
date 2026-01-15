namespace Bermondsey.Options;

public class SmsOptions
{
    public int MaxPerMinute { get; set; }
    public string PhoneNumbers { get; set; } = string.Empty;
    public required string ConnectionString { get; set; }
    public List<string> GetPhoneNumberList() =>
        PhoneNumbers?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
        ?? new List<string>();
}
