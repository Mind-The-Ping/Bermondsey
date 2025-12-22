using CSharpFunctionalExtensions;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bermondsey.Clients.NotificationClient;
public static class NotificationTemplateLoader
{
    private static readonly string BasePath =
        Path.Combine(AppContext.BaseDirectory, "NotificationTemplates");

    public static Result<string> BuildPayload(
        string templateName, 
        string title, 
        string body, 
        Guid notificationId,
        int? badge)
    {
        var path = Path.Combine(BasePath, $"{templateName}.json");

        if(!File.Exists(path)) {
            return Result.Failure<string>($"Template not found: {path}");
        }

        var json = File.ReadAllText(path, Encoding.UTF8)
            .Replace("{title}", JsonEncodedText.Encode(title).ToString())
            .Replace("{body}", JsonEncodedText.Encode(body).ToString())
            .Replace("{id}", notificationId.ToString());


        if (!badge.HasValue) {
            return Result.Success(json);
        }

        var root = JsonNode.Parse(json);
        if (root is null) {
            return Result.Failure<string>("Invalid JSON template");
        }

        var aps = root["message"]?["apns"]?["payload"]?["aps"] as JsonObject;

        if (aps is not null) {
            aps["badge"] = badge.Value;
        }

        return Result.Success(root.ToJsonString());
    }
}
