using System.Collections;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Documents;
using System.Windows.Shapes;
using NLog;

namespace SoulsModTranslator.core;

public class Glossary
{

    // private static Dictionary<string, string> LoadGlossary(string fileName)
    // {
    //     var dict = new Dictionary<string, string>();
    //     try
    //     {
    //         var str = File.ReadAllText(fileName);
    //         dict = JsonSerializer.Deserialize<Dictionary<string, string>>(str);
    //     }
    //     catch (Exception e)
    //     {
    //         Logger.Error($"无法读取Json文件{fileName}", e);
    //     }
    //     return dict ?? new Dictionary<string, string>();
    // }



    class RegexMatchRule
    {

        public Regex? Regex = null;
        public string Replacement = "";

    };
    private static readonly Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    private Regex? _phaseRegex = null;
    private IOrderedEnumerable<KeyValuePair<string, string>>? orderedPhaseDict = null;

    private List<RegexMatchRule> _NormalRegexList = new List<RegexMatchRule>();
    //TODO: 分两类，支持正则和字符串

    public bool Load(IEnumerable<string> glossaryFileNames)
    {


        var phaseKV = new Dictionary<string, string>();
        var regexKV = new Dictionary<string, string>();

        foreach (var glossaryFile in glossaryFileNames)
        {
            try
            {
                Logger.Info($"开始读取词汇表{glossaryFile}");
                var str = File.ReadAllText(glossaryFile);
                var dict = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(str)
                 ?? new Dictionary<string, Dictionary<string, string>>();
                Logger.Debug(dict.Count);
                if (dict.ContainsKey("phases"))
                {
                    var phaseDict = dict["phases"];
                    foreach (var kv in phaseDict)
                    {
                        phaseKV.TryAdd(kv.Key, kv.Value);
                    }

                }

                if (dict.ContainsKey("regex"))
                {
                    var regexDict = dict["regex"];
                    foreach (var kv in regexDict)
                    {
                        regexKV.TryAdd(kv.Key, kv.Value);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error($"无法读取读取词汇表文件{glossaryFile}", e);
            }
        }


        //build regex
        orderedPhaseDict = phaseKV.OrderByDescending(kv => kv.Key.Length); //先匹配字符串
        //string regex
        var phasePattern = @"\b(" + string.Join("|", orderedPhaseDict.Select(kv => Regex.Escape(kv.Key))) + @")\b";
        this._phaseRegex = new Regex(phasePattern);
        //normal regex
        try
        {
            foreach (var regexString in regexKV)
            {
                var rule = new RegexMatchRule
                {
                    Regex = new Regex(regexString.Key),
                    Replacement = regexString.Value,
                };
                _NormalRegexList.Add(rule);
            }
        }
        catch (Exception e)
        {
            Logger.Info("无法初始化正则表达式: " + e.Message);
        }
        Logger.Info($"共发现{phaseKV.Count} 个短语以及 {_NormalRegexList.Count}个正则表达式");
        return true;
    }

    private string ProcessOne(string input)
    {

        var output = this._phaseRegex?.Replace(input, match =>
            {
                var key = match.Value;
                if (orderedPhaseDict == null) return key;
                var dictEntry = orderedPhaseDict.FirstOrDefault(kv => key.Equals(kv.Key));
                return dictEntry.Value ?? "[ERROR KEY]";
            }
        ) ?? input;
        foreach (var rule in _NormalRegexList)
        {
            output = rule.Regex?.Replace(output ?? input, match =>
            {
                return rule.Replacement;
            });
        }
        return output ?? input;
    }

    public ExportResult Process(ExportResult result)
    {
        var res = new ExportResult();
        foreach (var item in result.PhaseList)
        {
            res.AddPhase(item.Id, ProcessOne(item.Text));
        }

        foreach (var item in result.SentenceList)
        {
            res.AddSentence(item.Id, ProcessOne(item.Text));
        }
        return res;
    }
}