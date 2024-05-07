using System.Diagnostics;
using System.IO;
using System.Windows.Forms.Design;
using NLog;
using NPOI.SS.Formula;
using NPOI.SS.Formula.Functions;
using SoulsFormats;

namespace SoulsModTranslator.core;

public class ExportResult
{
    public struct Item
    {
        public long Id { get; init; }
        public string Text { get; init; }
    }

    public bool Success = false;
    public readonly List<Item> PhaseList = new();
    public readonly List<Item> SentenceList = new();

    public static readonly List<string> PhaseFileList = new()
    {
        "ArtsName", //战技名字
        "GemName", //战灰名字
        "ProtectionName", //盔甲名字
        "WeaponName" //武器名字
    };

    public void AddPhase(long id, string text)
    {
        PhaseList.Add(new Item { Id = id, Text = text });
    }

    public void AddSentence(long id, string text)
    {
        SentenceList.Add(new Item { Id = id, Text = text });
    }
}

public class TextTree
{

    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    private readonly Dictionary<long, string> Sentences = new();
    public long TextId; //文本ID (= 文件ID * 100000000 + textID)

    public TextTree(long textId)
    {
        this.Reset(textId);
    }

    private string Get(int id, string sp)
    {
        if (Sentences.TryGetValue(id, out var sentence))
            return sentence;

        if (id % 10 != 0) return ""; //用\n分隔的句子，没有就是没有了
        var tmp = new List<string>();
        for (var i = 1; i <= 9; i++)
        {
            tmp.Add(Get(id == 0 ? i * 10 : id + i, "\n"));
        }

        return string.Join(sp, tmp).Trim();
    }

    public void AddSentence(long id, string sentence)
    {
        if (Sentences.TryGetValue(id, out var oldSentence))
        {
            Logger.Warn($"文本{id}内出现重复句子: {oldSentence} -> {sentence}");
        }
        else
        {
            Sentences.Add(id, sentence); //an item with same key
        }
    }

    public string Collect()
    {
        return Get(0, "\n\n");
    }


    public void Reset(long textId)
    {
        Sentences.Clear();
        TextId = textId;
    }
}

public static class Translator
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    private static Action<string, int, string, int> CreateTraverser(DB db, Action<long, long, string> unTrans, Action<long, long, string, string> trans)
    {
        return (string fileName, int fileId, string text, int textId) =>
        {
            var uid = (long)fileId * LangFile.Mtid + (long)textId;
            var res = db.Translate(text.Trim()); //
            if (res.Key)
            {
                trans(uid, uid * 100, text, res.Value);
                return;
            }

            var paraList = text.Split("\n\n");
            for (var i = 0; i < paraList.Length; i++)
            {
                res = db.Translate(paraList[i].Trim());
                if (res.Key)
                {
                    trans(uid, uid * 100 + (i + 1) * 10, paraList[i], res.Value);
                    continue;
                }

                var sentenceList = paraList[i].Split("\n"); //翻译失败，尝试分句
                for (var j = 0; j < sentenceList.Length; j++)
                {
                    res = db.Translate(sentenceList[j].Trim());
                    if (res.Key)
                    {
                        trans(uid, uid * 100 + (i + 1) * 10 + j + 1, sentenceList[j], res.Value);
                    }
                    else
                    {
                        unTrans(uid, uid * 100 + (i + 1) * 10 + j + 1, sentenceList[j]);
                    }
                }
            }
        };
    }

    public static ExportResult Export(string rootPath, string dbPath)
    {
        Logger.Info($"开始导出未翻译文本，msg根目录:{rootPath}，数据库路径:{dbPath}");
        var result = new ExportResult
        {
            Success = false
        };
        var langFile = new LangFile();
        var db = new DB();
        if (!langFile.Load(Path.Combine(rootPath, "engus")) || !db.Load(dbPath))
        {
            Logger.Error("无法加载语言文件或者数据库文件，导出终止");
            return result;
        }

        result.Success = true;
        langFile.ForeachAllKey(CreateTraverser(db,
            (tId, id, src) => { result.AddSentence(id, src); },
            (tId, id, scr, dest) => { }));
        Logger.Info("成功生成未翻译文本");
        return result;
    }

    public static bool Translate(string rootPath, string dbPath, string translateFileName)
    {
        Logger.Info($"开始生成目标语言文件，msg根目录:{rootPath}\n - 数据库路径:{dbPath}，翻译文件路径:{translateFileName}");
        var langFile = new LangFile();
        var db = new DB();
        if (!langFile.Load(Path.Combine(rootPath, "engus")) || !db.Load(dbPath))
        {
            Logger.Error("无法读取语言文件/数据库文件，生成终止");
            return false;
        }

        var translated = TextImporter.Import(translateFileName);
        var textDict = new Dictionary<long, string>();
        var tree = new TextTree(-1);
        Logger.Info("已成功加载翻译文件");
        langFile.ForeachAllKey(CreateTraverser(db,
            (uid, sentenceId, src) =>
            {
                if (!translated.ContainsKey(sentenceId))
                {
                    Logger.Error($"无法翻译文本: {src}");
                }

                var dest = translated.GetValueOrDefault(sentenceId, src);
                if (uid != tree.TextId)
                {
                    //id不一样，缓存并创建一个新的
                    textDict.TryAdd(tree.TextId, tree.Collect());
                    tree.Reset(uid);
                }

                tree.AddSentence(sentenceId % (uid * 100), dest);
            }, //
            (uid, sentenceId, scr, dest) =>
            {
                if (uid != tree.TextId)
                {
                    textDict.TryAdd(tree.TextId, tree.Collect());
                    tree.Reset(uid);
                }

                tree.AddSentence(sentenceId % (uid * 100), dest);
            }));
        Logger.Info($"共翻译 {textDict.Count} 段文本");
        var zhocnPath = Path.Combine(rootPath, "zhocn");
        foreach (var bnd in langFile.Bnds)
        {
            Logger.Info($"开始生成文件 {Path.Join(zhocnPath, bnd.Key)} ");
            foreach (var file in bnd.Value.Files)
            {
                //replace name form engus to zhocn
                var newName = file.Name.Replace("engUS", "zhoCN");
                var fileName = Path.GetFileNameWithoutExtension(newName);
                file.Name = newName;

                if (LangFile.BlackFileList.Contains(fileName)) continue; //不翻译

                //read fmg and replace
                var fmg = FMG.Read(file.Bytes);
                foreach (var entry in fmg.Entries)
                {
                    if (entry.Text == null) continue;
                    if (textDict.TryGetValue(file.ID * LangFile.Mtid + entry.ID, out var dest))
                    {
                        entry.Text = dest;
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