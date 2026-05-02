using System.Text.Json;
using NLog;

namespace SMT.core;

public class AiConfigData
{
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string ModelName { get; set; } = "";
    public string ReasoningEffort { get; set; } = "medium";
    public bool EnableDeepThink { get; set; }
    public string CustomPrompt { get; set; } = "";
}

public static class AiConfig
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly string ConfigPath = Path.Combine(Directory.GetCurrentDirectory(), "ai_config.json");

    public static void Save(AiConfigData data)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
        Logger.Info($"AI 配置已保存至 {ConfigPath}");
    }

    public static AiConfigData Load()
    {
        if (!File.Exists(ConfigPath))
        {
            Logger.Info("AI 配置文件不存在，使用默认值");
            return new AiConfigData();
        }

        try
        {
            var json = File.ReadAllText(ConfigPath);
            var data = JsonSerializer.Deserialize<AiConfigData>(json);
            if (data == null)
            {
                Logger.Warn("AI 配置文件解析失败，使用默认值");
                return new AiConfigData();
            }
            Logger.Info("AI 配置已加载");
            return data;
        }
        catch (Exception ex)
        {
            Logger.Error($"AI 配置文件读取失败: {ex.Message}");
            return new AiConfigData();
        }
    }
}
