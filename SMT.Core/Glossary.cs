using System.Collections;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using NLog;

namespace SMT.core;

public class Glossary
{
    private class RegexMatchRule
    {
        public Regex? Regex;
        public string Replacement = "";
    };

    private static readonly Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    private Regex? _phaseRegex;
    private IOrderedEnumerable<KeyValuePair<string, string>>? _orderedPhaseDict;

    private readonly List<RegexMatchRule> _normalRegexList = new();

    //TODO: 分两类，支持正则和字符串

    private readonly bool _ignoreCase;

    public Glossary(bool caseInsensitive)
    {
        _ignoreCase = caseInsensitive;
        Logger.Debug($"忽视术语表大小写：{_ignoreCase}");
    }

    public bool Load(IEnumerable<string> glossaryFileNames)
    {
        var phaseKv = new Dictionary<string, string>();
        var regexKv = new Dictionary<string, string>();

        //遍历每一个术语表软件
        foreach (var glossaryFile in glossaryFileNames)
        {
            try
            {
                Logger.Info($"开始读取术语表{glossaryFile}");
                var str = File.ReadAllText(glossaryFile);
                var dict = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(str)
                           ?? new Dictionary<string, Dictionary<string, string>>();
                Logger.Debug(dict.Count);
                //读取短语表
                if (dict.TryGetValue("phases", out var phaseDict))
                {
                    foreach (var kv in phaseDict)
                    {
                        //如果忽略大小写，就统一使用小写字母来构建正则，方便之后做替换
                        //否则使用原样
                        var key = this._ignoreCase ? kv.Key.ToLower() : kv.Key;
                        phaseKv.Add(key, kv.Value);
                    }
                }

                //读取正则表
                if (dict.TryGetValue("regex", out var value))
                {
                    foreach (var kv in value)
                    {
                        regexKv.TryAdd(kv.Key, kv.Value);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error($"无法读取读取术语表文件{glossaryFile}", e);
            }
        }


        //排序，优先替换更长的短语组
        _orderedPhaseDict = phaseKv.OrderByDescending(kv => kv.Key.Length); //先匹配字符串
        //构建字符替换的正则,\b(p1|p2|p3....)按照单词边界匹配，排序是为了优先匹配长的
        var phasePattern = @"\b(" + string.Join("|", _orderedPhaseDict.Select(kv => Regex.Escape(kv.Key))) + @")\b";
        this._phaseRegex = new Regex(phasePattern, _ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
        //构建普通正则短语组
        try
        {
            foreach (var rule in regexKv.Select(regexString => new RegexMatchRule
                     {
                         Regex = new Regex(regexString.Key, _ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None),
                         Replacement = regexString.Value,
                     }))
            {
                _normalRegexList.Add(rule);
            }
        }
        catch (Exception e)
        {
            Logger.Info("无法初始化正则表达式: " + e.Message);
        }

        Logger.Info($"共发现{phaseKv.Count} 个短语以及 {_normalRegexList.Count}个正则表达式");
        Logger.Debug($"{phaseKv.ToArray()[0].Key}");
        return true;
    }

    private string ProcessOne(string input)
    {
        //优先进行单词序列替换
        var output = this._phaseRegex?.Replace(input, match =>
            {
                var key = match.Value;
                if (this._ignoreCase) key = key.ToLower(); //如果是忽视大小写模式，则根据小写进行查询，这里和上面的做统一
                if (_orderedPhaseDict == null) return key;
                var dictEntry = _orderedPhaseDict.FirstOrDefault(kv => key.Equals(kv.Key));
                return dictEntry.Value ?? "[ERROR KEY]";
            }
        ) ?? input;
        //然后遍历每一个正则进行替换和匹配
        output = _normalRegexList.Aggregate(output,
            (current, rule) => rule.Regex?.Replace(current ?? input, match => rule.Replacement) ?? string.Empty);
        return output;
    }

    public ExportResult Process(ExportResult result)
    {
        var res = new ExportResult();
        foreach (var item in result.SentenceList)
        {
            res.AddSentence(item.GlobalId, ProcessOne(item.TextContent), item.FileName);
        }

        return res;
    }
}