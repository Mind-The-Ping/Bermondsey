using Bermondsey.Clients.NotificationClient;
using Bermondsey.Models;
using FluentAssertions;
using System.Text.Json;

namespace Bermondsey.Unit.Tests;
public class NotificationTemplateLoaderTests
{
    [Fact]
    public void NotificationTemplateLoader_BuildPayload_IOS_Successful()
    {
        var phoneOS = PhoneOS.IOS;
        var title = "This is a test title";
        var body = "This is a test body, please take care.";
        var notificationId = Guid.NewGuid();

        var expected = $@"
        {{
          ""aps"": {{
            ""alert"": {{
              ""title"": ""{title}"",
              ""body"": ""{body}""
            }},
            ""sound"": ""default""
          }},
          ""data"": {{
            ""id"": ""{notificationId}""
          }}
        }}";

        var result = NotificationTemplateLoader.BuildPayload(
            phoneOS.ToString().ToLower(),
            title,
            body,
            notificationId);

        result.IsSuccess.Should().BeTrue();

        Normalize(result.Value).Should().Be(Normalize(expected));
    }

    [Fact]
    public void NotificationTemplateLoader_BuildPayload_Android_Successful()
    {
        var phoneOS = PhoneOS.Android;
        var title = "This is a test title";
        var body = "This is a test body, please take care.";
        var notificationId = Guid.NewGuid();

        var expected = $@"
        {{
          ""message"": {{
            ""notification"": {{
              ""title"": ""{title}"",
              ""body"": ""{body}""
            }},
            ""data"": {{
              ""id"": ""{notificationId}""
            }}
          }}
        }}";

        var result = NotificationTemplateLoader.BuildPayload(
            phoneOS.ToString().ToLower(),
            title,
            body,
            notificationId);

        result.IsSuccess.Should().BeTrue();

        Normalize(result.Value).Should().Be(Normalize(expected));
    }

    private string Normalize(string json) => 
        JsonSerializer.Serialize(JsonSerializer.Deserialize<object>(json));
}
