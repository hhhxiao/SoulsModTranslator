namespace SMT.Console;
using CommandLine;

public class SoulsModTranslator
{





    static int Main(string[] args)
    {
        return CommandLine.Parser.Default.ParseArguments<DumpOptions, DbOptions>(args).MapResult(
            (DumpOptions options) => DumpOptions.Executor(options),
            (DbOptions options) => DbOptions.Executor(options),
            errs => 1
        );

    }
    static void RunOptions(Options opts)
    {
        //handle options
    }

    static void HandleParseError(IEnumerable<Error> errs)
    {
        //handle errors
    }

}



