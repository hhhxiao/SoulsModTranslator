namespace SMT.core;

public static class Configuration
{
    public static string SrcLangPath = "engus";
    public static string SrcLangInnerName = "engUS";

    public static string DestLangPath = "zhocn";
    public static string DestLangInnerName = "zhoCN";

    public static void UpdateDestLang(string path, string inner)
    {
        DestLangInnerName = inner;
        DestLangPath = path;
    }


}