namespace Bermondsey.Clients.SmsClient.PhoneNumber;

public class PhoneNumberPool
{
    private int _index = -1;
    private readonly IEnumerable<string> _numbers;

    public int Count => _numbers.Count();

    public PhoneNumberPool(IEnumerable<string> numbers)
    {
        if (numbers == null || !numbers.Any()) {
            throw new ArgumentNullException("At least one phone number is required");
        }

        _numbers = numbers;
    }

    public string Next()
    {
        var i = Interlocked.Increment(ref _index);
        return _numbers.ElementAt(i % _numbers.Count());
    }
}
