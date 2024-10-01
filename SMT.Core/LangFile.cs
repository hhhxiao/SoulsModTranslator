using NLog;
using OpenCCNET;
using SoulsFormats;

namespace SMT.core;

public class LangFileSet
{
    private static readonly Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    public const long Mtid = 100000000;

    public static readonly List<string> BlackFileList = new()
    {
        "ToS_win64", //用户协议相关
        "TextEmbedImageName_win64", //键位相关
    };


    public enum InterLang
    {
        araAE, deuDE, engUS, fraFR, itaIT, jpnJP, korKR,
        polPL, porBR, rusRU, spaAR, spaES, thaTH,
        zhoCN, zhoTW
    }


    public Dictionary<string, BND4> Bnds = new();

    public bool Load(string langRootPath)
    {
        if (!Directory.Exists(langRootPath))
        {
            Logger.Error($"目录不存在：{langRootPath}");
            return false;
        }

        Logger.Info($"语言文件的目录为: {langRootPath}");
        var fmgFileIdSet = new HashSet<int>();
        //找到所有的bnd文件并编号
        var info = new DirectoryInfo(langRootPath);
        foreach (var bndFile in info.GetFiles())
        {
            if (!bndFile.Name.EndsWith(".msgbnd.dcx")) continue;
            Logger.Info($"发现msgbnd.dcx文件: {bndFile.Name}");
            if (bndFile.Name == "ngword.msgbnd.dcx") continue; //跳过这个ng单词
            try
            {
                var bnd = BND4.Read(Path.Combine(langRootPath, bndFile.Name));
                // if (bnd.Files.Any(fmgFile => !fmgFileIdSet.Add(fmgFile.ID)))
                // {
                //     Logger.Error($"发现重复的FMG语言文件，文件夹{langRootPath}中是否存在与{bndFile.Name}相同的dcx" +
                //                  $"文件(比如黑暗之魂III和艾尔登法环)请根据需求删除(或修改后缀名)其中一个");
                //     Logger.Error($"推荐做法为新建文件夹并将除了item_dlc02.msgbnd.dcx以及menu_dlc02.msgbnd.dcx之外的dcx文件移动到该文件夹内");
                //     return false;
                // }

                Bnds[bndFile.Name] = bnd;
            }
            catch (Exception e)
            {
                Logger.Error("不合法的dcx文件格式.\n\n" + e.Message);
                return false;
            }
        }

        return true;
    }


    public bool SaveTo(string folder)
    {
        if (!Directory.Exists(folder))
        {
            Logger.Error($"要写入的文件夹 {folder} 不存在");
            return false;
        }

        Logger.Info($"开始写入文件");
        foreach (var (bndName, bnd) in Bnds)
        {
            var path = Path.Combine(folder, bndName);
            bnd.Write(path);
            Logger.Info($"写入文件 {path}...");
        }

        Logger.Info("所有文件写入完成");
        return true;
    }

    public void ForeachEntryRead(Action<string, int, string, int> traverser)
    {
        foreach (var (bndName, bnd) in Bnds)
        {
            foreach (var f in bnd.Files)
            {
                var fmgFileName = Path.GetFileNameWithoutExtension(f.Name);
                if (BlackFileList.Contains(fmgFileName)) continue;
                var fmg = FMG.Read(f.Bytes);
                foreach (var entry in fmg.Entries)
                {
                    if (entry.Text != null)
                        traverser(fmgFileName, f.ID, entry.Text, entry.ID);
                }
            }
        }
    }


    public void ForeachEntryUpdate(Func<string, int, string, int, string?> replacer)
    {
        foreach (var (bndName, bnd) in Bnds)
        {
            foreach (var file in bnd.Files)
            {
                var fmg = FMG.Read(file.Bytes);
                foreach (var entry in fmg.Entries)
                {
                    if (entry.Text != null)
                        entry.Text = replacer(file.Name, file.ID, entry.Text, entry.ID);
                }
                file.Bytes = fmg.Write();
            }
        }
    }

    public void UpdateInterLang(string newLang)
    {
        var innerLang = GetIntnerLang();
        if (innerLang == null)
        {
            Logger.Warn("Inner lang of current lang file is unknown, can now switch to " + newLang);
            return;
        }

        foreach (var (bndName, bnd) in Bnds)
        {
            foreach (var file in bnd.Files)
            {
                file.Name = file.Name.Replace(innerLang, newLang);
                var fmg = FMG.Read(file.Bytes);
                file.Bytes = fmg.Write();
            }
        }
    }

    //N:\GR\data\INTERROOT_win64\msg\deuDE\TalkMsg.fmg er
    //N:\SPRJ\data\INTERROOT_ps4\msg\zhoTW\64bit\会話.fmg
    //只返回第一个
    public string? GetIntnerLang()
    {

        foreach (var (bndName, bnd) in Bnds)
        {
            foreach (var file in bnd.Files)
            {
                var pathTokens = file.Name.Split("\\");
                for (var i = 0; i < pathTokens.Length; i++)
                {
                    if (i < pathTokens.Length - 1 && pathTokens[i] == "msg")
                    {
                        return pathTokens[i + 1];
                    }
                }
            }
        }
        return null;
    }


    public static bool Dump(string input, string output)
    {
        var file = new LangFileSet();
        if (!file.Load(input))
        {
            return false;
        }

        //makedir
        foreach (var (bndName, bnd) in file.Bnds)
        {
            var bndPath = Path.Combine(output, bndName.Replace(".msgbnd.dcx", ""));
            Directory.CreateDirectory(bndPath);
            foreach (var f in bnd.Files)
            {
                var fmg = FMG.Read(f.Bytes);
                var str = "";
                foreach (var entry in fmg.Entries)
                {
                    if (entry.Text != null)
                    {
                        str += $"[{entry.ID}]\n{entry.Text}\n";
                    }
                }
                File.WriteAllText(Path.Combine(bndPath, Path.GetFileNameWithoutExtension(f.Name) + ".txt"), str);
            }
        }
        return true;
    }

    public static bool CNTWConvert(string interLangName, string srcPath, string descPath)
    {
        if (interLangName != "zhoTW" && interLangName != "zhoCN")
        {
            Logger.Error($"不合法的目标语言 {interLangName}");
            return false;
        }
        var langFile = new LangFileSet();
        if (!langFile.Load(srcPath))
        {
            Logger.Error($"不合法的语言文件 {srcPath}");
            return false;
        }
        langFile.UpdateInterLang(interLangName);
        ZhConverter.Initialize();
        langFile.ForeachEntryUpdate((string file, int fileId, string entry, int entryId) =>
        {
            if (interLangName == "zhoCN")
            {
                return ZhConverter.HantToHans(entry);
            }
            else if (interLangName == "zhoTW")
            {
                return ZhConverter.HansToHant(entry);
            }
            return null;
        });

        return langFile.SaveTo(descPath);
    }
}

