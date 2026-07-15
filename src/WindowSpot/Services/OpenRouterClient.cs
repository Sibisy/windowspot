using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WindowSpot.Models;

namespace WindowSpot.Services;

/// <summary>
/// OpenRouter의 OpenAI 호환 chat completions API 호출. 안드로이드판 OpenRouterRepository.kt 대응.
/// </summary>
public class OpenRouterClient
{
    private const string ChatCompletionsUrl = "https://openrouter.ai/api/v1/chat/completions";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

    public async Task<string> ChatAsync(string apiKey, string model, IReadOnlyList<ChatMessageView> messages)
    {
        var messagesArray = new List<object>();
        foreach (var msg in messages)
        {
            messagesArray.Add(new { role = msg.IsUser ? "user" : "assistant", content = msg.Text });
        }

        var body = new { model, messages = messagesArray };
        string json = JsonSerializer.Serialize(body);

        using var request = new HttpRequestMessage(HttpMethod.Post, ChatCompletionsUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await Http.SendAsync(request);
        string responseText = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OpenRouter 요청 실패 ({(int)response.StatusCode}): {responseText}");
        }

        using var doc = JsonDocument.Parse(responseText);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }
}
