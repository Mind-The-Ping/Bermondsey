using CSharpFunctionalExtensions;
using System.Text;
using System.Text.Json;

namespace Bermondsey.Clients.NotificationClient;
public static class NotificationTemplateLoader
{
    private static readonly string BasePath =
        Path.Combine(AppContext.BaseDirectory, "NotificationsTemplate");

    public static Result<string> BuildPayload(
        string templateName, 
        string title, 
        string body, 
        Guid notificationId)
    {
        var path = Path.Combine(BasePath, $"{templateName}.json");

        if(!File.Exists(path)) {
            return Result.Failure<string>($"Template not found: {path}");
        }

        var json = File.ReadAllText(path, Encoding.UTF8);

        var payload = json
            .Replace("{title}", JsonEncodedText.Encode(title).ToString())
            .Replace("{body}", JsonEncodedText.Encode(body).ToString())
            .Replace("{id}", notificationId.ToString());

        return Result.Success(payload);
    }
}
