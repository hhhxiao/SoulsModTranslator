using CommandLine;
[Verb("export", HelpText = "Export untranslated text to file")]
class ExportOptions
{

    [Option('g', "glossary", Required = false, HelpText = "Input glossaries to be used.")]
    public IEnumerable<string>? InputFiles { get; set; }

}