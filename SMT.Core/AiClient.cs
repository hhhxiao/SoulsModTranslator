using System.Linq;
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
        bool enableDeepThink,
        CancellationToken cancellationToken = default)
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
        var response = await HttpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

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

    /// <summary>
    /// 使用 AI 翻译导出数据
    /// </summary>
    /// <param name="exportResult">导出结果（含待翻译文本列表）</param>
    /// <param name="config">AI 配置（URL、密钥、模型、提示词等）</param>
    /// <param name="glossaries">术语表文件路径列表</param>
    /// <param name="progress">进度报告回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>(是否全部完成, 已翻译的词条列表)</returns>
    public static async Task<(bool Success, ExportResult Translated)> TranslateWithAiAsync(
        ExportResult exportResult, AiConfigData config,
        List<string>? glossaries = null,
        IProgress<TranslationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var total = exportResult.SentenceList.Count;
        var alreadyTranslated = exportResult.TranslatedIds.Count;
        Logger.Info($"AI 翻译开始，共 {total} 条词条，其中 {alreadyTranslated} 条已标记为已翻译，每批 {config.BatchSize} 条");
        var translated = new ExportResult();

        var glossaryDict = LoadGlossaryDict(glossaries);

        // 过滤出待翻译词条，同时跳过已翻译的
        var toTranslate = FilterUntranslatedItems(exportResult, translated, progress, total, cancellationToken);
        if (toTranslate == null) return (false, translated);

        // 分批翻译
        var batchSize = Math.Max(1, config.BatchSize);
        for (var batchStart = 0; batchStart < toTranslate.Count; batchStart += batchSize)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Logger.Warn($"AI 翻译被用户取消，已翻译 {translated.SentenceList.Count} 条");
                return (false, translated);
            }

            var batch = toTranslate.Skip(batchStart).Take(batchSize).ToList();
            var batchTexts = batch.Select(b => b.Item.TextContent).ToList();
            var batchEnd = Math.Min(batchStart + batchSize, toTranslate.Count);
            Logger.Info($"正在翻译第 {batchStart + 1}-{batchEnd}/{toTranslate.Count} 批");

            var displayPrompt = BuildDisplayPrompt(config.CustomPrompt, batchTexts);

            // 调用 AI API 翻译
            var responseJson = await TranslateBatchAsync(batchTexts, glossaryDict, config, cancellationToken);

            if (responseJson == null)
            {
                Logger.Error($"AI 翻译第 {batchStart + 1}-{batchEnd}/{toTranslate.Count} 批请求失败");
                return (false, translated);
            }

            ProcessBatchResponse(translated, batch, responseJson);

            // 报告进度
            progress?.Report(new TranslationProgress
            {
                Current = translated.SentenceList.Count,
                Total = total,
                CurrentText = displayPrompt
            });
        }

        Logger.Info("AI 翻译完成");
        return (true, translated);
    }

    private static Dictionary<string, string> LoadGlossaryDict(List<string>? glossaries)
    {
        var glossaryDict = new Dictionary<string, string>();
        if (glossaries is { Count: > 0 })
        {
            var glossary = new Glossary(false);
            if (glossary.Load(glossaries))
            {
                glossaryDict = glossary.GetPhaseDict();
            }
        }

        return glossaryDict;
    }

    private static List<(int Index, ExportResult.Item Item)>? FilterUntranslatedItems(
        ExportResult exportResult, ExportResult translated,
        IProgress<TranslationProgress>? progress, int total,
        CancellationToken cancellationToken)
    {
        var toTranslate = new List<(int Index, ExportResult.Item Item)>();
        for (var i = 0; i < total; i++)
        {
            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Logger.Warn($"AI 翻译被用户取消，已翻译 {translated.SentenceList.Count} 条");
                    return null;
                }

                var item = exportResult.SentenceList[i];
                if (exportResult.TranslatedIds.Contains(item.GlobalId))
                {
                    translated.AddSentence(item.GlobalId, item.TextContent, item.FileName);
                    progress?.Report(new TranslationProgress
                    {
                        Current = i + 1,
                        Total = total,
                        CurrentText = item.TextContent + " (已翻译)"
                    });
                    continue;
                }

                toTranslate.Add((i, item));
            }
            catch (OperationCanceledException)
            {
                Logger.Warn($"AI 翻译被用户取消，已翻译 {translated.SentenceList.Count} 条");
                return null;
            }
        }

        return toTranslate;
    }

    private static string BuildDisplayPrompt(string customPrompt, List<string> batchTexts)
    {
        var data = new Dictionary<string, object>
        {
            ["sentences"] = batchTexts
        };
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        return customPrompt.Replace("{sentences}", json);
    }

    private static void ProcessBatchResponse(ExportResult translated,
        List<(int Index, ExportResult.Item Item)> batch, string responseJson)
    {
        var translatedTexts = ParseTranslatedSentences(responseJson, batch.Count);
        for (var j = 0; j < batch.Count; j++)
        {
            var (_, item) = batch[j];
            var dest = j < translatedTexts.Count ? translatedTexts[j] : item.TextContent;
            translated.AddSentence(item.GlobalId, dest, item.FileName);
        }
    }

    /// <summary>
    /// 调用 AI API 翻译一批句子
    /// </summary>
    private static async Task<string?> TranslateBatchAsync(
        List<string> sentences, Dictionary<string, string> glossaryDict,
        AiConfigData config, CancellationToken cancellationToken)
    {
        var requestData = new Dictionary<string, object>
        {
            ["glossaries"] = glossaryDict,
            ["sentences"] = sentences
        };
        var requestJson = JsonSerializer.Serialize(requestData, new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        var prompt = config.CustomPrompt.Replace("{sentences}", requestJson);

        try
        {
            return await SendChatRequestAsync(
                config.BaseUrl, config.ApiKey, config.ModelName,
                prompt, config.ReasoningEffort, config.EnableDeepThink, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.Error($"翻译批次请求失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 从 API 返回的 JSON 中解析翻译后的句子列表
    /// </summary>
    private static List<string> ParseTranslatedSentences(string responseJson, int expectedCount)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.TryGetProperty("sentences", out var sentencesElement))
            {
                var result = new List<string>();
                foreach (var element in sentencesElement.EnumerateArray())
                {
                    result.Add(element.GetString() ?? "");
                }
                return result;
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"解析翻译结果 JSON 失败: {ex.Message}");
        }

        return new List<string>();
    }
}
