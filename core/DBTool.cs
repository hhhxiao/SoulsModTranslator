using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using NLog;

namespace SoulsModTranslator.core;

public static class DbTool
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    private static Dictionary<string, string> ReadKeyValue(LangFile langFile)
    {
        var dict = new Dictionary<string, string>();
        langFile.ForeachAllKey((fName, fId, text, tId) => { dict.TryAdd($"{fName}_{tId}", text); });
        return dict;
    }

    private static void TrySplitKv(string src, string dest, string sp, Dictionary<string, string> kv)
    {
        var srcParagraphList = src.Split(sp);
        var destParagraphList = dest.Split(sp); //切分
        if (srcParagraphList.Length == 1 || destParagraphList.Length == 1) return; //只有一句，直接返回
        if (srcParagraphList.Length != destParagraphList.Length) return; //中英行数不一样就返回
        for (var i = 0; i < srcParagraphList.Length; i++)
        {
            //每一段一一对应
            kv.TryAdd(srcParagraphList[i].Trim(), destParagraphList[i].Trim());
            TrySplitKv(srcParagraphList[i], destParagraphList[i], "\n", kv); //以句子为单位再递归一遍
        }
    }

    public static bool CreateDb(string srcPath, string destPath, string savePath)
    {
        var srcLang = new LangFile();
        var destLang = new LangFile();
        if (!srcLang.Load(srcPath) || !destLang.Load(destPath))
        {
            Logger.Error("无法读取语言文件，创建数据库失败");
            return false;
        }

        var sourceDict = ReadKeyValue(srcLang);
        var destDict = ReadKeyValue(destLang);
        Logger.Info($"从源语言中读取{sourceDict.Count}个文本，从目标语言中读取{destDict.Count}个文本");
        var dbDict = new Dictionary<string, string>();
        foreach (var (key, srcValue) in sourceDict)
        {
            if (!destDict.TryGetValue(key, out var destValue)) continue;
            if ((srcValue is "[ERROR]" or "%null") || (destValue is "[ERROR]" or "%null")) continue;
            dbDict.TryAdd(srcValue.Trim(), destValue.Trim()); //add full
            TrySplitKv(srcValue, destValue, "\n\n", dbDict);
        }

        Utils.SaveMapAsJson(dbDict, savePath);
        Logger.Info($"已生成对照数据库：{savePath}");
        return true;
    }


    public static bool MergeDB(string[] fileNames, string savePath)
    {
        Logger.Info($"合并以下数据库为：{savePath}");
        var newDB = new Dictionary<string, string>();
        var count = 0;
        foreach (var file in fileNames)
        {
            var db = Utils.LoadJsonToMap(file);
            Logger.Info($" - {file}: {db.Count}");
            count += db.Count;
            foreach (var kv in db)
            {
                if (!newDB.ContainsKey(kv.Key))
                {
                    newDB.Add(kv.Key, kv.Value);
                }
            }
        }

        Logger.Info($"{count}->{newDB.Count}");
        Utils.SaveMapAsJson(newDB, savePath);
        return true;
    }
}