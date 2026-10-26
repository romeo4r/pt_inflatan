using System.Net.Http.Json;
using System.Text.Json;

namespace Mundialito.IntegrationTests;

internal static class HttpExtensions
{
    public static async Task<JsonDocument> ReadJsonAsync(this HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    public static HttpRequestMessage WithJson(this HttpRequestMessage request, object body)
    {
        request.Content = JsonContent.Create(body);
        return request;
    }

    public static HttpRequestMessage WithHeader(this HttpRequestMessage request, string name, string value)
    {
        request.Headers.TryAddWithoutValidation(name, value);
        return request;
    }

    public static string NewKey() => Guid.CreateVersion7().ToString();
}
