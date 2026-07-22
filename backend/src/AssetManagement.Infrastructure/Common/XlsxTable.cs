using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using AssetManagement.Application.Common;

namespace AssetManagement.Infrastructure.Common;

internal static class XlsxTable
{
    private const int MaxArchiveEntries = 100;
    private const long MaxArchiveUncompressedBytes = 20 * 1024 * 1024;
    private const long MaxWorksheetBytes = 10 * 1024 * 1024;
    private const long MaxSharedStringsBytes = 5 * 1024 * 1024;
    private const int MaxRows = AppConstants.MaxImportRows + 1;
    private const int MaxColumns = 50;
    private const int MaxCellCharacters = 10_000;
    private const int MaxSharedStrings = MaxRows * MaxColumns;
    private static readonly XNamespace SheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public static byte[] Write(IEnumerable<string[]> rows)
        => Write(new[] { ("Sheet1", rows) });

    public static byte[] Write(IReadOnlyList<(string Name, IEnumerable<string[]> Rows)> sheets)
    {
        if (sheets.Count == 0) throw new ArgumentException("至少需要一个工作表", nameof(sheets));
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(zip, "[Content_Types].xml", BuildContentTypesXml(sheets.Count));
            WriteEntry(zip, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);
            WriteEntry(zip, "xl/workbook.xml", BuildWorkbookXml(sheets));
            WriteEntry(zip, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationshipsXml(sheets.Count));
            for (var index = 0; index < sheets.Count; index++)
                WriteEntry(zip, $"xl/worksheets/sheet{index + 1}.xml", BuildSheetXml(sheets[index].Rows));
        }

        return ms.ToArray();
    }

    private static string BuildContentTypesXml(int sheetCount)
    {
        XNamespace ns = "http://schemas.openxmlformats.org/package/2006/content-types";
        return new XDocument(new XDeclaration("1.0", "UTF-8", null),
            new XElement(ns + "Types",
                new XElement(ns + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(ns + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                new XElement(ns + "Override", new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                Enumerable.Range(1, sheetCount).Select(index =>
                    new XElement(ns + "Override", new XAttribute("PartName", $"/xl/worksheets/sheet{index}.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")))))
            .ToString(SaveOptions.DisableFormatting);
    }

    private static string BuildWorkbookXml(IReadOnlyList<(string Name, IEnumerable<string[]> Rows)> sheets)
    {
        XNamespace relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        return new XDocument(new XDeclaration("1.0", "UTF-8", null),
            new XElement(SheetNs + "workbook",
                new XAttribute(XNamespace.Xmlns + "r", relationships),
                new XElement(SheetNs + "sheets", sheets.Select((sheet, index) =>
                    new XElement(SheetNs + "sheet",
                        new XAttribute("name", sheet.Name),
                        new XAttribute("sheetId", index + 1),
                        new XAttribute(relationships + "id", $"rId{index + 1}"))))))
            .ToString(SaveOptions.DisableFormatting);
    }

    private static string BuildWorkbookRelationshipsXml(int sheetCount)
    {
        XNamespace ns = "http://schemas.openxmlformats.org/package/2006/relationships";
        return new XDocument(new XDeclaration("1.0", "UTF-8", null),
            new XElement(ns + "Relationships", Enumerable.Range(1, sheetCount).Select(index =>
                new XElement(ns + "Relationship",
                    new XAttribute("Id", $"rId{index}"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                    new XAttribute("Target", $"worksheets/sheet{index}.xml")))))
            .ToString(SaveOptions.DisableFormatting);
    }

    public static List<List<string>> Read(Stream stream)
    {
        try
        {
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            ValidateArchive(zip);
            var sharedStrings = ReadSharedStrings(zip);
            var entry = zip.GetEntry("xl/worksheets/sheet1.xml")
                ?? throw new BizException(4001, "Excel 文件缺少工作表");
            var doc = ReadXml(entry, MaxWorksheetBytes, "工作表");
            var rows = doc.Descendants(SheetNs + "row").Take(MaxRows + 1).ToList();
            if (rows.Count > MaxRows)
            {
                throw new BizException(4153, $"Excel 文件不能超过 {AppConstants.MaxImportRows} 行数据");
            }
            return rows.Select(row => ReadRow(row, sharedStrings)).ToList();
        }
        catch (BizException)
        {
            throw;
        }
        catch (Exception ex) when (ex is InvalidDataException or XmlException or IOException or UnauthorizedAccessException)
        {
            throw new BizException(4001, "Excel 文件格式无效或已损坏");
        }
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
            if (columnIndex >= MaxColumns)
            {
                throw new BizException(4153, $"Excel 文件不能超过 {MaxColumns} 列");
            }

            while (values.Count <= columnIndex)
            {
                values.Add("");
            }

            var value = ReadCell(cell, sharedStrings);
            if (value.Length > MaxCellCharacters)
            {
                throw new BizException(4153, $"单元格内容不能超过 {MaxCellCharacters} 个字符");
            }
            values[columnIndex] = value;
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

        var doc = ReadXml(entry, MaxSharedStringsBytes, "共享字符串");
        var items = doc.Descendants(SheetNs + "si").Take(MaxSharedStrings + 1).ToList();
        if (items.Count > MaxSharedStrings)
        {
            throw new BizException(4153, "Excel 文件的共享字符串数量过多");
        }
        var result = items
            .Select(item => string.Concat(item.Descendants(SheetNs + "t").Select(x => x.Value)))
            .ToList();
        if (result.Any(x => x.Length > MaxCellCharacters))
        {
            throw new BizException(4153, $"单元格内容不能超过 {MaxCellCharacters} 个字符");
        }
        return result;
    }

    private static void ValidateArchive(ZipArchive zip)
    {
        if (zip.Entries.Count > MaxArchiveEntries)
        {
            throw new BizException(4153, "Excel 文件包含过多压缩条目");
        }

        long totalLength = 0;
        foreach (var entry in zip.Entries)
        {
            totalLength = checked(totalLength + entry.Length);
            if (totalLength > MaxArchiveUncompressedBytes)
            {
                throw new BizException(4153, "Excel 文件解压后体积过大");
            }
            if (entry.Length > 1024 * 1024
                && entry.Length / Math.Max(entry.CompressedLength, 1) > 100)
            {
                throw new BizException(4153, "Excel 文件压缩比异常");
            }
        }
    }

    private static XDocument ReadXml(ZipArchiveEntry entry, long maxBytes, string partName)
    {
        if (entry.Length > maxBytes)
        {
            throw new BizException(4153, $"Excel {partName}体积过大");
        }

        using var stream = entry.Open();
        using var limited = new BoundedReadStream(stream, maxBytes);
        using var reader = XmlReader.Create(limited, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            MaxCharactersInDocument = maxBytes,
            XmlResolver = null
        });
        return XDocument.Load(reader, LoadOptions.None);
    }

    private sealed class BoundedReadStream(Stream inner, long maxBytes) : Stream
    {
        private long _read;
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _read; set => throw new NotSupportedException(); }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = inner.Read(buffer, offset, count);
            _read += read;
            if (_read > maxBytes) throw new InvalidDataException("解压内容超过安全上限");
            return read;
        }
        public override int Read(Span<byte> buffer)
        {
            var read = inner.Read(buffer);
            _read += read;
            if (_read > maxBytes) throw new InvalidDataException("解压内容超过安全上限");
            return read;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
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
            if (index > MaxColumns)
            {
                return MaxColumns;
            }
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
