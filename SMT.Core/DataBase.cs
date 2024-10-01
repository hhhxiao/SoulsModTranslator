using System.Text.Json;

namespace SMT.core;

//添加和查询时都会删除前后空白字符
public class DataBase
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    private readonly Dictionary<string, Dictionary<int, string>> _dbData = new();

    public void Save(string dbPath)
    {
        var simple = new Dictionary<string, string>();
        var complex = new Dictionary<string, Dictionary<int, string>>();
        foreach (var (k, v) in _dbData)
        {
            if (v.Count == 1)
            {
                simple.Add(k, v.Values.First());
            }
            else
            {
                complex.Add(k, v);
            }
        }

        var toSave = new Dictionary<string, object>();
        foreach (var (k, v) in simple)
        {
            toSave.Add(k, v);
        }

        foreach (var (k, v) in complex)
        {
            toSave.Add(k, v);
        }

        DebugPrint();
        Utils.SaveObjectAsJson(toSave, dbPath);
    }

    public void AddKey(string srcText, string destText, int refId)
    {
        srcText = srcText.Trim();
        destText = destText.Trim();
        if (!_dbData.TryGetValue(srcText, out Dictionary<int, string>? value))
        {
            var key = new Dictionary<int, string> { { refId, destText } };
            _dbData[srcText] = key;
        }
        else
        {
            if (value.TryAdd(refId, destText)) return;
            var old = value[refId];
            if (old != destText)
            {
                Logger.Warn($"生成数据库时出现了冲突的key: {old} <=> {destText}");
            }
        }
    }

    public bool Load(string dbPath)
    {
        Logger.Info($"加载数据库文件 {dbPath}");
        if (!File.Exists(dbPath))
        {
            Logger.Error($"数据库文件{dbPath}不存在");
            return false;
        }

        var toSave = Utils.LoadJsonToObject<Dictionary<string, JsonElement>>(dbPath)
                     ?? new Dictionary<string, JsonElement>();
        Logger.Debug($"{toSave.Count}");
        foreach (var (k, v) in toSave)
        {
            switch (v.ValueKind)
            {
                case JsonValueKind.String:
                    _dbData.Add(k, new Dictionary<int, string> { { -1, v.GetString() ?? "" } });
                    break;
                case JsonValueKind.Object:
                    {
                        var subDict = new Dictionary<int, string>();
                        foreach (var property in v.EnumerateObject())
                        {
                            subDict[int.Parse(property.Name)] = property.Value.GetString() ?? "";
                        }

                        _dbData.TryAdd(k, subDict);
                        break;
                    }
                case JsonValueKind.Undefined:
                case JsonValueKind.Array:
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                case JsonValueKind.Null:
                default:
                    break;
            }
        }

        DebugPrint();
        return true;
    }

    public KeyValuePair<bool, string> Translate(string key, int refId = -1)
    {
        key = key.Trim();
        //inline
        if (key == "[ERROR]") return new KeyValuePair<bool, string>(true, "[ERROR]");
        if (_dbData.TryGetValue(key, out var item))
        {
            var res = item.TryGetValue(refId, out var value) ? value : item.First().Value;
            return new KeyValuePair<bool, string>(true, res);
        }
        else
        {
            return new KeyValuePair<bool, string>(false, "");
        }
    }

    private void DebugPrint()
    {
        Logger.Debug($"总共 {_dbData.Count} 条源语言数据");
        var size = _dbData.Sum(item => item.Value.Count);
        Logger.Debug($"总共 {size} 条目标语言数据");
    }

    ///=======================================Static methods below=========================================
    private static Dictionary<string, string> ReadLangFileToKv(LangFileSet langFile)
    {
        var dict = new Dictionary<string, string>();
        langFile.ForeachEntryRead((fName, fId, text, tId) => { dict.TryAdd($"{fName}|{tId}", text); });
        return dict;
    }


    private static void TrySplitTraverse(string key, string src, string dest, string sp, DataBase dataBase)
    {
        var id = int.Parse(key.Split("|")[1]);
        dataBase.AddKey(src, dest, id);
        var srcList = src.Split(sp);
        var destList = dest.Split(sp); //切分
        if (srcList.Length == 1 || destList.Length == 1) return; //只有一句，直接返回
        if (srcList.Length != destList.Length) return; //中英行数不一样就返回
        for (var i = 0; i < srcList.Length; i++)
        {
            dataBase.AddKey(srcList[i].Trim(), destList[i].Trim(), id);
        }
    }

    public static bool CreateDb(string srcPath, string destPath, string savePath)
    {
        var srcLang = new LangFileSet();
        var destLang = new LangFileSet();
        if (!srcLang.Load(srcPath) || !destLang.Load(destPath))
        {
            Logger.Error("无法读取语言文件，创建数据库失败");
            return false;
        }

        var sourceDict = ReadLangFileToKv(srcLang);
        var destDict = ReadLangFileToKv(destLang);
        Logger.Info($"从源语言中读取{sourceDict.Count}条文本，从目标语言中读取{destDict.Count}条文本");

        var db = new DataBase();
        foreach (var (key, srcText) in sourceDict)
        {
            if (!destDict.TryGetValue(key, out var destText)) continue;
            if ((srcText is "[ERROR]" or "%null") || (destText is "[ERROR]" or "%null")) continue;
            TrySplitTraverse(key, srcText, destText, "\n\n", db);
        }

        db.Save(savePath);
        Logger.Info($"已生成对照数据库：{savePath}");
        return true;
    }

    public static bool MergeDataBase(string[] fileNames, string savePath)
    {
        Logger.Info($"合并以下数据库为：{savePath}");
        var merged = new DataBase();
        foreach (var file in fileNames)
        {
            var db = new DataBase();
            db.Load(file);
            foreach (var (src, value) in db._dbData)
            {
                foreach (var (id, dest) in value)
                {
                    merged.AddKey(src, dest, id);
                }
            }
        }
        merged.Save(savePath);
        return true;
    }
}