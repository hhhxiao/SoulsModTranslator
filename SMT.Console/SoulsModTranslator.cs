namespace SMT.Console;
using System.IO;
using SMT.core;

public class SoulsModTranslator
{


    static int Main(string[] args)
    {
        // var db = new DataBase();
        // db.Load(@"C:\Users\xhy\dev\SoulsModTranslator\SMT.WPF\db\eldenring.json");
        // db.Save(@"C:\Users\xhy\dev\SoulsModTranslator\SMT.WPF\db\eldenring_save.json");
        var langFile = new LangFile();
        langFile.Load(@"C:\Users\xhy\dev\SoulsModTranslator\vanilla\EldenRing\zhotw");
        System.Console.WriteLine(langFile.InterLangName());
        return 0;
    }
}



