using AssetManagement.Application.Audit;
using System.Text;
using AssetManagement.Application.Assets;
using AssetManagement.Application.Auth;
using AssetManagement.Application.BaseData;
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

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditActionFilter>();
builder.Services.AddControllers(o => o.Filters.Add<AuditActionFilter>());
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default")));
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
    && (jwtKey.Length < 32 || jwtKey.StartsWith("REPLACE_WITH", StringComparison.Ordinal)))
{
    throw new InvalidOperationException("生产环境必须配置强随机 Jwt:Key(至少 32 字符),请通过环境变量 Jwt__Key 注入");
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

public partial class Program
{
}
