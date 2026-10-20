using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using AssetManagement.Application.TestMaterials;
using AssetManagement.Application.Common;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Infrastructure.TestMaterials;

internal static class ProjectReportBuilder
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace ContentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";

    public static byte[] BuildDocx(ProjectReportData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteXml(archive, "[Content_Types].xml", BuildContentTypes());
            WriteXml(archive, "_rels/.rels", BuildPackageRelationships());
            WriteXml(archive, "docProps/core.xml", BuildCoreProperties(data));
            WriteXml(archive, "docProps/app.xml", BuildAppProperties());
            WriteXml(archive, "word/document.xml", BuildDocument(data));
            WriteXml(archive, "word/styles.xml", BuildStyles());
            WriteXml(archive, "word/_rels/document.xml.rels", BuildDocumentRelationships());
        }

        return stream.ToArray();
    }

    public static string BuildHtml(ProjectReportData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var project = data.Project ?? new TestProjectDto();
        var stages = OrderedStages(data);
        var materials = (data.Materials ?? new List<TestMaterialDto>())
            .OrderBy(x => x.MaterialNo, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Id)
            .ToList();
        var documents = OrderedDocuments(data, stages);
        var followups = (data.Followups ?? new List<TestProjectFollowupDto>())
            .OrderBy(x => x.DueDate)
            .ThenBy(x => x.FilledAt)
            .ToList();
        var flows = (data.Flows ?? new List<MaterialFlowDto>())
            .OrderBy(x => x.ApplyTime)
            .ThenBy(x => x.Id)
            .ToList();
        var title = ReportTitle(data);

        var html = new StringBuilder(32_768);
        html.Append("<!doctype html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\">")
            .Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
            .Append("<meta name=\"generator\" content=\"部门资产管理系统\">")
            .Append("<meta name=\"generated-at\" content=\"")
            .Append(H(FormatDateTime(data.GeneratedAt)))
            .Append("\"><title>").Append(H(title)).Append("</title>")
            .Append("<style>")
            .Append("@page{size:A4;margin:18mm 16mm}*{box-sizing:border-box}body{margin:0 auto;max-width:210mm;color:#1f2937;background:#fff;font:14px/1.65 'Microsoft YaHei','PingFang SC',sans-serif}")
            .Append("h1{text-align:center;font-size:26px;margin:0 0 8px}h2{font-size:20px;border-bottom:2px solid #374151;padding-bottom:5px;margin:28px 0 12px}h3{font-size:17px;margin:22px 0 8px}p{margin:6px 0}.meta{text-align:center;color:#6b7280;margin-bottom:24px}")
            .Append("table{width:100%;border-collapse:collapse;margin:8px 0 18px;table-layout:fixed}th,td{border:1px solid #9ca3af;padding:7px 8px;text-align:left;vertical-align:top;overflow-wrap:anywhere;white-space:pre-wrap}th{background:#eef2f7;font-weight:700}.label{width:14%;background:#f8fafc;font-weight:700}.empty{color:#6b7280;font-style:italic}.long-text{white-space:pre-wrap;overflow-wrap:anywhere;border-left:3px solid #d1d5db;padding:8px 10px;background:#f9fafb}.attachment{color:#1d4ed8;text-decoration:underline}.stage{break-before:page;page-break-before:always}.stage.first{break-before:auto;page-break-before:auto}.avoid-break{break-inside:avoid;page-break-inside:avoid}.report-list a{overflow-wrap:anywhere}@media print{body{max-width:none}.stage{break-before:page;page-break-before:always}.stage.first{break-before:auto;page-break-before:auto}a{color:inherit;text-decoration:none}}")
            .Append("</style></head><body>");

        html.Append("<h1>").Append(H(title)).Append("</h1><p class=\"meta\">生成时间：")
            .Append(H(FormatDateTime(data.GeneratedAt))).Append("</p>");

        AppendHtmlHeading(html, "一、项目基本资料");
        AppendHtmlPropertyTable(html,
            ("项目编号", project.Code), ("项目名称", project.Name),
            ("项目类型", project.ProjectTypeLabel ?? project.ProjectTypeCode), ("负责人", project.OwnerName),
            ("开始日期", FormatDate(project.StartDate)), ("计划完成", FormatDate(project.PlannedFinishDate)),
            ("结案日期", FormatDate(project.ClosedDate)), ("项目进度", project.ProgressLabel ?? project.ProgressCode),
            ("跟进间隔", $"{project.FollowUpIntervalDays} 天"), ("下次跟进", FormatDate(project.NextFollowUpDueDate)),
            ("跟进状态", project.FollowUpStatus), ("创建时间", FormatDateTime(project.CreatedAt)));
        AppendHtmlLongText(html, "测试情况", project.TestStatus);
        AppendHtmlLongText(html, "最近跟进", project.LatestFollowUpContent);

        AppendHtmlHeading(html, "二、整体进度");
        html.Append("<p>").Append(H(BuildProgressSummary(project, stages, data.StageId.HasValue))).Append("</p>");
        AppendHtmlTable(html, new[] { "阶段状态", "阶段数" }, StageStatusRows(stages));

        AppendHtmlHeading(html, "三、料件清单");
        if (materials.Count == 0)
        {
            AppendHtmlEmpty(html, "暂无料件记录");
        }
        else
        {
            foreach (var material in materials)
            {
                AppendHtmlPropertyTable(html, MaterialProperties(material));
            }
        }

        AppendHtmlHeading(html, "四、阶段概览");
        if (stages.Count == 0)
        {
            AppendHtmlEmpty(html, "暂无阶段记录");
        }
        else
        {
            AppendHtmlTable(html, new[] { "序号", "阶段名称", "状态", "开始日期", "结束日期", "最近更新" },
                stages.Select(x => new[]
                {
                    x.Sequence.ToString(CultureInfo.InvariantCulture), Display(x.Name), StageStatusText(x.Status),
                    FormatDate(x.StartDate), FormatDate(x.EndDate), FormatDateTime(x.UpdatedAt)
                }));
        }

        AppendHtmlHeading(html, "五、各阶段详细记录");
        if (stages.Count == 0)
        {
            AppendHtmlEmpty(html, "暂无阶段记录");
        }
        else
        {
            for (var index = 0; index < stages.Count; index++)
            {
                var stage = stages[index];
                html.Append("<section class=\"stage")
                    .Append(index == 0 ? " first" : "")
                    .Append("\"><h3>")
                    .Append(H($"{stage.Sequence}. {Display(stage.Name)}"))
                    .Append("</h3>");
                AppendHtmlPropertyTable(html,
                    ("状态", StageStatusText(stage.Status)), ("开始日期", FormatDate(stage.StartDate)),
                    ("结束日期", FormatDate(stage.EndDate)), ("最近更新", FormatDateTime(stage.UpdatedAt)));
                AppendHtmlLongText(html, "阶段目标", stage.Objective);
                AppendHtmlLongText(html, "测试内容", stage.TestContent);
                AppendHtmlLongText(html, "测试结果", stage.TestResult);
                html.Append("</section>");
            }
        }

        AppendHtmlHeading(html, "六、关联报告清单");
        if (documents.Count == 0)
        {
            AppendHtmlEmpty(html, "暂无关联报告");
        }
        else
        {
            var stageNames = stages.ToDictionary(x => x.Id, x => x.Name);
            html.Append("<table class=\"report-list\"><thead><tr>")
                .Append("<th>文件</th><th>所属阶段</th><th>类型/大小</th><th>说明</th><th>上传信息</th><th>附件路径</th>")
                .Append("</tr></thead><tbody>");
            foreach (var document in documents)
            {
                var path = AttachmentPath(document);
                html.Append("<tr><td>").Append(H(document.FileName)).Append("</td><td>")
                    .Append(H(StageName(document.StageId, stageNames))).Append("</td><td>")
                    .Append(H($"{Display(document.ContentType)} / {FormatFileSize(document.Size)}")).Append("</td><td>")
                    .Append(H(Display(document.Description))).Append("</td><td>")
                    .Append(H($"{Display(document.UploadedBy)} / {FormatDateTime(document.UploadedAt)}")).Append("</td><td><a class=\"attachment\" href=\"")
                    .Append(H(AttachmentUri(document))).Append("\">").Append(H(path)).Append("</a></td></tr>");
            }
            html.Append("</tbody></table>");
        }

        AppendHtmlHeading(html, "七、落地跟进");
        if (followups.Count == 0)
        {
            AppendHtmlEmpty(html, "暂无跟进记录");
        }
        else
        {
            AppendHtmlTable(html, new[] { "应跟进日期", "跟进内容", "填写人", "填写时间" },
                followups.Select(x => new[]
                {
                    FormatDate(x.DueDate), Display(x.Content), Display(x.FilledByName), FormatDateTime(x.FilledAt)
                }));
        }

        AppendHtmlHeading(html, "八、料件流转摘要");
        if (flows.Count == 0)
        {
            AppendHtmlEmpty(html, "暂无料件流转记录");
        }
        else
        {
            AppendHtmlTable(html,
                new[] { "流转单号", "料件", "流转方向", "原因", "状态", "申请时间", "期限", "方式" },
                flows.Select(x => new[]
                {
                    Display(x.FlowNo), JoinNonEmpty(" / ", x.MaterialNo, x.MaterialName),
                    $"{Display(x.Applicant)} → {Display(x.Transferee)}", Display(x.Reason), FlowStatusText(x.Status),
                    FormatDateTime(x.ApplyTime), FormatDateTime(x.Deadline), x.DirectTransfer ? "直接转移" : "审批流转"
                }));
        }

        AppendHtmlHeading(html, "九、生成信息");
        html.Append("<p>本报告由部门资产管理系统生成，生成时间：")
            .Append(H(FormatDateTime(data.GeneratedAt))).Append("。</p></body></html>");

        return html.ToString();
    }

    private static XDocument BuildDocument(ProjectReportData data)
    {
        var project = data.Project ?? new TestProjectDto();
        var stages = OrderedStages(data);
        var materials = (data.Materials ?? new List<TestMaterialDto>())
            .OrderBy(x => x.MaterialNo, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Id)
            .ToList();
        var documents = OrderedDocuments(data, stages);
        var followups = (data.Followups ?? new List<TestProjectFollowupDto>())
            .OrderBy(x => x.DueDate)
            .ThenBy(x => x.FilledAt)
            .ToList();
        var flows = (data.Flows ?? new List<MaterialFlowDto>())
            .OrderBy(x => x.ApplyTime)
            .ThenBy(x => x.Id)
            .ToList();

        var body = new XElement(W + "body",
            Paragraph(ReportTitle(data), "Title"),
            Paragraph($"生成时间：{FormatDateTime(data.GeneratedAt)}", "Subtitle"),
            Heading("一、项目基本资料", 1),
            PropertyTable(
                ("项目编号", project.Code), ("项目名称", project.Name),
                ("项目类型", project.ProjectTypeLabel ?? project.ProjectTypeCode), ("负责人", project.OwnerName),
                ("开始日期", FormatDate(project.StartDate)), ("计划完成", FormatDate(project.PlannedFinishDate)),
                ("结案日期", FormatDate(project.ClosedDate)), ("项目进度", project.ProgressLabel ?? project.ProgressCode),
                ("跟进间隔", $"{project.FollowUpIntervalDays} 天"), ("下次跟进", FormatDate(project.NextFollowUpDueDate)),
                ("跟进状态", project.FollowUpStatus), ("创建时间", FormatDateTime(project.CreatedAt))),
            LongTextBlock("测试情况", project.TestStatus),
            LongTextBlock("最近跟进", project.LatestFollowUpContent),
            Heading("二、整体进度", 1),
            Paragraph(BuildProgressSummary(project, stages, data.StageId.HasValue)),
            Table(new[] { "阶段状态", "阶段数" }, StageStatusRows(stages)),
            Heading("三、料件清单", 1));

        if (materials.Count == 0) body.Add(Paragraph("暂无料件记录", "Note"));
        foreach (var material in materials) body.Add(PropertyTable(MaterialProperties(material)));

        body.Add(Heading("四、阶段概览", 1));
        body.Add(stages.Count == 0
            ? Paragraph("暂无阶段记录", "Note")
            : Table(new[] { "序号", "阶段名称", "状态", "开始日期", "结束日期", "最近更新" },
                stages.Select(x => new[]
                {
                    x.Sequence.ToString(CultureInfo.InvariantCulture), Display(x.Name), StageStatusText(x.Status),
                    FormatDate(x.StartDate), FormatDate(x.EndDate), FormatDateTime(x.UpdatedAt)
                })));

        body.Add(PageBreak(), Heading("五、各阶段详细记录", 1));
        if (stages.Count == 0)
        {
            body.Add(Paragraph("暂无阶段记录", "Note"));
        }
        else
        {
            for (var index = 0; index < stages.Count; index++)
            {
                var stage = stages[index];
                body.Add(Heading($"{stage.Sequence}. {Display(stage.Name)}", 2, index > 0));
                body.Add(PropertyTable(
                    ("状态", StageStatusText(stage.Status)), ("开始日期", FormatDate(stage.StartDate)),
                    ("结束日期", FormatDate(stage.EndDate)), ("最近更新", FormatDateTime(stage.UpdatedAt))));
                body.Add(LongTextBlock("阶段目标", stage.Objective));
                body.Add(LongTextBlock("测试内容", stage.TestContent));
                body.Add(LongTextBlock("测试结果", stage.TestResult));
            }
        }

        body.Add(Heading("六、关联报告清单", 1, pageBreakBefore: true));
        if (documents.Count == 0)
        {
            body.Add(Paragraph("暂无关联报告", "Note"));
        }
        else
        {
            var stageNames = stages.ToDictionary(x => x.Id, x => x.Name);
            body.Add(Table(
                new[] { "文件", "所属阶段", "类型/大小", "说明", "上传信息", "附件路径" },
                documents.Select(x => new[]
                {
                    Display(x.FileName), StageName(x.StageId, stageNames), $"{Display(x.ContentType)} / {FormatFileSize(x.Size)}",
                    Display(x.Description), $"{Display(x.UploadedBy)} / {FormatDateTime(x.UploadedAt)}", AttachmentPath(x)
                })));
        }

        body.Add(Heading("七、落地跟进", 1));
        body.Add(followups.Count == 0
            ? Paragraph("暂无跟进记录", "Note")
            : Table(new[] { "应跟进日期", "跟进内容", "填写人", "填写时间" },
                followups.Select(x => new[]
                {
                    FormatDate(x.DueDate), Display(x.Content), Display(x.FilledByName), FormatDateTime(x.FilledAt)
                })));

        body.Add(Heading("八、料件流转摘要", 1));
        body.Add(flows.Count == 0
            ? Paragraph("暂无料件流转记录", "Note")
            : Table(new[] { "流转单号", "料件", "流转方向", "原因", "状态", "申请时间", "期限", "方式" },
                flows.Select(x => new[]
                {
                    Display(x.FlowNo), JoinNonEmpty(" / ", x.MaterialNo, x.MaterialName),
                    $"{Display(x.Applicant)} → {Display(x.Transferee)}", Display(x.Reason), FlowStatusText(x.Status),
                    FormatDateTime(x.ApplyTime), FormatDateTime(x.Deadline), x.DirectTransfer ? "直接转移" : "审批流转"
                })));

        body.Add(
            Heading("九、生成信息", 1),
            Paragraph($"本报告由部门资产管理系统生成，生成时间：{FormatDateTime(data.GeneratedAt)}。"),
            new XElement(W + "sectPr",
                new XElement(W + "pgSz",
                    new XAttribute(W + "w", "11906"),
                    new XAttribute(W + "h", "16838")),
                new XElement(W + "pgMar",
                    new XAttribute(W + "top", "1021"),
                    new XAttribute(W + "right", "907"),
                    new XAttribute(W + "bottom", "1021"),
                    new XAttribute(W + "left", "907"),
                    new XAttribute(W + "header", "567"),
                    new XAttribute(W + "footer", "567"),
                    new XAttribute(W + "gutter", "0"))));

        return new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            new XElement(W + "document",
                new XAttribute(XNamespace.Xmlns + "w", W),
                new XAttribute(XNamespace.Xmlns + "r", R),
                body));
    }

    private static XDocument BuildStyles()
    {
        XElement RunProperties(string size, bool bold = false, string color = "1F2937") =>
            new(W + "rPr",
                new XElement(W + "rFonts",
                    new XAttribute(W + "ascii", "Calibri"),
                    new XAttribute(W + "hAnsi", "Calibri"),
                    new XAttribute(W + "eastAsia", "Microsoft YaHei"),
                    new XAttribute(W + "cs", "Calibri")),
                bold ? new XElement(W + "b") : null,
                new XElement(W + "color", new XAttribute(W + "val", color)),
                new XElement(W + "sz", new XAttribute(W + "val", size)),
                new XElement(W + "szCs", new XAttribute(W + "val", size)),
                new XElement(W + "lang", new XAttribute(W + "eastAsia", "zh-CN")));

        XElement Style(string id, string name, string? basedOn, string? next, XElement pPr, XElement rPr) =>
            new(W + "style",
                new XAttribute(W + "type", "paragraph"),
                new XAttribute(W + "styleId", id),
                new XElement(W + "name", new XAttribute(W + "val", name)),
                basedOn is null ? null : new XElement(W + "basedOn", new XAttribute(W + "val", basedOn)),
                next is null ? null : new XElement(W + "next", new XAttribute(W + "val", next)),
                pPr,
                rPr);

        return new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            new XElement(W + "styles",
                new XAttribute(XNamespace.Xmlns + "w", W),
                new XElement(W + "docDefaults",
                    new XElement(W + "rPrDefault", RunProperties("22")),
                    new XElement(W + "pPrDefault",
                        new XElement(W + "pPr",
                            new XElement(W + "spacing", new XAttribute(W + "after", "120"), new XAttribute(W + "line", "360"), new XAttribute(W + "lineRule", "auto"))))),
                new XElement(W + "style",
                    new XAttribute(W + "type", "paragraph"),
                    new XAttribute(W + "default", "1"),
                    new XAttribute(W + "styleId", "Normal"),
                    new XElement(W + "name", new XAttribute(W + "val", "Normal")),
                    new XElement(W + "qFormat"),
                    new XElement(W + "pPr"),
                    RunProperties("22")),
                Style("Title", "Title", "Normal", "Subtitle",
                    new XElement(W + "pPr",
                        new XElement(W + "jc", new XAttribute(W + "val", "center")),
                        new XElement(W + "spacing", new XAttribute(W + "before", "240"), new XAttribute(W + "after", "160")),
                        new XElement(W + "keepNext")),
                    RunProperties("40", bold: true, color: "111827")),
                Style("Subtitle", "Subtitle", "Normal", "Normal",
                    new XElement(W + "pPr",
                        new XElement(W + "jc", new XAttribute(W + "val", "center")),
                        new XElement(W + "spacing", new XAttribute(W + "after", "360"))),
                    RunProperties("20", color: "6B7280")),
                Style("Heading1", "heading 1", "Normal", "Normal",
                    new XElement(W + "pPr",
                        new XElement(W + "keepNext"),
                        new XElement(W + "keepLines"),
                        new XElement(W + "spacing", new XAttribute(W + "before", "360"), new XAttribute(W + "after", "160")),
                        new XElement(W + "outlineLvl", new XAttribute(W + "val", "0"))),
                    RunProperties("32", bold: true, color: "1E3A5F")),
                Style("Heading2", "heading 2", "Normal", "Normal",
                    new XElement(W + "pPr",
                        new XElement(W + "keepNext"),
                        new XElement(W + "keepLines"),
                        new XElement(W + "spacing", new XAttribute(W + "before", "260"), new XAttribute(W + "after", "120")),
                        new XElement(W + "outlineLvl", new XAttribute(W + "val", "1"))),
                    RunProperties("28", bold: true, color: "1F4E78")),
                Style("TableText", "Table Text", "Normal", "Normal",
                    new XElement(W + "pPr", new XElement(W + "spacing", new XAttribute(W + "after", "0"), new XAttribute(W + "line", "280"), new XAttribute(W + "lineRule", "auto"))),
                    RunProperties("18")),
                Style("Note", "Note", "Normal", "Normal",
                    new XElement(W + "pPr", new XElement(W + "ind", new XAttribute(W + "left", "240"))),
                    RunProperties("20", color: "6B7280"))));
    }

    private static XDocument BuildContentTypes() => new(
        new XDeclaration("1.0", "utf-8", "yes"),
        new XElement(ContentTypes + "Types",
            new XElement(ContentTypes + "Default",
                new XAttribute("Extension", "rels"),
                new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
            new XElement(ContentTypes + "Default",
                new XAttribute("Extension", "xml"),
                new XAttribute("ContentType", "application/xml")),
            Override("/word/document.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"),
            Override("/word/styles.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"),
            Override("/docProps/core.xml", "application/vnd.openxmlformats-package.core-properties+xml"),
            Override("/docProps/app.xml", "application/vnd.openxmlformats-officedocument.extended-properties+xml")));

    private static XElement Override(string partName, string contentType) =>
        new(ContentTypes + "Override", new XAttribute("PartName", partName), new XAttribute("ContentType", contentType));

    private static XDocument BuildPackageRelationships() => new(
        new XDeclaration("1.0", "utf-8", "yes"),
        new XElement(Relationships + "Relationships",
            Relationship("rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", "word/document.xml"),
            Relationship("rId2", "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties", "docProps/core.xml"),
            Relationship("rId3", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties", "docProps/app.xml")));

    private static XDocument BuildDocumentRelationships() => new(
        new XDeclaration("1.0", "utf-8", "yes"),
        new XElement(Relationships + "Relationships",
            Relationship("rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "styles.xml")));

    private static XElement Relationship(string id, string type, string target) =>
        new(Relationships + "Relationship", new XAttribute("Id", id), new XAttribute("Type", type), new XAttribute("Target", target));

    private static XDocument BuildCoreProperties(ProjectReportData data)
    {
        XNamespace cp = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
        XNamespace dc = "http://purl.org/dc/elements/1.1/";
        XNamespace dcterms = "http://purl.org/dc/terms/";
        XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
        var project = data.Project ?? new TestProjectDto();
        var generatedAt = GeneratedAtUtc(data.GeneratedAt).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

        return new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            new XElement(cp + "coreProperties",
                new XAttribute(XNamespace.Xmlns + "cp", cp),
                new XAttribute(XNamespace.Xmlns + "dc", dc),
                new XAttribute(XNamespace.Xmlns + "dcterms", dcterms),
                new XAttribute(XNamespace.Xmlns + "xsi", xsi),
                new XElement(dc + "title", ReportTitle(data)),
                new XElement(dc + "subject", "新产品新技术测试项目报告"),
                new XElement(dc + "creator", "部门资产管理系统"),
                new XElement(cp + "lastModifiedBy", "部门资产管理系统"),
                new XElement(cp + "keywords", "测试项目;测试料件;阶段报告"),
                new XElement(dcterms + "created", new XAttribute(xsi + "type", "dcterms:W3CDTF"), generatedAt),
                new XElement(dcterms + "modified", new XAttribute(xsi + "type", "dcterms:W3CDTF"), generatedAt)));
    }

    private static XDocument BuildAppProperties()
    {
        XNamespace ep = "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties";
        XNamespace vt = "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes";
        return new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            new XElement(ep + "Properties",
                new XAttribute(XNamespace.Xmlns + "vt", vt),
                new XElement(ep + "Application", "部门资产管理系统"),
                new XElement(ep + "AppVersion", "1.0"),
                new XElement(ep + "Company", "")));
    }

    private static XElement Heading(string text, int level, bool pageBreakBefore = false)
    {
        var paragraph = Paragraph(text, level == 1 ? "Heading1" : "Heading2");
        if (pageBreakBefore)
        {
            paragraph.Element(W + "pPr")?.Add(new XElement(W + "pageBreakBefore"));
        }
        return paragraph;
    }

    private static XElement Paragraph(string text, string? style = null, bool bold = false)
    {
        var paragraphProperties = new XElement(W + "pPr",
            style is null ? null : new XElement(W + "pStyle", new XAttribute(W + "val", style)));
        return new XElement(W + "p", paragraphProperties, Run(Display(text), bold));
    }

    private static XElement Run(string text, bool bold = false)
    {
        var run = new XElement(W + "r", bold ? new XElement(W + "rPr", new XElement(W + "b")) : null);
        var lines = NormalizeLineEndings(text).Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            if (index > 0)
            {
                run.Add(new XElement(W + "br"));
            }
            run.Add(new XElement(W + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), lines[index]));
        }
        return run;
    }

    private static XElement LongTextBlock(string label, string? text) =>
        new XElement(W + "sdt",
            new XElement(W + "sdtContent",
                Paragraph(label, bold: true),
                Paragraph(Display(text))));

    private static XElement PageBreak() =>
        new(W + "p", new XElement(W + "r", new XElement(W + "br", new XAttribute(W + "type", "page"))));

    private static XElement PropertyTable(params (string Label, string? Value)[] items)
    {
        var rows = new List<XElement>();
        for (var index = 0; index < items.Length; index += 2)
        {
            var left = items[index];
            var right = index + 1 < items.Length ? items[index + 1] : ("", "");
            rows.Add(new XElement(W + "tr",
                TableCell(left.Label, label: true),
                TableCell(Display(left.Value)),
                TableCell(right.Item1, label: true),
                TableCell(Display(right.Item2))));
        }
        return TableCore(rows);
    }

    private static XElement Table(IEnumerable<string> headers, IEnumerable<string[]> rows)
    {
        var tableRows = new List<XElement>
        {
            new(W + "tr",
                new XElement(W + "trPr", new XElement(W + "tblHeader"), new XElement(W + "cantSplit")),
                headers.Select(x => TableCell(x, header: true)))
        };
        tableRows.AddRange(rows.Select(row =>
            new XElement(W + "tr",
                row.Select(value => TableCell(Display(value))))));
        return TableCore(tableRows);
    }

    private static XElement TableCore(IEnumerable<XElement> rows)
    {
        var tableRows = rows.ToList();
        var columnCount = Math.Max(1, tableRows.Max(x => x.Elements(W + "tc").Count()));
        return new XElement(W + "tbl",
            new XElement(W + "tblPr",
                new XElement(W + "tblW", new XAttribute(W + "w", "5000"), new XAttribute(W + "type", "pct")),
                new XElement(W + "tblBorders",
                    Border("top"), Border("left"), Border("bottom"), Border("right"), Border("insideH"), Border("insideV")),
                new XElement(W + "tblLayout", new XAttribute(W + "type", "fixed")),
                new XElement(W + "tblCellMar",
                    CellMargin("top", "80"), CellMargin("left", "100"), CellMargin("bottom", "80"), CellMargin("right", "100"))),
            new XElement(W + "tblGrid", Enumerable.Range(0, columnCount).Select(_ =>
                new XElement(W + "gridCol", new XAttribute(W + "w", 10092 / columnCount)))),
            tableRows);
    }

    private static XElement Border(string name) =>
        new(W + name,
            new XAttribute(W + "val", "single"),
            new XAttribute(W + "sz", "4"),
            new XAttribute(W + "space", "0"),
            new XAttribute(W + "color", "9CA3AF"));

    private static XElement CellMargin(string name, string width) =>
        new(W + name, new XAttribute(W + "w", width), new XAttribute(W + "type", "dxa"));

    private static XElement TableCell(string text, bool header = false, bool label = false) =>
        new(W + "tc",
            new XElement(W + "tcPr",
                new XElement(W + "tcW", new XAttribute(W + "w", "0"), new XAttribute(W + "type", "auto")),
                header || label
                    ? new XElement(W + "shd", new XAttribute(W + "val", "clear"), new XAttribute(W + "fill", header ? "DCE6F1" : "EEF2F7"))
                    : null,
                new XElement(W + "vAlign", new XAttribute(W + "val", "top"))),
            Paragraph(Display(text), "TableText", header || label));

    private static List<ProjectTestStageDto> OrderedStages(ProjectReportData data) =>
        (data.Stages ?? new List<ProjectTestStageDto>())
            .OrderBy(x => x.Sequence)
            .ThenBy(x => x.Id)
            .ToList();

    private static List<ProjectReportDocumentDto> OrderedDocuments(ProjectReportData data, IReadOnlyCollection<ProjectTestStageDto> stages)
    {
        var sequenceById = stages.ToDictionary(x => x.Id, x => x.Sequence);
        return (data.Documents ?? new List<ProjectReportDocumentDto>())
            .OrderBy(x => x.StageId.HasValue ? sequenceById.GetValueOrDefault(x.StageId.Value, int.MaxValue - 1) : int.MaxValue)
            .ThenBy(x => x.UploadedAt)
            .ThenBy(x => x.Id)
            .ToList();
    }

    private static IEnumerable<string[]> StageStatusRows(IReadOnlyCollection<ProjectTestStageDto> stages) =>
        new[]
        {
            new[] { "未开始", stages.Count(x => string.Equals(x.Status, "planned", StringComparison.OrdinalIgnoreCase)).ToString(CultureInfo.InvariantCulture) },
            new[] { "进行中", stages.Count(x => string.Equals(x.Status, "in_progress", StringComparison.OrdinalIgnoreCase)).ToString(CultureInfo.InvariantCulture) },
            new[] { "已完成", stages.Count(x => string.Equals(x.Status, "completed", StringComparison.OrdinalIgnoreCase)).ToString(CultureInfo.InvariantCulture) },
            new[] { "受阻", stages.Count(x => string.Equals(x.Status, "blocked", StringComparison.OrdinalIgnoreCase)).ToString(CultureInfo.InvariantCulture) }
        };

    private static string ReportTitle(ProjectReportData data)
    {
        var name = Display(data.Project.Name);
        var stage = data.StageId.HasValue ? data.Stages.SingleOrDefault(x => x.Id == data.StageId) : null;
        return stage is null ? $"{name}测试项目报告" : $"{name} — 第{stage.Sequence}阶段测试报告";
    }

    private static string BuildProgressSummary(TestProjectDto project, IReadOnlyCollection<ProjectTestStageDto> stages, bool stageOnly)
    {
        if (stages.Count == 0)
        {
            return $"项目进度：{Display(project.ProgressLabel ?? project.ProgressCode)}；尚未设置测试阶段。";
        }

        var completed = stages.Count(x => string.Equals(x.Status, "completed", StringComparison.OrdinalIgnoreCase));
        var percent = completed * 100 / stages.Count;
        var scope = stageOnly ? "本报告仅包含选定阶段" : "本报告包含项目全部阶段";
        return $"项目整体进度：{Display(project.ProgressLabel ?? project.ProgressCode)}；{scope}，共 {stages.Count} 个阶段，已完成 {completed} 个，所含阶段完成率 {percent}%。";
    }

    private static string StageStatusText(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        "planned" => "未开始",
        "in_progress" => "进行中",
        "completed" => "已完成",
        "blocked" => "受阻",
        _ => Display(status)
    };

    private static string MaterialStatusText(MaterialStatus status) => status switch
    {
        MaterialStatus.ReturnedToVendor => "已退回厂商",
        _ => "在用"
    };

    private static string FlowStatusText(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        "pending" => "审批中",
        "approved" => "已通过",
        "rejected" => "已驳回",
        "withdrawn" => "已撤回",
        _ => Display(status)
    };

    private static string StageName(int? stageId, IReadOnlyDictionary<int, string> stageNames) =>
        stageId.HasValue ? Display(stageNames.GetValueOrDefault(stageId.Value, $"阶段 #{stageId.Value}")) : "项目级";

    private static string AttachmentPath(ProjectReportDocumentDto document)
    {
        var fileName = document.FileName ?? "";
        return $"documents/{document.Id}/{(string.IsNullOrWhiteSpace(fileName) ? "attachment" : fileName)}";
    }

    private static string AttachmentUri(ProjectReportDocumentDto document) =>
        $"documents/{document.Id}/{Uri.EscapeDataString(string.IsNullOrWhiteSpace(document.FileName) ? "attachment" : document.FileName)}";

    private static string FormatDate(DateTime? value) => value.HasValue ? value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "—";

    private static string FormatDateTime(DateTime value) => value == default ? "—" :
        (value.Kind == DateTimeKind.Utc ? BusinessClock.FromUtc(value) : value).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private static string FormatDateTime(DateTime? value) => value.HasValue ? FormatDateTime(value.Value) : "—";

    private static DateTime GeneratedAtUtc(DateTime value)
    {
        if (value == default)
        {
            return DateTime.UnixEpoch;
        }
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => BusinessClock.ToUtc(value)
        };
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{Math.Max(0, bytes)} B";
        }
        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024d:0.#} KB";
        }
        return $"{bytes / 1024d / 1024d:0.#} MB";
    }

    private static string JoinNonEmpty(string separator, params string?[] values)
    {
        var items = values.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()).ToArray();
        return items.Length == 0 ? "—" : string.Join(separator, items);
    }

    private static (string Label, string? Value)[] MaterialProperties(TestMaterialDto x) => new[]
    {
        ("料件编号", (string?)x.MaterialNo), ("料件名称", x.Name),
        ("型号/品牌", JoinNonEmpty(" / ", x.Model, x.Brand)), ("数量", x.Quantity.ToString(CultureInfo.InvariantCulture)),
        ("供应厂商", x.VendorName), ("对接厂商", x.DockingVendorName),
        ("归属部门", x.DepartmentName), ("保管人", x.CustodianName),
        ("存放位置", x.LocationName), ("收件日期", FormatDate(x.ReceivedDate)),
        ("状态", MaterialStatusText(x.Status) + (x.IsDeleted ? "（已删除）" : "")), ("备注", x.Remark)
    };

    private static string Display(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    private static string NormalizeLineEndings(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static string H(string? value) => WebUtility.HtmlEncode(Display(value));

    private static void AppendHtmlHeading(StringBuilder html, string text) =>
        html.Append("<h2>").Append(H(text)).Append("</h2>");

    private static void AppendHtmlEmpty(StringBuilder html, string text) =>
        html.Append("<p class=\"empty\">").Append(H(text)).Append("</p>");

    private static void AppendHtmlLongText(StringBuilder html, string label, string? text) =>
        html.Append("<div class=\"avoid-break\"><h3>").Append(H(label)).Append("</h3><div class=\"long-text\">")
            .Append(H(text)).Append("</div></div>");

    private static void AppendHtmlPropertyTable(StringBuilder html, params (string Label, string? Value)[] items)
    {
        html.Append("<table><tbody>");
        for (var index = 0; index < items.Length; index += 2)
        {
            var left = items[index];
            var right = index + 1 < items.Length ? items[index + 1] : ("", "");
            html.Append("<tr><td class=\"label\">").Append(H(left.Label)).Append("</td><td>").Append(H(left.Value))
                .Append("</td><td class=\"label\">").Append(H(right.Item1)).Append("</td><td>").Append(H(right.Item2)).Append("</td></tr>");
        }
        html.Append("</tbody></table>");
    }

    private static void AppendHtmlTable(StringBuilder html, IEnumerable<string> headers, IEnumerable<string[]> rows)
    {
        html.Append("<table><thead><tr>");
        foreach (var header in headers)
        {
            html.Append("<th>").Append(H(header)).Append("</th>");
        }
        html.Append("</tr></thead><tbody>");
        foreach (var row in rows)
        {
            html.Append("<tr>");
            foreach (var value in row)
            {
                html.Append("<td>").Append(H(value)).Append("</td>");
            }
            html.Append("</tr>");
        }
        html.Append("</tbody></table>");
    }

    private static void WriteXml(ZipArchive archive, string path, XDocument document)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = true,
            CloseOutput = false
        });
        document.Save(writer);
    }
}
