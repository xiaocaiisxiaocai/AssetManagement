using AssetManagement.Application.Audit;
using System.Text;
using AssetManagement.Application.Assets;
using AssetManagement.Application.Auth;
using AssetManagement.Application.BaseData;
using AssetManagement.Application.Common;
using AssetManagement.Application.Files;
using AssetManagement.Application.Rbac;
using AssetManagement.Application.Reports;
using AssetManagement.Application.TestMaterials;
using AssetManagement.Application.Workflow;
using AssetManagement.Application.Notifications;
using AssetManagement.Infrastructure.Audit;
using AssetManagement.Infrastructure.Notifications;
using AssetManagement.Infrastructure.Assets;
using AssetManagement.Infrastructure.Auth;
using AssetManagement.Infrastructure.BaseData;
using AssetManagement.Infrastructure.Files;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Infrastructure.Persistence.Seed;
using AssetManagement.Infrastructure.Reports;
using AssetManagement.Infrastructure.Rbac;
using AssetManagement.Infrastructure.TestMaterials;
using AssetManagement.Infrastructure.Workflow;
using AssetManagement.Api.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;

// 配置 Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(
        path: "logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .Enrich.WithProperty("Application", "AssetManagement")
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Warning)
    .CreateLogger();

try
{
    Log.Information("应用程序启动中...");

var builder = WebApplication.CreateBuilder(args);

static bool IsReplacementValue(string? value)
    => !string.IsNullOrWhiteSpace(value)
       && value.Contains("REPLACE_", StringComparison.OrdinalIgnoreCase);

if (!builder.Environment.IsDevelopment())
{
    if (IsReplacementValue(builder.Configuration["Attachment:Path"]))
    {
        throw new InvalidOperationException("生产环境必须配置真实的 Attachment:Path，不能使用 REPLACE 占位值");
    }
    if (IsReplacementValue(builder.Configuration["DatabaseBackup:Path"]))
    {
        throw new InvalidOperationException("生产环境必须配置真实的 DatabaseBackup:Path，不能使用 REPLACE 占位值");
    }
}

// 使用 Serilog 替换默认日志
builder.Host.UseSerilog();

// Add services to the container.

builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();

// 全局请求体大小上限：附件大小上限（attachment_max_mb，最高可配置到 100MB）此前
// 仅在应用层读取完整文件流后才校验，恶意请求体在被拒绝前已耗费内存/IO。这里在
// 管线最前端设置一个足够覆盖多图上传场景的静态上限，尽早拒绝明显超大的请求。
const long maxRequestBodyBytes = 100 * 1024 * 1024;
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = maxRequestBodyBytes);
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = maxRequestBodyBytes;
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context =>
    {
        // 生产环境始终启用；开发环境默认启用，仅测试工厂可通过配置显式关闭，
        // 避免同一 TestServer IP 的大量独立登录场景互相干扰。
        var enabled = !builder.Environment.IsDevelopment()
            || builder.Configuration.GetValue("Security:LoginRateLimitEnabled", true);
        if (!enabled)
        {
            return RateLimitPartition.GetNoLimiter("test-disabled");
        }

        return RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
    // 修改密码/重置密码等凭据变更接口此前完全没有限流，仅登录接口受保护；
    // 已登录用户理论上可对旧密码做高频枚举/暴力尝试，补充与登录一致的限流水平。
    options.AddPolicy("credential-change", context =>
    {
        var enabled = !builder.Environment.IsDevelopment()
            || builder.Configuration.GetValue("Security:LoginRateLimitEnabled", true);
        if (!enabled)
        {
            return RateLimitPartition.GetNoLimiter("test-disabled");
        }

        return RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});
builder.Services.AddScoped<AuditActionFilter>();
builder.Services.AddControllers(o => o.Filters.Add<AuditActionFilter>())
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var message = context.ModelState.Values
                .SelectMany(value => value.Errors)
                .Select(error => error.ErrorMessage)
                .FirstOrDefault(error => !string.IsNullOrWhiteSpace(error))
                ?? "请求参数不正确";
            return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(
                ApiResult<object?>.Fail(4001, message));
        };
    });
builder.Services.AddDbContext<AppDbContext>(o =>
{
    var connStr = builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("缺少 ConnectionStrings:Default 配置，请通过环境变量 ConnectionStrings__Default 注入");
    if (!builder.Environment.IsDevelopment()
        && connStr.Contains("REPLACE_", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("生产环境必须通过环境变量 ConnectionStrings__Default 配置真实 MySQL 连接串");
    }
    o.UseMySql(connStr, ServerVersion.AutoDetect(connStr));

    // 默认不跟踪查询，提升只读查询性能
    o.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

    // 生产环境启用敏感数据日志和命令日志（监控慢查询）
    if (!builder.Environment.IsDevelopment())
    {
        o.EnableSensitiveDataLogging(false);
        o.LogTo(msg =>
        {
            // 记录执行时间超过阈值的慢查询
            if (msg.Contains("Executed DbCommand") && msg.Contains("ms"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(msg, @"(\d+)ms");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var ms) && ms > AppConstants.SlowQueryThresholdMs)
                {
                    Log.Warning("慢查询检测 ({Duration}ms): {Query}", ms, msg);
                }
            }
        }, Microsoft.Extensions.Logging.LogLevel.Information);
    }
});
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IRbacService, RbacService>();
builder.Services.AddScoped<IBaseDataService, BaseDataService>();
builder.Services.AddScoped<IAssetService, AssetService>();
builder.Services.AddScoped<IFileStorageService>(sp =>
    new FileStorageService(
        builder.Configuration["Attachment:Path"] ?? "App_Data/uploads",
        builder.Environment.ContentRootPath,
        sp.GetRequiredService<AppDbContext>()));
