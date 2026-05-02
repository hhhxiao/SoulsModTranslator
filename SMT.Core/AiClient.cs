using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NLog;

namespace SMT.core;

public static class AiClient
{
    private static readonly HttpClient HttpClient = new();
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 发送对话请求到大模型 API
    /// </summary>
    /// <param name="baseUrl">API 基础地址，如 https://api.deepseek.com</param>
    /// <param name="apiKey">API 密钥</param>
    /// <param name="model">模型名称，如 deepseek-chat</param>
    /// <param name="prompt">用户提示词</param>
    /// <param name="reasoningEffort">推理等级：low / medium / high</param>
    /// <param name="enableDeepThink">是否启用深度思考</param>
    /// <returns>API 返回的文本内容（不含思考过程）</returns>
    /// <exception cref="ArgumentException">参数校验失败时抛出</exception>
    /// <exception cref="HttpRequestException">HTTP 请求失败时抛出</exception>
    /// <exception cref="JsonException">响应解析失败时抛出</exception>
    public static async Task<string> SendChatRequestAsync(
        string baseUrl,
        string apiKey,
        string model,
        string prompt,
        string reasoningEffort,
        bool enableDeepThink)
    {
        // 参数校验
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("Base URL 不能为空", nameof(baseUrl));
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API 密钥不能为空", nameof(apiKey));
        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("模型名称不能为空", nameof(model));
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("提示词不能为空", nameof(prompt));

        Logger.Info($"发送请求: BaseUrl={baseUrl}, Model={model}, ReasoningEffort={reasoningEffort}, DeepThink={enableDeepThink}");

        // 构造请求体
        var messages = new List<object>
        {
            new { role = "user", content = prompt }
        };

        var requestBody = new Dictionary<string, object>
        {
            ["model"] = model,
            ["messages"] = messages,
            ["reasoning_effort"] = reasoningEffort
        };

        if (enableDeepThink)
        {
            requestBody["deep_thinking"] = true;
        }

        var url = baseUrl.TrimEnd('/') + "/chat/completions";
        var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // 发送请求
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        request.Content = content;

        Logger.Info("正在发送 HTTP 请求...");
        var response = await HttpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Logger.Error($"API 请求失败: {response.StatusCode}\n{responseBody}");
            throw new HttpRequestException(
                $"API 请求失败 ({(int)response.StatusCode} {response.ReasonPhrase}): {responseBody}");
        }

        Logger.Info("API 请求成功，正在解析响应");

        // 解析响应，仅提取文本内容
        using var doc = JsonDocument.Parse(responseBody);
        var contentText = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (contentText == null)
        {
            Logger.Error("响应中未找到 content 字段");
            throw new JsonException("API 响应格式异常：未找到 choices[0].message.content");
        }

        Logger.Info("成功提取文本内容");
        return contentText;
    }
}
