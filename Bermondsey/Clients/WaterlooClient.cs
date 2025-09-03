using Bermondsey.Models;
using Bermondsey.Options;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Bermondsey.Clients;
public class WaterlooClient : IWaterlooClient
{
    private readonly HttpClient _httpClient;
    private readonly TokenProvider _tokenProvider;
    private readonly WaterlooOptions _waterlooOptions;

    public WaterlooClient(
            HttpClient httpClient,
            TokenProvider tokenProvider,
            IOptions<WaterlooOptions> waterlooOptions)
    {
        _httpClient = httpClient ??
            throw new ArgumentNullException(nameof(httpClient));
        _tokenProvider = tokenProvider ??
            throw new ArgumentNullException(nameof(tokenProvider));
        _waterlooOptions = waterlooOptions.Value ??
            throw new ArgumentNullException(nameof(waterlooOptions));
    }

    public async Task<Result<IEnumerable<AffectedUser>>> GetAffectedUsersAsync(
        Guid line, 
        Guid startStation, 
        Guid endStation, 
        Severity severity, 
        TimeOnly time, 
        DayOfWeek queryDay,
        CancellationToken cancellationToken = default)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _tokenProvider.CreateToken());

        var url = QueryHelpers.AddQueryString(
             $"{_waterlooOptions.BaseUrl}/{_waterlooOptions.AffectedUsers}",
             new Dictionary<string, string?>
             {
                 ["LineId"] = line.ToString(),
                 ["StartStationId"] = startStation.ToString(),
                 ["EndStationId"] = endStation.ToString(),
                 ["QueryTime"] = time.ToString("HH:mm"),
                 ["QueryDay"] = queryDay.ToString(),
                 ["Serverity"] = severity.ToString()
             });

        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if(response.IsSuccessStatusCode)
            {
                var affectedUsers = await response.Content.ReadFromJsonAsync<IEnumerable<AffectedUser>>();
                return affectedUsers is null
                     ? Result.Failure<IEnumerable<AffectedUser>>(
                         $"Null response from {startStation} to {endStation}")
                     : Result.Success(affectedUsers);
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            return Result.Failure<IEnumerable<AffectedUser>>(
                $"Affected user response failed {response.StatusCode}: {errorContent}");
        }
        catch(Exception ex)
        {
            return Result
                .Failure<IEnumerable<AffectedUser>>($"Exception getting affected users from: {startStation} to {endStation} " +
                $": {ex.Message}");
        }
    }
}
