namespace SMT.Console;
using System.IO;
using NLog;
using NPOI.SS.Formula.Functions;
using SMT.core;

public class SoulsModTranslator
{


    static int Main(string[] args)
    {
        // var langFile = new LangFileSet();
        // string path = @"C:\Users\xhy\games\ERMods\ConvergenceER\Convergence\msg\zhocn";
        // langFile.Load(path);
        // langFile.ForeachEntryUpdate((string fn, int fid, string text, int eid) =>
        // {
        //     return "😅";
        // });

        // langFile.SaveTo(path);

        var path = @"C:\Users\xhy\dev\SoulsModTranslator\vanilla\EldenRing\zhotw";

        var langFile = new LangFileSet();
        if (langFile.Load(path))
        {
            System.Console.WriteLine(langFile.GetInnerLang());
        }
        return 0;
    }
}



