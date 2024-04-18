using System.IO;
using Microsoft.VisualBasic.Logging;
using NPOI.XSSF.UserModel;

namespace SoulsModTranslator.core;

public class TextImporter
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    private static Dictionary<long, string> ImportExcel(string fileName)
    {
        var result = new Dictionary<long, string>();
        try
        {
            var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            var workbook = new XSSFWorkbook(fs);
            var sheet = workbook.GetSheetAt(0);
            if (sheet != null)
            {
                for (var i = 0; i <= sheet.LastRowNum; i++)
                {
                    var curRow = sheet.GetRow(i);
                    if (curRow == null)
                    {
                        Logger.Warn($"在Excel {fileName}中发现空行{i}");
                        continue;
                    }

                    var id = curRow.GetCell(0).NumericCellValue;
                    var text = curRow.GetCell(1).StringCellValue.Trim();
                    if (!result.ContainsKey((long)id))
                    {
                        result.Add((long)id, text);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Logger.Error("无法读取Excel: " + fileName + "\n\n" + e.Message);
        }

        return result;
    }

    private static Dictionary<long, string> ImportTxt(string fileName)
    {
        var res = new Dictionary<long, string>();
        var list = File.ReadLines(fileName).ToList();
        foreach (var line in list)
        {
            try
            {
                var sp = line.Split("||");
                var id = Convert.ToInt64(sp[0]);
                res.Add(id, sp[1]);
            }
            catch (Exception)
            {
                Logger.Error($"Invalid Translated line: {line}");
            }
        }

        return res;
    }

    public static Dictionary<long, string> Import(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        if (ext.EndsWith("txt")) return ImportTxt(fileName);
        return ext.EndsWith("xlsx") ? ImportExcel(fileName) : new Dictionary<long, string>();
    }
}