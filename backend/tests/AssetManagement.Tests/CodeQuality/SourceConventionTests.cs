using FluentAssertions;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace AssetManagement.Tests.CodeQuality;

public class SourceConventionTests
{
    [Fact]
    public void Infrastructure_sources_do_not_use_find_async_under_global_no_tracking()
    {
        var root = FindRepositoryRoot();
        var files = Directory
            .EnumerateFiles(Path.Combine(root, "backend", "src", "AssetManagement.Infrastructure"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}"))
            .ToList();

        var offenders = files
            .SelectMany(file => File.ReadLines(file)
                .Select((line, index) => new { file, line, lineNo = index + 1 }))
            .Where(x => x.line.Contains("FindAsync(", StringComparison.Ordinal))
            .Select(x => $"{Path.GetRelativePath(root, x.file)}:{x.lineNo}")
            .ToList();

        offenders.Should().BeEmpty("全局 NoTracking 下写路径必须显式 AsTracking，读路径用 SingleOrDefaultAsync/FirstOrDefaultAsync 保持行为一致");
    }

    [Fact]
    public void Incremental_seed_does_not_create_menus_with_hardcoded_ids()
    {
        var root = FindRepositoryRoot();
        var seedFile = Path.Combine(root, "backend", "src", "AssetManagement.Infrastructure", "Persistence", "Seed", "DbSeeder.cs");
        var source = File.ReadAllText(seedFile);
        var incrementalStart = source.IndexOf("private static void SeedIncremental", StringComparison.Ordinal);

        incrementalStart.Should().BeGreaterThan(0);
        var incrementalSource = source[incrementalStart..];

        incrementalSource.Should().NotContain("new Menu { Id =", "增量种子要使用自增 ID，避免已有库主键冲突");
    }

    [Fact]
    public void Notification_idempotency_keys_are_not_built_from_raw_bpmn_node_ids()
    {
        var root = FindRepositoryRoot();
        var serviceFiles = new[]
        {
            Path.Combine(root, "backend", "src", "AssetManagement.Infrastructure", "Workflow", "WorkflowService.cs"),
            Path.Combine(root, "backend", "src", "AssetManagement.Infrastructure", "TestMaterials", "MaterialFlowService.cs"),
        };

        var offenders = serviceFiles
            .SelectMany(file => File.ReadLines(file)
                .Select((line, index) => new { file, line, lineNo = index + 1 }))
            .Where(x => x.line.Contains("IdempotencyKey =", StringComparison.Ordinal)
                        && x.line.Contains("{nodeId}", StringComparison.Ordinal))
            .Select(x => $"{Path.GetRelativePath(root, x.file)}:{x.lineNo}")
            .ToList();

        offenders.Should().BeEmpty("BPMN 节点 ID 来自 XML，长度不可控，必须先压缩成稳定短 key");
    }

    [Fact]
    public void Post_commit_notifications_are_guarded_and_logged()
    {
        var root = FindRepositoryRoot();
        var workflowSource = File.ReadAllText(Path.Combine(root, "backend", "src", "AssetManagement.Infrastructure", "Workflow", "WorkflowService.cs"));
        var materialSource = File.ReadAllText(Path.Combine(root, "backend", "src", "AssetManagement.Infrastructure", "TestMaterials", "MaterialFlowService.cs"));

        ExtractMethod(workflowSource, "public async Task<ApprovalFlowDto> StartAsync")
            .Should().Contain("try")
            .And.Contain("NotifyCurrentApproversAsync")
            .And.Contain("_logger.LogWarning", "发起审批已提交后，通知失败只能记录告警，不能反向打失败业务接口");

        ExtractMethod(materialSource, "public async Task<MaterialFlowDto> InitiateTransferAsync")
            .Should().Contain("try")
            .And.Contain("NotifyCurrentApproversAsync")
            .And.Contain("_logger.LogWarning", "料件流转已提交后，通知失败只能记录告警，不能反向打失败业务接口");
    }

    [Fact]
    public void Material_flow_service_does_not_swallow_notification_exceptions_silently()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "backend", "src", "AssetManagement.Infrastructure", "TestMaterials", "MaterialFlowService.cs"));

        source.Should().NotContain("catch (Exception)\r\n                {\r\n                    // 通知失败不影响直接转移结果。\r\n                }");
        source.Should().NotContain("catch (Exception) { }");
        source.Should().NotContain("catch { }");
    }

    [Fact]
    public void Incremental_seed_ensures_report_parent_before_report_overdue_child()
    {
        var root = FindRepositoryRoot();
        var seedFile = Path.Combine(root, "backend", "src", "AssetManagement.Infrastructure", "Persistence", "Seed", "DbSeeder.cs");
        var source = File.ReadAllText(seedFile);
        var incrementalSource = source[source.IndexOf("private static void SeedIncremental", StringComparison.Ordinal)..];
        var overdueIndex = incrementalSource.IndexOf("ReportOverdue", StringComparison.Ordinal);
        var reportParentIndex = incrementalSource.IndexOf("reportMenu = new Menu", StringComparison.Ordinal);

        overdueIndex.Should().BeGreaterThan(0);
        reportParentIndex.Should().BeInRange(0, overdueIndex - 1, "补 ReportOverdue 子菜单前必须先确保 Report 父菜单存在");
        incrementalSource.Should().Contain("MenuId = reportMenu.Id", "增量创建 Report 父菜单时也要给管理员补父路由授权");
    }

    [Fact]
    public void Incremental_seed_repairs_existing_core_role_permissions()
    {
        var root = FindRepositoryRoot();
        var seedFile = Path.Combine(root, "backend", "src", "AssetManagement.Infrastructure", "Persistence", "Seed", "DbSeeder.cs");
        var source = File.ReadAllText(seedFile);
        var incrementalSource = source[source.IndexOf("private static void SeedIncremental", StringComparison.Ordinal)..];

        incrementalSource.Should().Contain("EnsureCoreRolePermissions(db)", "已有库只会进入增量种子，必须补齐基础角色权限矩阵");
        var employeeRoleIndex = source.IndexOf("[\"employee\"]", StringComparison.Ordinal);
        employeeRoleIndex.Should().BeGreaterThan(0);
        var employeeRoleSource = source[employeeRoleIndex..source.IndexOf("}", employeeRoleIndex, StringComparison.Ordinal)];
        employeeRoleSource.Should().Contain("\"approval:create\"",
            "普通员工必须能发起资产审批，否则不同权限流程测试和实际员工借用申请都会被 403 拦截");
    }

    [Fact]
    public void Seeder_restores_query_tracking_behavior_after_seed()
    {
        var root = FindRepositoryRoot();
        var seedFile = Path.Combine(root, "backend", "src", "AssetManagement.Infrastructure", "Persistence", "Seed", "DbSeeder.cs");
        var source = File.ReadAllText(seedFile);
        var seedMethod = ExtractMethod(source, "public static void Seed(AppDbContext db)");

        seedMethod.Should().Contain("var originalTrackingBehavior = db.ChangeTracker.QueryTrackingBehavior");
        seedMethod.Should().Contain("finally");
        seedMethod.Should().Contain("db.ChangeTracker.QueryTrackingBehavior = originalTrackingBehavior");
    }

    [Fact]
    public void Business_controller_actions_do_not_use_plain_authorize_without_permission_code()
    {
        var root = FindRepositoryRoot();
        var controllerRoot = Path.Combine(root, "backend", "src", "AssetManagement.Api", "Controllers");
        var allowedPlainAuthorizeActions = new HashSet<string>
        {
            "AuthController.cs:UserInfo",
            "AuthController.cs:ChangePassword",
            "MenuController.cs:Routes",
            "NotificationController.cs:List",
            "NotificationController.cs:UnreadCount",
            "NotificationController.cs:MarkRead",
            "NotificationController.cs:MarkAllRead",
            "SettingController.cs:Runtime"
        };

        var offenders = Directory
            .EnumerateFiles(controllerRoot, "*Controller.cs")
            .SelectMany(file => ExtractActionAttributeBlocks(File.ReadAllText(file))
                .Where(block => block.Contains("[Http", StringComparison.Ordinal))
                .Where(block => block.Contains("[Authorize]", StringComparison.Ordinal)
                                && !block.Contains("[HasPermission(", StringComparison.Ordinal))
                .Where(block => !allowedPlainAuthorizeActions.Contains($"{Path.GetFileName(file)}:{ActionName(block)}"))
                .Select(block => $"{Path.GetRelativePath(root, file)}:{LineNumber(File.ReadAllText(file), block)}"))
            .ToList();

        offenders.Should().BeEmpty("业务接口必须绑定具体权限码，不能只要求已登录");
    }

    [Fact]
    public void Workspace_cli_bins_use_stable_wrappers()
    {
        var root = FindRepositoryRoot();
        var webRoot = Path.Combine(root, "web");
        var packages = new[]
        {
            (Path.Combine(webRoot, "scripts", "turbo-run", "package.json"), "turbo-run", "./cli/turbo-run.mjs"),
            (Path.Combine(webRoot, "scripts", "vsh", "package.json"), "vsh", "./cli/vsh.mjs"),
        };

        foreach (var (packageFile, commandName, expectedBin) in packages)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(packageFile));
            var actual = doc.RootElement.GetProperty("bin").GetProperty(commandName).GetString();
            actual.Should().Be(expectedBin, "workspace CLI 应有稳定包装入口，避免 package bin 直接暴露内部构建路径");
        }
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("无法定位仓库根目录");
    }

    private static string ExtractMethod(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0);

        var braceStart = source.IndexOf('{', start);
        braceStart.Should().BeGreaterThan(start);

        var depth = 0;
        for (var i = braceStart; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            if (source[i] == '}') depth--;
            if (depth == 0) return source[start..(i + 1)];
        }

        throw new InvalidOperationException($"无法提取方法：{signature}");
    }

    private static IEnumerable<string> ExtractActionAttributeBlocks(string source)
    {
        var lines = source.Replace("\r\n", "\n").Split('\n');
        var buffer = new List<string>();
        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("[", StringComparison.Ordinal))
            {
                buffer.Add(line);
                continue;
            }

            if (buffer.Count > 0 && trimmed.StartsWith("public ", StringComparison.Ordinal))
            {
                buffer.Add(line);
                yield return string.Join('\n', buffer);
                buffer.Clear();
                continue;
            }

            if (buffer.Count > 0 && trimmed.Length > 0)
            {
                buffer.Clear();
            }
        }
    }

    private static int LineNumber(string source, string block)
    {
        var index = source.IndexOf(block.Split('\n')[0], StringComparison.Ordinal);
        return index < 0 ? 1 : source[..index].Count(c => c == '\n') + 1;
    }

    private static string ActionName(string block)
    {
        var methodLine = block.Split('\n').First(line => line.TrimStart().StartsWith("public ", StringComparison.Ordinal));
        var match = Regex.Match(methodLine, @"\s([A-Za-z_][A-Za-z0-9_]*)\s*\(");
        return match.Success ? match.Groups[1].Value : methodLine.Trim();
    }
}
