using NPOI.XSSF.UserModel;

namespace SMT.core;

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
                        result.Add((long)id, text.Replace("[@]", "\n"));
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
        list.Add("[0]");
        try
        {
            List<string> buffer = new List<string>();
            foreach (var rawLine in list)
            {
                var line = rawLine.Trim();
                if (line.StartsWith("|") && line.EndsWith("|"))
                {
                    string idStr = line;
                    if (line.Contains(","))
                    {
                        idStr = line.Trim().Split(",")[0];
                    }
                    var id = Convert.ToInt64(idStr.Replace("|", ""));
                    if (id != 0)
                    {
                        var text = string.Join("\n", buffer);
                        buffer.Clear();
                        res.Add(id, text.Replace("[@]", "\n"));
                    }
                }
                else
                {
                    buffer.Add(line);
                }
            }
        }
        catch (Exception e)
        {
            Logger.Error($"读取文本时出现错误: {e.Message}");
        }
        Logger.Info($"共读取到{res.Count}条文本");
        return res;
    }

    public static Dictionary<long, string> Import(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        if (ext.EndsWith("txt")) return ImportTxt(fileName);
        return ext.EndsWith("xlsx") ? ImportExcel(fileName) : new Dictionary<long, string>();
    }
}