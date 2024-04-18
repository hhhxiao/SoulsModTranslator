using System.Text.RegularExpressions;
using System.Windows.Documents;
using System.Windows.Shapes;

namespace SoulsModTranslator.core;

public class Glossary
{
    private Regex? _regex = null;
    private IOrderedEnumerable<KeyValuePair<string, string>>? _orderedDict = null;

    public Glossary()
    {
    }

    public bool Load(IEnumerable<string> glossaryFileNames)
    {
        var phaseList = new Dictionary<string, string>();
        foreach (var dict in glossaryFileNames.Select(Utils.LoadJsonToMap))
        {
            foreach (var (key, value) in dict)
            {
                phaseList.TryAdd(key, value);
            }
        }

        _orderedDict = phaseList.OrderByDescending(kv => kv.Key.Length);
        var pattern = @"\b(" + string.Join("|", _orderedDict.Select(kv => Regex.Escape(kv.Key))) + @")\b";
        _regex = new Regex(pattern);
        return true;
    }

    private string ProcessOne(string input)
    {
        return _regex?.Replace(input, match =>
            {
                var key = match.Value;
                if (_orderedDict == null) return "[ERROR GLOSSARY]";
                var dictEntry = _orderedDict.FirstOrDefault(kv => key.Equals(kv.Key));
                return dictEntry.Value ?? key;
            }
        ) ?? "[ERROR GLOSSARY]";
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