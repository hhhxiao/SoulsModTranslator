using System.IO;
using NLog;
using NPOI.SS.Formula.Functions;
using NPOI.XSSF.UserModel;

namespace SMT.core;

public static class TextExporter
{


    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    // private static (List<KeyValuePair<long, List<int>>>, List<KeyValuePair<string, int>>) ReIndex(List<ExportResult.Item> l, string splitor)
    // {
    //     var indexList = new Dictionary<long, List<int>>();
    //     var eList = new List<ExportResult.Item>();

    //     var eSet = new Dictionary<string, int>(); //词元表
    //     foreach (var item in l)
    //     {
    //         var index = new List<int>();
    //         var text = item.Text;
    //         var sp = text.Split(splitor);
    //         foreach (var e in sp)
    //         {
    //             if (e.Length == 0) continue;
    //             if (eSet.TryGetValue(e, out var eIdx)) //存在词元
    //             {
    //                 index.Add(eIdx);
    //             }
    //             else
    //             {
    //                 index.Add(eSet.Count); //没有就插入
    //                 eSet.Add(e, eSet.Count);
    //             }
    //         }

    //         indexList.Add(item.Id, index);
    //     }

    //     //
    //     var res1 = indexList.ToList().OrderBy(o => o.Key).ToList();
    //     var res2 = eSet.ToList().OrderBy(o => o.Value).ToList();
    //     return (res1, res2);
    // }

    // private static void CompressedExport(Stream file, ExportResult exportResult)
    // {
    //     //sheet 0 文本元素
    //     //sheet 1 单词元素
    //     //sheet 2 文本索引
    //     //sheet 3 单词索引
    //     var (sentenceIndex, sentenceEle) = ReIndex(exportResult.SentenceList, ".");
    //     var (phaseIndex, phaseEle) = ReIndex(exportResult.SentenceList, " ");

    //     var book = new XSSFWorkbook();
    //     var s0 = book.CreateSheet("0");
    //     var s1 = book.CreateSheet("1");
    //     var s2 = book.CreateSheet("2");
    //     var s3 = book.CreateSheet("3");

    //     for (var i = 0; i < sentenceEle.Count; i++)
    //     {
    //         var row = s0.CreateRow(i);
    //         row.CreateCell(0).SetCellValue(sentenceEle[i].Key);
    //         row.CreateCell(1).SetCellValue(sentenceEle[i].Value);
    //     }

    //     for (var i = 0; i < phaseEle.Count; i++)
    //     {
    //         var row = s1.CreateRow(i);
    //         row.CreateCell(0).SetCellValue(phaseEle[i].Key);
    //         row.CreateCell(1).SetCellValue(phaseEle[i].Value);
    //     }

    //     for (var i = 0; i < sentenceIndex.Count; i++)
    //     {
    //         var row = s2.CreateRow(i);
    //         row.CreateCell(0).SetCellValue(sentenceIndex[i].Key);
    //         for (var j = 0; j < sentenceIndex[i].Value.Count; j++)
    //         {
    //             row.CreateCell(j + 1).SetCellValue(sentenceIndex[i].Value[j]);
    //         }
    //     }

    //     for (var i = 0; i < phaseIndex.Count; i++)
    //     {
    //         var row = s3.CreateRow(i);
    //         row.CreateCell(0).SetCellValue(phaseIndex[i].Key);
    //         for (var j = 0; j < phaseIndex[i].Value.Count; j++)
    //         {
    //             row.CreateCell(j + 1).SetCellValue(phaseIndex[i].Value[j]);
    //         }
    //     }

    //     book.Write(file);
    // }


    private static void ExcelRawExport(Stream file, ExportResult exportResult, bool markSource, bool replaceNewLine)
    {
        var book = new XSSFWorkbook();
        var s0 = book.CreateSheet("0");
        var index = 0;
        foreach (var item in exportResult.SentenceList)
        {
            var row = s0.CreateRow(index);
            row.CreateCell(0).SetCellValue(item.globalId);
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
        using (StreamWriter writer = new StreamWriter(fileName))
        {
            foreach (var kvp in exportResult.SentenceList)
            {
                var id = $"|{kvp.globalId}|";
                var content = kvp.TextContent;
                if (replaceNewLine)
                {
                    content = content.Replace("\n", "[@]");
                }
                if (markSource)
                {
                    id = $"|{kvp.globalId},{kvp.FileName}|";
                }
                writer.WriteLine($"{id}");
                writer.WriteLine(content);
            }
        }
        Logger.Info($"共导出{exportResult.SentenceList.Count}条未翻译文本");
    }




    public static bool Export(string fileName, ExportResult exportResult, bool excel, bool resort, bool markSource, bool repalceNewLine, bool compressed)
    {
        Logger.Info("开始导出：" + fileName);
        Logger.Info($" - 导出句子条数:  {exportResult.SentenceList.Count}");
        Logger.Info(" - 是否导出为Excel:  " + excel);
        Logger.Info(" - 是否重排:  " + resort);
        Logger.Info(" - 是否标注文本来源:  " + markSource);
        Logger.Info(" - 是否替换换行符:  " + repalceNewLine);
        Logger.Info(" - 是否压缩存储（未实装）:  " + compressed);

        if (resort)
        {
            exportResult.SentenceList = exportResult.SentenceList.OrderByDescending(item => Utils.GetChineseCharacterRatio(item.TextContent))
            .ThenBy(item => item.globalId).ToList();
        }

        if (!excel)
        {
            TextExport(fileName, exportResult, markSource, repalceNewLine);
            return true;
        }

        var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write);
        ExcelRawExport(fs, exportResult, markSource, repalceNewLine);
        fs.Close();
        return true;
    }
}
