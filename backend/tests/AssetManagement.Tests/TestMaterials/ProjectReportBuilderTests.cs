using System.IO.Compression;
using System.Xml.Linq;
using AssetManagement.Application.TestMaterials;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.TestMaterials;
using FluentAssertions;

namespace AssetManagement.Tests.TestMaterials;

public class ProjectReportBuilderTests
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    [Fact]
    public void BuildDocx_creates_parseable_A4_package_with_metadata_and_complete_report_content()
    {
        var data = CreateReportData();

        var bytes = ProjectReportBuilder.BuildDocx(data);

        bytes.Should().NotBeEmpty();
        using var stream = new MemoryStream(bytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        archive.Entries.Select(x => x.FullName).Should().Contain(new[]
        {
            "[Content_Types].xml",
            "_rels/.rels",
            "docProps/core.xml",
            "docProps/app.xml",
            "word/document.xml",
            "word/styles.xml",
            "word/_rels/document.xml.rels"
        });

        var xmlEntries = archive.Entries.Where(x => x.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                                                     || x.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase));
        foreach (var entry in xmlEntries)
        {
            var act = () => ReadXml(entry);
            act.Should().NotThrow($"{entry.FullName} 应为可解析 XML");
        }

        var contentTypes = ReadXml(archive, "[Content_Types].xml");
        contentTypes.ToString().Should().Contain("wordprocessingml.document.main+xml")
            .And.Contain("wordprocessingml.styles+xml");

        XNamespace dc = "http://purl.org/dc/elements/1.1/";
        XNamespace dcterms = "http://purl.org/dc/terms/";
        var core = ReadXml(archive, "docProps/core.xml");
        core.Descendants(dc + "title").Single().Value.Should().Be("新型传感器验证测试项目报告");
        core.Descendants(dc + "creator").Single().Value.Should().Be("部门资产管理系统");
        core.Descendants(dcterms + "created").Single().Value.Should().NotBeNullOrWhiteSpace();

        var document = ReadXml(archive, "word/document.xml");
        var text = string.Concat(document.Descendants(W + "t").Select(x => x.Value));
        text.Should().Contain("项目基本资料")
            .And.Contain("整体进度")
            .And.Contain("对接厂商甲")
            .And.Contain("未开始")
            .And.Contain("进行中")
            .And.Contain("已完成")
            .And.Contain("受阻")
            .And.Contain("第一行中文说明")
            .And.Contain("第二行中文说明")
            .And.Contain("documents/31/阶段报告.pdf")
            .And.Contain("落地跟进")
            .And.Contain("料件流转摘要")
            .And.Contain("2026-10-19 09:30:00")
            .And.NotContain("阶段结论");
        text.IndexOf("阶段一", StringComparison.Ordinal).Should()
            .BeLessThan(text.IndexOf("阶段二", StringComparison.Ordinal), "阶段必须按 Sequence 排序");

        document.Descendants(W + "br")
            .Should().Contain(x => (string?)x.Attribute(W + "type") == "page", "报告应包含必要分页");
        document.Descendants(W + "br")
            .Should().Contain(x => x.Attribute(W + "type") == null, "长文本换行应写入 Word 换行符");
        var pageSize = document.Descendants(W + "pgSz").Single();
        pageSize.Attribute(W + "w")!.Value.Should().Be("11906");
        pageSize.Attribute(W + "h")!.Value.Should().Be("16838");

        var styles = ReadXml(archive, "word/styles.xml");
        styles.Descendants(W + "rFonts")
            .Should().Contain(x => (string?)x.Attribute(W + "eastAsia") == "Microsoft YaHei");
        styles.Descendants(W + "style").Select(x => (string?)x.Attribute(W + "styleId"))
            .Should().Contain(new[] { "Title", "Heading1", "Heading2" });
    }

    [Fact]
    public void BuildHtml_encodes_injection_preserves_multiline_text_and_uses_relative_attachment_path()
    {
        var data = CreateReportData();
        data.Project.Name = "项目</title><script>alert('x')</script>";
        var firstStage = data.Stages.Single(x => x.Sequence == 1);
        firstStage.Name = "阶段一<img src=x onerror=alert(1)>";
        firstStage.Objective = "目标第一行\n目标第二行<script>bad()</script>";
        data.Documents[0].FileName = "报告\"><script>bad()</script>.pdf";

        var html = ProjectReportBuilder.BuildHtml(data);

        html.Should().StartWith("<!doctype html>")
            .And.Contain("<meta charset=\"utf-8\">")
            .And.Contain("@page{size:A4")
            .And.Contain("项目&lt;/title&gt;&lt;script&gt;alert(&#39;x&#39;)&lt;/script&gt;")
            .And.Contain("阶段一&lt;img src=x onerror=alert(1)&gt;")
            .And.Contain("目标第一行\n目标第二行&lt;script&gt;bad()&lt;/script&gt;")
            .And.Contain($"href=\"documents/31/{Uri.EscapeDataString(data.Documents[0].FileName)}\"")
            .And.Contain("进行中")
            .And.Contain("受阻")
            .And.NotContain("阶段结论")
            .And.NotContain("<script>")
            .And.NotContain("<link ")
            .And.NotContain("http://")
            .And.NotContain("https://");

        html.IndexOf("阶段一&lt;img", StringComparison.Ordinal).Should()
            .BeLessThan(html.IndexOf("阶段二", StringComparison.Ordinal), "HTML 阶段也必须按 Sequence 排序");
    }

    [Fact]
    public void Stage_report_explicitly_labels_its_scope_instead_of_claiming_project_completion()
    {
        var data = CreateReportData();
        var stage = data.Stages.OrderBy(x => x.Sequence).First();
        data.StageId = stage.Id;
        data.Stages = new List<ProjectTestStageDto> { stage };
        var html = ProjectReportBuilder.BuildHtml(data);
        html.Should().Contain($"第{stage.Sequence}阶段测试报告").And.Contain("仅包含选定阶段")
            .And.NotContain("包含项目全部阶段");
        using var docx = new ZipArchive(new MemoryStream(ProjectReportBuilder.BuildDocx(data)));
        var xml = ReadXml(docx, "word/document.xml");
        string.Concat(xml.Descendants(W + "t").Select(x => x.Value))
            .Should().Contain("仅包含选定阶段");
    }

    private static ProjectReportData CreateReportData() => new()
    {
        Project = new TestProjectDto
        {
            Id = 7,
            Code = "TP-2026-007",
            Name = "新型传感器验证",
            ProjectTypeCode = "prototype",
            ProjectTypeLabel = "样机测试",
            OwnerName = "项目负责人",
            StartDate = new DateTime(2026, 9, 1),
            PlannedFinishDate = new DateTime(2026, 11, 30),
            ProgressCode = "testing",
            ProgressLabel = "测试中",
            TestStatus = "第一行中文说明\r\n第二行中文说明，包含足够长的测试状态内容。",
            FollowUpIntervalDays = 14,
            NextFollowUpDueDate = new DateTime(2026, 10, 25),
            FollowUpStatus = "正常",
            LatestFollowUpContent = "现场验证按计划推进",
            CreatedAt = new DateTime(2026, 8, 20, 8, 0, 0)
        },
        Materials = new List<TestMaterialDto>
        {
            new()
            {
                Id = 11,
                ProjectId = 7,
                MaterialNo = "TM-20260901-001",
                Name = "压力传感器",
                Model = "PS-100",
                Brand = "示例品牌",
                Quantity = 3,
                VendorName = "供应厂商甲",
                DockingVendorName = "对接厂商甲",
                DepartmentName = "研发部",
                CustodianName = "保管人甲",
                LocationName = "实验室 A",
                ReceivedDate = new DateTime(2026, 9, 1),
                Status = MaterialStatus.InUse,
                Remark = "仅用于验证"
            }
        },
        Stages = new List<ProjectTestStageDto>
        {
            new()
            {
                Id = 22,
                ProjectId = 7,
                Sequence = 2,
                Name = "阶段二",
                Status = "blocked",
                Objective = "完成环境适应性验证",
                TestContent = "高低温循环",
                TestResult = "等待补充样本",
                StartDate = new DateTime(2026, 10, 1),
                UpdatedAt = new DateTime(2026, 10, 18, 16, 0, 0)
            },
            new()
            {
                Id = 21,
                ProjectId = 7,
                Sequence = 1,
                Name = "阶段一",
                Status = "in_progress",
                Objective = "验证基础性能",
                TestContent = "第一项测试\n第二项测试",
                TestResult = "数据稳定",
                StartDate = new DateTime(2026, 9, 1),
                EndDate = new DateTime(2026, 9, 30),
                UpdatedAt = new DateTime(2026, 10, 1, 10, 0, 0)
            },
            new() { Id = 23, ProjectId = 7, Sequence = 3, Name = "阶段三", Status = "planned", UpdatedAt = new DateTime(2026, 10, 2) },
            new() { Id = 24, ProjectId = 7, Sequence = 4, Name = "阶段四", Status = "completed", UpdatedAt = new DateTime(2026, 10, 3) }
        },
        Documents = new List<ProjectReportDocumentDto>
        {
            new()
            {
                Id = 31,
                ProjectId = 7,
                StageId = 21,
                FileName = "阶段报告.pdf",
                ContentType = "application/pdf",
                Size = 2048,
                Description = "阶段一原始报告",
                UploadedBy = "上传人甲",
                UploadedAt = new DateTime(2026, 10, 2, 9, 15, 0)
            }
        },
        Followups = new List<TestProjectFollowupDto>
        {
            new()
            {
                Id = 41,
                ProjectId = 7,
                DueDate = new DateTime(2026, 10, 15),
                Content = "确认验证数据并安排落地试用",
                FilledByName = "跟进人甲",
                FilledAt = new DateTime(2026, 10, 15, 14, 30, 0)
            }
        },
        Flows = new List<MaterialFlowDto>
        {
            new()
            {
                Id = 51,
                FlowNo = "MF-20261001-001",
                BizType = "material_transfer",
                MaterialId = 11,
                MaterialNo = "TM-20260901-001",
                MaterialName = "压力传感器",
                Applicant = "申请人甲",
                Transferee = "接收人乙",
                Reason = "转交环境实验室",
                Status = "approved",
                ApplyTime = new DateTime(2026, 10, 1, 9, 0, 0),
                Deadline = new DateTime(2026, 10, 5, 18, 0, 0)
            }
        },
        GeneratedAt = new DateTime(2026, 10, 19, 9, 30, 0, DateTimeKind.Local)
    };

    private static XDocument ReadXml(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path);
        entry.Should().NotBeNull();
        return ReadXml(entry!);
    }

    private static XDocument ReadXml(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }
}
