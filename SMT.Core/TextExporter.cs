using System.IO;
using NLog;
using NPOI.SS.Formula.Functions;
using NPOI.XSSF.UserModel;
using SoulsFormats;

namespace SMT.core;

public static class TextExporter
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    public static readonly int MaxFileLine = 50000;

    private static void ExcelRawExport(Stream file, ExportResult exportResult, int beginIndex, int count,
        bool replaceNewLine)
    {
        Logger.Debug($"导出序号[{beginIndex}, {count})");
        var book = new XSSFWorkbook();
        var s0 = book.CreateSheet("0");
        var index = 0;
        foreach (var item in exportResult.SentenceList.GetRange(beginIndex, count))
        {
            var row = s0.CreateRow(index);
            row.CreateCell(0).SetCellValue(item.GlobalId);
            var content = item.TextContent;
            if (replaceNewLine)
            {
                content = content.Replace("\n", "[@]");
            }

            row.CreateCell(1).SetCellValue(content);
            index++;
        }

        Logger.Info($"共导出{exportResult.SentenceList.Count}条未翻译文本");
        book.Write(file);
    }

    private static void TextExport(string fileName, ExportResult exportResult, bool markSource, bool replaceNewLine)
    {
        using (var writer = new StreamWriter(fileName))
        {
            foreach (var kvp in exportResult.SentenceList)
            {
                var id = $"|{kvp.GlobalId}|";
                var content = kvp.TextContent;
                if (replaceNewLine)
                {
                    content = content.Replace("\n", "[@]");
                }

                if (markSource)
                {
                    id = $"|{kvp.GlobalId},{kvp.FileName}|";
                }

                writer.WriteLine($"{id}");
                writer.WriteLine(content);
            }
        }

        Logger.Info($"共导出{exportResult.SentenceList.Count}条未翻译文本");
    }


    public static bool Export(string fileName, ExportResult exportResult, bool excel, bool resort, bool markSource,
        bool replaceNewLine, bool compressed, int maxLine)
    {
        Logger.Info("开始导出：" + fileName);
        Logger.Info($" - 导出句子条数:  {exportResult.SentenceList.Count}");
        Logger.Info(" - 是否导出为Excel:  " + excel);
        Logger.Info(" - 是否重排:  " + resort);
        Logger.Info(" - 是否标注文本来源:  " + markSource);
        Logger.Info(" - 是否替换换行符:  " + replaceNewLine);
        Logger.Info(" - 是否压缩存储（未实装）:  " + compressed);
        Logger.Info(" - 文本最大行数:  " + maxLine);

        if (resort)
        {
            exportResult.SentenceList = exportResult.SentenceList
                .OrderByDescending(item => Utils.GetChineseCharacterRatio(item.TextContent))
                .ThenBy(item => item.GlobalId).ToList();
        }

        if (!excel)
        {
            TextExport(fileName, exportResult, markSource, replaceNewLine);
            return true;
        }

        var linePerFile = maxLine;
        linePerFile = Math.Min(linePerFile, exportResult.SentenceList.Count);

        var beginIndex = 0;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var directoryPath = Path.GetDirectoryName(fileName) ?? string.Empty;
        var extension = Path.GetExtension(fileName);
        var fileIdx = 0;
        while (true)
        {
            var realFileName = $"{fileNameWithoutExtension}_{fileIdx}{extension}";
            if (linePerFile == exportResult.SentenceList.Count)
            {
                realFileName = $"{fileNameWithoutExtension}{extension}";
            }
            Logger.Debug($"Export File: {realFileName}");
            var fs = new FileStream(Path.Combine(directoryPath, realFileName), FileMode.Create, FileAccess.Write);
            var count = Math.Min(linePerFile, exportResult.SentenceList.Count - beginIndex);
            ExcelRawExport(fs, exportResult, beginIndex, count, replaceNewLine);
            fs.Close();
            beginIndex += linePerFile;
            fileIdx++;
            if (beginIndex >= exportResult.SentenceList.Count) break;
        }

        return true;
    }
}