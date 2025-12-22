using Bermondsey.Clients.NotificationClient;
using Bermondsey.Models;
using FluentAssertions;
using System.Text.Json;
using System.Text.Json.Nodes;

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
          ""message"": {{
            ""notification"": {{
              ""title"": ""{title}"",
              ""body"": ""{body}""
            }},
            ""data"": {{
              ""id"": ""{notificationId}""
            }},
            ""apns"": {{
              ""headers"": {{
                ""apns-priority"": ""10""
              }},
              ""payload"": {{
                ""aps"": {{
                  ""sound"": ""default"",
                  ""interruption-level"": ""time-sensitive""
                }}
              }}
            }}
          }}
        }}";

        var result = NotificationTemplateLoader.BuildPayload(
            phoneOS.ToString().ToLower(),
            title,
            body,
            notificationId,
            null);

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
            }},
            ""android"": {{
              ""priority"": ""HIGH"",
              ""notification"": {{
                ""channel_id"": ""mind_the_ping_id"",
                ""visibility"": ""PUBLIC""
              }}
            }}
          }}
        }}";

        var result = NotificationTemplateLoader.BuildPayload(
            phoneOS.ToString().ToLower(),
            title,
            body,
            notificationId,
            null);

        result.IsSuccess.Should().BeTrue();

        Normalize(result.Value).Should().Be(Normalize(expected));
    }

    [Fact]
    public void NotificationTemplateLoader_BuildPayload_Badge_IOS_Successful()
    {
        var phoneOS = PhoneOS.IOS;
        var title = "This is a test title";
        var body = "This is a test body, please take care.";
        var notificationId = Guid.NewGuid();
        int? badge = 5;

        var expectedJson = JsonNode.Parse($@"
        {{
          ""message"": {{
            ""notification"": {{
              ""title"": ""{title}"",
              ""body"": ""{body}""
            }},
            ""data"": {{
              ""id"": ""{notificationId}""
            }},
            ""apns"": {{
              ""headers"": {{
                ""apns-priority"": ""10""
              }},
              ""payload"": {{
                ""aps"": {{
                  ""sound"": ""default"",
                  ""interruption-level"": ""time-sensitive"",
                  ""badge"": 5
                }}
              }}
            }}
          }}
        }}
        ");


        var result = NotificationTemplateLoader.BuildPayload(
            phoneOS.ToString().ToLower(),
            title,
            body,
            notificationId,
            badge);

        result.IsSuccess.Should().BeTrue();

        var actual = JsonNode.Parse(result.Value)!.ToJsonString();
        var expected = expectedJson!.ToJsonString();

        actual.Should().Be(expected);
    }

    private string Normalize(string json) => 
        JsonSerializer.Serialize(JsonSerializer.Deserialize<object>(json));
}
