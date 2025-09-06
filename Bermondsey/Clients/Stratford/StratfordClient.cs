using Bermondsey.Models;
using Bermondsey.Options;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;


namespace Bermondsey.Clients.Stratford;
public class StratfordClient : IStratfordClient
{
    private readonly string _url;
    private readonly HttpClient _httpClient;
    private readonly TokenProvider _tokenProvider;

    public StratfordClient(
            HttpClient httpClient,
            TokenProvider tokenProvider,
            IOptions<StratfordOptions> stratfordOptions)
    {
        _httpClient = httpClient ??
           throw new ArgumentNullException(nameof(httpClient));
        _tokenProvider = tokenProvider ??
            throw new ArgumentNullException(nameof(tokenProvider));

        var options = stratfordOptions.Value ??
            throw new ArgumentNullException(nameof(stratfordOptions));

        _url = $"{options.BaseUrl}/{options.Users}";
    }

    public async Task<Result<IEnumerable<UserDetails>>> GetUserDetailsAsync(IEnumerable<Guid> ids)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
              new AuthenticationHeaderValue("Bearer", _tokenProvider.CreateToken());

        try
        {
            var response = await _httpClient.PostAsJsonAsync(_url, ids);
            if(response.IsSuccessStatusCode)
            {
                var userDetails = await response.Content.ReadFromJsonAsync<IEnumerable<UserDetails>>();
                return userDetails is null
                        ? Result.Failure<IEnumerable<UserDetails>>(
                            $"Null response from retrieving users details.")
                        : Result.Success(userDetails);
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            return Result.Failure<IEnumerable<UserDetails>>(
                $"Users details response failed {response.StatusCode}: {errorContent}");
        }
        catch (Exception ex)
        {
            return Result
                .Failure<IEnumerable<UserDetails>>($"Exception getting users details : {ex.Message}");
        }
    }
}
