using System.Diagnostics;
using CommandLine;
using NLog;

[Verb("db", HelpText = "DataBase tools")]
public class DbOptions

{

    private static readonly Logger Logger = NLog.LogManager.GetCurrentClassLogger();


    [Option('s', "src", Required = true)]
    public string? SrcLangFile { get; set; }


    [Option('d', "dest", Required = true)]
    public string? DestLangFile { get; set; }


    [Option("deep", Default = false, HelpText = "split text as paragraph when create DB")]

    public bool? DeepLevel { get; set; }


    public static int Executor(DbOptions options)
    {
        return 0;
    }
}
