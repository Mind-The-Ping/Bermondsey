namespace Bermondsey.Models;

public enum PhoneOS
{
    IOS = 0,
    Android = 1,
    Unknown = 2
}

public record UserDetails(
    Guid Id, 
    string PhoneNumber, 
    PhoneOS PhoneOS);
