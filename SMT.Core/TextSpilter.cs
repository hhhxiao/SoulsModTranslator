public class TextSpilter
{
    public enum Type
    {
        BY_TEXT,
        BY_PARAGRAPH,
        BY_LINE,
        BY_END
    }



    public static void Do(string src_text, string dest_text, Action<string, string> action, Type type = Type.BY_PARAGRAPH)
    {
        action(src_text, dest_text);
        if (type == Type.BY_TEXT) return;
        var srcParaList = src_text.Split("\n\n");
        var destParaList = dest_text.Split("\n\n");
        if (srcParaList.Length != destParaList.Length) return;
        if (srcParaList.Length == 1 || destParaList.Length == 1) return;
        for (var i = 0; i < srcParaList.Length; i++)
        {
            var srcPara = srcParaList[i];
            var destPara = destParaList[i];
            action(srcPara, destPara);
        }
        // if (type == Type.BY_TEXT) { action(src_text, dest_text); return; }




    }
};