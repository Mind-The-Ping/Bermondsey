namespace Bermondsey.Options;
public class SmsOptions
{
    public int MaxPerMinute { get; set; }
    public List<string> PhoneNumbers { get; set; } = [];
    public required string ConnectionString  { get; set; }
}
