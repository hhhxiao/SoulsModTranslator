using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

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
        var dict = new Dictionary<string, string>();
        try
        {
            var str = File.ReadAllText(fileName);
            dict = JsonSerializer.Deserialize<Dictionary<string, string>>(str);
        }
        catch (Exception e)
        {
            Logger.Error($"无法读取Json文件：{fileName}", e);
        }
        return dict ?? new Dictionary<string, string>();
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

    static int CountChineseCharacters(string input)
    {
        System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"[\u4e00-\u9fff]");
        MatchCollection matches = regex.Matches(input);
        return matches.Count;
    }
    public static double GetChineseCharacterRatio(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0.0;

        int totalLength = text.Length;
        int chineseCharacterCount = Regex.Matches(text, @"[\u4e00-\u9fff]").Count;
        return (double)chineseCharacterCount / totalLength;
    }

    public static void OpenURL(string url)
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
