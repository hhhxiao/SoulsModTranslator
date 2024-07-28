using System.Security.Cryptography;
using CommandLine;
using NLog;
using SMT.core;
using SoulsFormats;

[Verb("dump", HelpText = "dump *.msgbnd.dcx file to specific directory")]
public class DumpOptions
{

    private static readonly Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    [Option('i', "input", Required = true, HelpText = "Input file to be processed.")]
    public string InputFile { get; set; }

    [Option('o', "output", Required = true, HelpText = "Output Directory")]
    public string OutputDir { get; set; }


    public static int Executor(DumpOptions opts)
    {
        //handle options
        var input = opts.InputFile;
        var output = opts.OutputDir;

        Logger.Debug($"{input}");
        Logger.Debug($"{output}");
        LangFile file = new LangFile();
        file.Load(input);

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
        return 0;
    }
}

