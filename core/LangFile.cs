using System.Diagnostics;
using System.IO;
using NLog;
using SoulsFormats;

namespace SoulsModTranslator.core;

public class LangFile
{
    private static readonly Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    public const long Mtid = 1000000000;

    public static readonly List<string> BlackFileList = new()
    {
        "ToS_win64", //用户协议相关
        "TextEmbedImageName_win64", //键位相关
    };

    public BND4? ItemBnd { get; private set; }
    public BND4? MenuBnd { get; private set; }

    public Dictionary<string, BND4> Bnds = new();

    public bool Load(string langRootPath)
    {
        if (!Directory.Exists(langRootPath))
        {
            Logger.Error($"目录不存在： {langRootPath}");
            return false;
        }

        Logger.Info($"语言文件的目录为: {langRootPath}");
        var fmgFileIdSet = new HashSet<int>();
        //找到所有的bnd文件并编号
        var info = new DirectoryInfo(langRootPath);
        foreach (var bndFile in info.GetFiles())
        {
            if (!bndFile.Name.EndsWith(".msgbnd.dcx")) continue;
            Logger.Info($"发现 msgbnd.dcx 文件: {bndFile.Name}");
            if (bndFile.Name == "ngword.msgbnd.dcx") continue; //跳过这个ng单词

            try
            {
                var bnd = BND4.Read(Path.Combine(langRootPath, bndFile.Name));
                if (bnd.Files.Any(fmgFile => !fmgFileIdSet.Add(fmgFile.ID)))
                {
                    Logger.Error($"发现重复的FMG语言文件，文件夹{langRootPath}中是否存在与{bndFile.Name}相同的dcx" +
                                 $"文件(比如黑暗之魂III和艾尔登法环)请根据需求删除(或修改后缀名)其中一个");
                    Logger.Error($"推荐做法为新建文件夹并将除了item_dlc02.msgbnd.dcx以及item_dlc02.msgbnd.dcx之外的dcx文件移动到该文件夹内");
                    return false;
                }

                Bnds[bndFile.Name] = bnd;
            }
            catch (Exception e)
            {
                Logger.Error("不合法的dcx文件格式。 " + e.Message);
                return false;
            }
        }

        return true;
    }


    public bool Save(string folder)
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

    public void ForeachAllKey(Action<string, int, string, int> traverser)
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
}