builder.Services.AddScoped<IWorkflowService, WorkflowService>();
builder.Services.AddScoped<IBizEffectApplier, BizEffectApplier>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IAuditQueryService, AuditQueryService>();
builder.Services.AddScoped<IAuditMaintenanceService, AuditMaintenanceService>();
builder.Services.AddScoped<IDatabaseBackupService, DatabaseBackupService>();
builder.Services.AddScoped<ITestProjectService, TestProjectService>();
builder.Services.AddScoped<ITestMaterialService, TestMaterialService>();
builder.Services.AddScoped<IMaterialFlowService, MaterialFlowService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddHostedService<OverdueNotificationWorker>();
builder.Services.AddHostedService<PendingApprovalReminderWorker>();
builder.Services.AddHostedService<DatabaseBackupWorker>();
builder.Services.AddHostedService<AuditCleanupWorker>();
builder.Services.AddHostedService<OrphanImageCleanupWorker>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        var jwtKey = builder.Configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("缺少 Jwt:Key 配置，请通过环境变量 Jwt__Key 注入");
        // 生产环境纵深防御:禁止以占位符或弱密钥(<32 字符)启动,密钥应通过环境变量 Jwt__Key 注入
        if (!builder.Environment.IsDevelopment()
            && (jwtKey.Length < AppConstants.JwtKeyMinLength || jwtKey.StartsWith("REPLACE_WITH", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"生产环境必须配置强随机 Jwt:Key(至少 {AppConstants.JwtKeyMinLength} 字符),请通过环境变量 Jwt__Key 注入");
        }
        var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "AssetManagement";
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "资产管理 API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// CORS:仅当配置了 Cors:AllowedOrigins 时启用(前后端分离部署场景);默认同源不启用
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
if (corsOrigins is { Length: > 0 })
{
    // 配合 WithExposedHeaders("accesstoken")，一旦误配为通配符/非法来源，任何被
    // 允许的源都能读取滑动续期下发的新 token，因此生产环境强制要求每个来源是
    // 精确的 https 完整地址（本机调试场景允许 http://localhost）。
    if (!builder.Environment.IsDevelopment())
    {
        foreach (var origin in corsOrigins)
        {
            var isLocalHttp = origin.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase)
                || origin.StartsWith("http://127.0.0.1", StringComparison.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(origin) || origin.Trim() == "*" ||
                (!origin.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !isLocalHttp))
            {
                throw new InvalidOperationException(
                    $"生产环境 Cors:AllowedOrigins 配置非法（\"{origin}\"）：不允许空值/通配符，且必须是完整的 https 来源");
            }
        }
    }
    builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
        p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("accesstoken")));
}

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (builder.Configuration.GetValue<bool>("Database:AutoMigrate"))
    {
        // 内网部署默认不在应用重启时改库；需要自动迁移时显式开启 Database:AutoMigrate。
        db.Database.Migrate();
    }
    if (builder.Configuration.GetValue<bool>("Database:AutoSeed"))
    {
        // 种子会同步角色权限、菜单等基础数据；需要初始化/修复时显式开启 Database:AutoSeed。
        if (!builder.Environment.IsDevelopment()
            && !db.Users.Any()
            && string.IsNullOrWhiteSpace(builder.Configuration["ASSET_ADMIN_PASSWORD"]))
        {
            throw new InvalidOperationException("生产环境初始化空库时必须通过 ASSET_ADMIN_PASSWORD 配置初始管理员密码");
        }
        DbSeeder.Seed(db, builder.Configuration["ASSET_ADMIN_PASSWORD"]);
    }
    if (!builder.Environment.IsDevelopment())
    {
        var backupPath = db.SystemSettings.AsNoTracking()
            .Where(x => x.Key == "database_backup_path")
            .Select(x => x.Value)
            .SingleOrDefault();
        if (IsReplacementValue(backupPath))
        {
            throw new InvalidOperationException("生产环境系统参数 database_backup_path 不能使用 REPLACE 占位值");
        }
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

// 反向代理场景（Nginx/Caddy）：解析 X-Forwarded-For / X-Forwarded-Proto，
// 确保 HttpContext.Connection.RemoteIpAddress 和 Request.Scheme 为真实客户端 IP
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
foreach (var proxyText in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? Array.Empty<string>())
{
    if (IPAddress.TryParse(proxyText, out var proxy))
    {
        forwardedHeadersOptions.KnownProxies.Add(proxy);
    }
}
app.UseForwardedHeaders(forwardedHeadersOptions);

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

if (corsOrigins is { Length: > 0 })
{
    app.UseCors();
}

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<AccountSecurityMiddleware>();
app.UseMiddleware<SlidingTokenMiddleware>();
app.UseMiddleware<AuthorizationFailureAuditMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "应用程序启动失败");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program
{
}
