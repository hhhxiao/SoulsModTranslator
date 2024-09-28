using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SMT.core;

public static class Utils
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    public static void SaveObjectAsJson<T>(T data, string fileName)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        var jsonUtf8Bytes = JsonSerializer.SerializeToUtf8Bytes(data, options);
        File.WriteAllBytes(fileName, jsonUtf8Bytes);
    }


    public static T? LoadJsonToObject<T>(string fileName)
    {
        try
        {
            var str = File.ReadAllText(fileName);
            return JsonSerializer.Deserialize<T>(str);
        }
        catch (Exception e)
        {
            Logger.Error($"无法读取文件：{fileName}", e);
            return default;
        }
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

    public static double GetChineseCharacterRatio(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0.0;

        var totalLength = text.Length;
        var chineseCharacterCount = Regex.Matches(text, @"[\u4e00-\u9fff]").Count;
        return (double)chineseCharacterCount / totalLength;
    }

    public static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Logger.Error($"Unable to open link: {ex.Message}");
        }
    }
}