using NLog;
using SoulsFormats;

namespace SMT.core;

/**

- ID划分规则


FS文件的组织架构
- File1
    entry1(一段完整的文本，中间用空行隔开)
        p1
        p2
        p3
    entry2
    entry3
    ...

- File2
    entry1
    entry2
    entry3
    ...

- File3
    entry1
    entry2
    entry3
    ...

文件自身的ID(局部)    -> fileID 根据文件名自动生成
entry的ID(局部)     -> entryID 语言文件内部自带
段落的ID(局部)      -> paraID 按照顺序1,2,3,4,... 段落ID是0时表示p1+p2...这一整个entry内的一段完整文本
段落的ID(全局)      -> globalID =  fileID * MTID  +  (entryID*10 + paraID)

**/
public class ExportResult
{
    public struct Item
    {
        public long GlobalId { get; init; }
        public string TextContent { get; init; }
        public string FileName { get; init; }
    }

    public bool Success = false;
    public List<Item> SentenceList = new();

    public void AddSentence(long id, string text, string file)
    {
        SentenceList.Add(new Item { GlobalId = id, TextContent = text, FileName = file });
    }
}

public class EntryCache
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    private readonly long _globalEntryId; //(= fileid  * 10000000 + entryID)
    private readonly SortedDictionary<long, string> _textList = new();

    public EntryCache(long globalEntryId)
    {
        _globalEntryId = globalEntryId;
    }

    public void AddParagraph(long globalParaId, string text)
    {
        if (!_textList.TryAdd(globalParaId - _globalEntryId * 10, text.Trim()))
        {
            Logger.Warn($"出现重复的句子: {text}");
        }
    }

    public string Collect()
    {
        if (_textList.Count == 0) return "";
        return _textList.TryGetValue(0, out var collect) ? collect : string.Join("\n\n", _textList.Values);
    }
}

public static class Translator
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    private static Action<string, int, string, int> CreateTraverser(
        DataBase dataBase, //db
        bool keepAsText, //keep as text
        Action<string, long, long, string> unTrans, //action for untranslated text
        /**
        string  filename
        long    globalEntryId
        long    globalParaID
        long    text
        */
        Action<string, long, long, string, string> trans //action for translated text
    /**
    string  fileName
    long    gloablEntryID
    long    globalParaID
    string  srcText
    string  destText
    */
    )
    {
        return (string fileName, int fileId, string entryText, int entryId) =>
        {
            var globalEntryId = (long)fileId * LangFileSet.Mtid + (long)entryId;
            var res = dataBase.Translate(entryText.Trim(), entryId); //
            if (res.Key) //匹配成功
            {
                trans(fileName, globalEntryId, globalEntryId * 10, entryText, res.Value);
                return;
            }

            //匹配不成功
            if (keepAsText) //不进行下一步翻译
            {
                unTrans(fileName, globalEntryId, globalEntryId * 10, entryText);
                return;
            }

            var paraList = entryText.Split("\n\n");
            for (var i = 0; i < paraList.Length; i++)
            {
                //globalParaID = globalEntryId * 10 + paraID
                res = dataBase.Translate(paraList[i].Trim(), entryId);
                if (res.Key)
                {
                    trans(fileName, globalEntryId, globalEntryId * 10 + (i + 1), paraList[i], res.Value);
                }
                else
                {
                    unTrans(fileName, globalEntryId, globalEntryId * 10 + (i + 1), paraList[i]);
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
        var langFile = new LangFileSet();
        var db = new DataBase();
        if (!langFile.Load(Path.Combine(rootPath, Configuration.SrcLangPath)) || !db.Load(dbPath))
        {
            Logger.Error("无法加载语言文件或者数据库文件，导出终止");
            return result;
        }

        result.Success = true;
        var set = new HashSet<long>();
        langFile.ForeachEntryRead(CreateTraverser(db, keepAsText,
            (fileName, globalEntryId, globalParaId, src) =>
            {
                result.AddSentence(globalParaId, src, fileName);
                set.Add(globalEntryId);
            },
            (fileName, globalEntryId, globalParaId, src, dest) => { set.Add(globalEntryId); }));
        Logger.Info($"共处理 {set.Count} 段文本");
        Logger.Info("成功生成未翻译文本");
        return result;
    }

    public static bool Translate(string rootPath, string dbPath,
        string[] translateFileNames,
        bool keepText,
        bool multiLang,
        bool useTrand
    )
    {
        Logger.Info($"开始生成目标语言文件");
        Logger.Info($"msg根目录：{rootPath}");
        Logger.Info($"数据库路径：{dbPath}");
        Logger.Info($"翻译文件路径：");
        Logger.Info($"导出为繁体：{useTrand}");
        Logger.Info($"双语模式：{multiLang}");
        var langFile = new LangFileSet();
        var db = new DataBase();
        if (!langFile.Load(Path.Combine(rootPath, Configuration.SrcLangPath)) || !db.Load(dbPath))
        {
            Logger.Error("无法读取语言文件/数据库文件，生成终止");
            return false;
        }

        var translated = new Dictionary<long, string>();
        foreach (var file in translateFileNames)
        {
            var dict = TextImporter.Import(file);
            Logger.Info($" - 从 {file} 中读取 {dict.Count}条文本");
            foreach (var kv in dict)
            {
                translated.TryAdd(kv.Key, kv.Value);
            }
        }

        var translateCache = new Dictionary<long, EntryCache>();
        Logger.Info($"已成功加载翻译文件, {translated.Count}条文本");
        langFile.ForeachEntryRead(CreateTraverser(db, keepText,
            (fileName, globalEntryId, globalParaId, src) =>
            {
                if (!translated.ContainsKey(globalParaId))
                {
                    Logger.Error($"无法翻译文本: {src}");
                }

                var dest = translated.GetValueOrDefault(globalParaId, src); //尝试翻译
                translateCache.TryAdd(globalEntryId, new EntryCache(globalEntryId));
                translateCache[globalEntryId].AddParagraph(globalParaId, dest);
            }, //
            (f, globalEntryId, globalParaId, src, dest) =>
            {
                translateCache.TryAdd(globalEntryId, new EntryCache(globalEntryId));
                translateCache[globalEntryId].AddParagraph(globalParaId, dest);
            }));
        Logger.Info($"共处理 {translateCache.Count} 段文本");


        //Generation
        var destPath = Path.Combine(rootPath, Configuration.DestLangPath);

        langFile.ForeachEntryUpdate((string fileName, int fileId, string entry, int entryId) =>
        {
            if (LangFileSet.BlackFileList.Contains(Path.GetFileNameWithoutExtension(fileName))) return entry;
            var globalEntryId = (long)fileId * LangFileSet.Mtid + (long)entryId;
            if (translateCache.TryGetValue(globalEntryId, out var dest))
            {
                if (multiLang)
                {
                    return dest.Collect() + "\n" + entry;
                }

                return dest.Collect();
            }

            Logger.Warn($"缺失文本翻译: {entryId}->{entry}");
            return null;
        });
        langFile.UpdateInterLang(Configuration.DestLangInterName);
        Logger.Info("尝试备份原有的语言文件");
        Utils.BackupFileOrDir(destPath);
        Directory.CreateDirectory(destPath);
        langFile.SaveTo(destPath);
        return true;
    }
}