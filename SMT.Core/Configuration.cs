namespace SMT.core;

public static class Configuration
{
    public const string SrcLangPath = "engus";
    public const string SrcLangInnerName = "engUS";

    public static string DestLangPath = "zhocn";
    public static string DestLangInnerName = "zhoCN";
    
    public static void UpdateDestLang(string path, string inner)
    {
        DestLangInnerName = inner;
        DestLangPath = path;
    }
}