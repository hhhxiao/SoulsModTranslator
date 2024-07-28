using System.Diagnostics;
using System.IO;
using System.Net.Http.Headers;
using NLog;
using NPOI.SS.Formula;
using NPOI.SS.Formula.Functions;
using SoulsFormats;

namespace SMT.core;


/**

文件ID entryID

textID  = 文件ID * 100000 + entryID
globalID = textID  * 10 + ?

**/

public class ExportResult
{
    public struct Item
    {
        public long globalId { get; init; }
        public string TextContent { get; init; }
        public string FileName { get; init; }
    }
    public bool Success = false;
    public List<Item> SentenceList = new();
    public void AddSentence(long id, string text, string file)
    {
        SentenceList.Add(new Item { globalId = id, TextContent = text, FileName = file });
    }
}


public class TextCache
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    public long TextId; //(= fileid  * 10000000 + entryID)
    private readonly SortedDictionary<long, string> textList = new();

    public TextCache(long textId)
    { TextId = textId; }

    public void AddParagraphOrText(long globalId, string text)
    {
        if (!textList.TryAdd(globalId - TextId * 10, text.Trim()))
        {
            Logger.Warn($"出现重复的句子: {text}");
        }
    }

    public string Collect()
    {
        if (textList.Count == 0) return "";
        if (textList.ContainsKey(0)) return textList[0];
        return string.Join("\n\n", textList.Values);
    }
}

public static class Translator
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    private static Action<string, int, string, int> CreateTraverser(
        DB db, //db
        bool keepAsText, //keep as text
        Action<string, long, long, string> unTrans,  //action for untranslated text
        Action<string, long, long, string, string> trans //action for translated text
        )
    {
        return (string fileName, int fileId, string text, int textId) =>
        {
            var globalID = (long)fileId * LangFile.Mtid + (long)textId;
            var res = db.Translate(text.Trim()); //
            if (res.Key) //匹配成功
            {
                trans(fileName, globalID, globalID * 10, text, res.Value);
                return;
            }
            //匹配不成功
            if (keepAsText) //不进行下一步翻译
            {
                unTrans(fileName, globalID, globalID * 10, text);
                return;
            }

            var paraList = text.Split("\n\n");
            for (var i = 0; i < paraList.Length; i++)
            {
                //段落ID = 文本ID + 段落序号
                res = db.Translate(paraList[i].Trim());
                if (res.Key)
                {
                    trans(fileName, globalID, globalID * 10 + (i + 1), paraList[i], res.Value);
                }
                else
                {
                    unTrans(fileName, globalID, globalID * 10 + (i + 1), paraList[i]);
                }
            }
        };
    }

    public static ExportResult Export(string rootPath, string dbPath, bool keepAsText)
    {
        Logger.Info($"开始导出未翻译文本");
        Logger.Info($"msg根目录：{rootPath}");
        Logger.Info($"数据库路径：{dbPath}");
        Logger.Info($"保持原始文本不分割：{keepAsText}");
        var result = new ExportResult
        {
            Success = false
        };
        var langFile = new LangFile();
        var db = new DB();
        if (!langFile.Load(Path.Combine(rootPath, Configuration.SrcLangPath)) || !db.Load(dbPath))
        {
            Logger.Error("无法加载语言文件或者数据库文件，导出终止");
            return result;
        }
        result.Success = true;
        var set = new HashSet<long>();
        langFile.ForeachAllKey(CreateTraverser(db, keepAsText,
            (f, textId, globalId, src) =>
            {
                result.AddSentence(globalId, src, f);
                set.Add(textId);
            },
            (f, textId, globalId, scr, dest) =>
            {
                set.Add(textId);
            }));
        Logger.Info($"共处理 {set.Count} 段文本");
        Logger.Info("成功生成未翻译文本");
        return result;
    }

    public static bool Translate(string rootPath, string dbPath, string translateFileName, bool keepText)
    {
        Logger.Info($"开始生成目标语言文件");
        Logger.Info($"msg根目录：{rootPath}");
        Logger.Info($"数据库路径：{dbPath}");
        Logger.Info($"翻译文件路径：{translateFileName}");
        Logger.Info($"保持原始文本不分割：{keepText}");
        var langFile = new LangFile();
        var db = new DB();
        if (!langFile.Load(Path.Combine(rootPath, Configuration.SrcLangPath)) || !db.Load(dbPath))
        {
            Logger.Error("无法读取语言文件/数据库文件，生成终止");
            return false;
        }

        var translated = TextImporter.Import(translateFileName);
        var translateCache = new Dictionary<long, TextCache>();
        Logger.Info("已成功加载翻译文件");
        langFile.ForeachAllKey(CreateTraverser(db, keepText,
            (fileName, textId, globalId, src) =>
            {
                if (!translated.ContainsKey(globalId))
                {
                    Logger.Error($"无法翻译文本: {src}");
                }
                var dest = translated.GetValueOrDefault(globalId, src); //尝试翻译
                translateCache.TryAdd(textId, new TextCache(textId));
                translateCache[textId].AddParagraphOrText(globalId, dest);
            }, //
            (f, textId, globalId, scr, dest) =>
            {
                translateCache.TryAdd(textId, new TextCache(textId));
                translateCache[textId].AddParagraphOrText(globalId, dest);
            }));
        Logger.Info($"共处理 {translateCache.Count} 段文本");

        var zhocnPath = Path.Combine(rootPath, Configuration.DestLangPath);
        foreach (var bnd in langFile.Bnds)
        {
            Logger.Info($"开始生成文件：{Path.Join(zhocnPath, bnd.Key)} ");
            foreach (var file in bnd.Value.Files)
            {
                //replace name form engus to zhocn
                var newName = file.Name.Replace(Configuration.SrcLangInnerName, Configuration.DestLangInnerName);
                var fileName = Path.GetFileNameWithoutExtension(newName);
                file.Name = newName;
                if (LangFile.BlackFileList.Contains(fileName)) continue; //不翻译
                //read fmg and replace
                var fmg = FMG.Read(file.Bytes);
                foreach (var entry in fmg.Entries)
                {
                    if (entry.Text == null) continue;
                    if (translateCache.TryGetValue(file.ID * LangFile.Mtid + entry.ID, out var dest))
                    {
                        entry.Text = dest.Collect();
                    }
                    else
                    {
                        Logger.Warn($"缺失文本翻译: {entry.ID}->{entry.Text}");
                    }
                }
                file.Bytes = fmg.Write();
            }
        }
        Logger.Info("尝试备份原有的语言文件");
        Utils.BackupFileOrDir(zhocnPath);
        Directory.CreateDirectory(zhocnPath);
        langFile.Save(zhocnPath);
        return true;
    }
}