using System.IO;

namespace SMT.core;

public class DB
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    private Dictionary<string, string> _data = new();

    public bool Load(string dbPath)
    {
        if (!File.Exists(dbPath))
        {
            Logger.Error($"数据库文件{dbPath}不存在");
            return false;
        }

        _data = Utils.LoadJsonToMap(dbPath);
        return true;
    }

    public KeyValuePair<bool, string> Translate(string key)
    {
        if (key == "[ERROR]") return new KeyValuePair<bool, string>(true, "[ERROR]");
        return _data.TryGetValue(key, out var value)
            ? new KeyValuePair<bool, string>(true, value)
            : new KeyValuePair<bool, string>(false, "");
    }
}