using AssetManagement.Application.Audit;
using System.Text;
using AssetManagement.Application.Assets;
using AssetManagement.Application.Auth;
using AssetManagement.Application.BaseData;
using AssetManagement.Application.Common;
using AssetManagement.Application.Files;
using AssetManagement.Application.Rbac;
using AssetManagement.Application.Reports;
using AssetManagement.Application.Workflow;
using AssetManagement.Infrastructure.Audit;
using AssetManagement.Infrastructure.Assets;
using AssetManagement.Infrastructure.Auth;
using AssetManagement.Infrastructure.BaseData;
using AssetManagement.Infrastructure.Files;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Infrastructure.Persistence.Seed;
using AssetManagement.Infrastructure.Reports;
using AssetManagement.Infrastructure.Rbac;
using AssetManagement.Infrastructure.Workflow;
using AssetManagement.Api.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

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

// 使用 Serilog 替换默认日志
builder.Host.UseSerilog();

// Add services to the container.

builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<AuditActionFilter>();
builder.Services.AddControllers(o => o.Filters.Add<AuditActionFilter>());
builder.Services.AddDbContext<AppDbContext>(o =>
{
    o.UseSqlite(builder.Configuration.GetConnectionString("Default"));

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
builder.Services.AddSingleton<IFileStorageService>(_ =>
    new FileStorageService(
        builder.Configuration["Attachment:Path"] ?? "App_Data/uploads",
        builder.Environment.ContentRootPath));
builder.Services.AddScoped<IWorkflowService, WorkflowService>();
builder.Services.AddScoped<IBizEffectApplier, BizEffectApplier>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IAuditQueryService, AuditQueryService>();
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("缺少 Jwt:Key 配置");
// 生产环境纵深防御:禁止以占位符或弱密钥(<32 字符)启动,密钥应通过环境变量 Jwt__Key 注入
if (!builder.Environment.IsDevelopment()
    && (jwtKey.Length < AppConstants.JwtKeyMinLength || jwtKey.StartsWith("REPLACE_WITH", StringComparison.Ordinal)))
{
    throw new InvalidOperationException($"生产环境必须配置强随机 Jwt:Key(至少 {AppConstants.JwtKeyMinLength} 字符),请通过环境变量 Jwt__Key 注入");
}
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "AssetManagement";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
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
        // 资产图片用 <img>/el-image 加载无法携带 Authorization 头,仅对 /api/files 路径允许从 query 读取 token
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                if (string.IsNullOrEmpty(ctx.Token)
                    && ctx.HttpContext.Request.Path.StartsWithSegments("/api/files")
                    && ctx.Request.Query.TryGetValue("token", out var queryToken))
                {
                    ctx.Token = queryToken;
                }
                return Task.CompletedTask;
            }
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
    builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
        p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("accesstoken")));
}

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    DbSeeder.Seed(db);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionMiddleware>();

if (corsOrigins is { Length: > 0 })
{
    app.UseCors();
}

app.UseAuthentication();
app.UseMiddleware<SlidingTokenMiddleware>();
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
