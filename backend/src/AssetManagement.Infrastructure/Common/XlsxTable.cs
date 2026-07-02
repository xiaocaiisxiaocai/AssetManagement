using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using AssetManagement.Application.Common;

namespace AssetManagement.Infrastructure.Common;

internal static class XlsxTable
{
    private static readonly XNamespace SheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public static byte[] Write(IEnumerable<string[]> rows)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(zip, "[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                </Types>
                """);
            WriteEntry(zip, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);
            WriteEntry(zip, "xl/workbook.xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets><sheet name="Sheet1" sheetId="1" r:id="rId1"/></sheets>
                </workbook>
                """);
            WriteEntry(zip, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                </Relationships>
                """);
            WriteEntry(zip, "xl/worksheets/sheet1.xml", BuildSheetXml(rows));
        }

        return ms.ToArray();
    }

    public static List<List<string>> Read(Stream stream)
    {
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var sharedStrings = ReadSharedStrings(zip);
        var entry = zip.GetEntry("xl/worksheets/sheet1.xml")
            ?? throw new BizException(4001, "Excel 文件缺少工作表");
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        var doc = XDocument.Parse(reader.ReadToEnd());
        return doc.Descendants(SheetNs + "row")
            .Select(row => ReadRow(row, sharedStrings))
            .ToList();
    }

    private static List<string> ReadRow(XElement row, IReadOnlyList<string> sharedStrings)
    {
        var values = new List<string>();
        foreach (var cell in row.Elements(SheetNs + "c"))
        {
            var columnIndex = ColumnIndex((string?)cell.Attribute("r"));
            if (columnIndex < 0)
            {
                columnIndex = values.Count;
            }

            while (values.Count <= columnIndex)
            {
                values.Add("");
            }

            values[columnIndex] = ReadCell(cell, sharedStrings);
        }

        return values;
    }

    private static List<string> ReadSharedStrings(ZipArchive zip)
    {
        var entry = zip.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return new List<string>();
        }

        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        var doc = XDocument.Parse(reader.ReadToEnd());
        return doc.Descendants(SheetNs + "si")
            .Select(item => string.Concat(item.Descendants(SheetNs + "t").Select(x => x.Value)))
            .ToList();
    }

    private static string ReadCell(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        var inlineText = cell.Element(SheetNs + "is")?.Element(SheetNs + "t")?.Value;
        if (inlineText is not null)
        {
            return inlineText;
        }

        var value = cell.Element(SheetNs + "v")?.Value ?? "";
        if ((string?)cell.Attribute("t") == "s"
            && int.TryParse(value, out var sharedStringIndex)
            && sharedStringIndex >= 0
            && sharedStringIndex < sharedStrings.Count)
        {
            return sharedStrings[sharedStringIndex];
        }

        return value;
    }

    private static int ColumnIndex(string? cellRef)
    {
        if (string.IsNullOrWhiteSpace(cellRef))
        {
            return -1;
        }

        var index = 0;
        foreach (var ch in cellRef)
        {
            if (!char.IsLetter(ch))
            {
                break;
            }

            index = index * 26 + char.ToUpperInvariant(ch) - 'A' + 1;
        }

        return index == 0 ? -1 : index - 1;
    }

    private static string BuildSheetXml(IEnumerable<string[]> rows)
    {
        var sheetRows = rows.Select((cells, rowIndex) => new XElement(SheetNs + "row",
            new XAttribute("r", rowIndex + 1),
            cells.Select((cell, colIndex) => new XElement(SheetNs + "c",
                new XAttribute("r", $"{ColumnName(colIndex + 1)}{rowIndex + 1}"),
                new XAttribute("t", "inlineStr"),
                new XElement(SheetNs + "is", new XElement(SheetNs + "t", cell))))));
        return new XDocument(new XDeclaration("1.0", "UTF-8", null),
            new XElement(SheetNs + "worksheet", new XElement(SheetNs + "sheetData", sheetRows))).ToString(SaveOptions.DisableFormatting);
    }

    private static void WriteEntry(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string ColumnName(int index)
    {
        var name = "";
        while (index > 0)
        {
            index--;
            name = (char)('A' + index % 26) + name;
            index /= 26;
        }

        return name;
    }
}
