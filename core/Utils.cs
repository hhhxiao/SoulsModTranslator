using System.IO;
using System.Text.Json;

namespace SoulsModTranslator.core;

public static class Utils
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    public static void SaveMapAsJson(Dictionary<string, string> map, string fileName)
    {
        var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var jsonUtf8Bytes = JsonSerializer.SerializeToUtf8Bytes(map, options);
        File.WriteAllBytes(fileName, jsonUtf8Bytes);
    }

    public static Dictionary<string, string> LoadJsonToMap(string fileName)
    {
        var str = File.ReadAllText(fileName);
        var res = JsonSerializer.Deserialize<Dictionary<string, string>>(str);
        if (res != null) return res;
        Logger.Info("Bad Json format" + fileName);
        return new Dictionary<string, string>();
    }

    public static void BackupFileOrDir(string path)
    {
        if (File.Exists(path))
        {
            File.Move(path, path + "." + new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds());
        }
        else if (Directory.Exists(path))
        {
            var newPath = path + "." + new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
            Directory.Move(path, newPath);
            Logger.Info($"备份: {path} -> {newPath}");
        }
    }
}