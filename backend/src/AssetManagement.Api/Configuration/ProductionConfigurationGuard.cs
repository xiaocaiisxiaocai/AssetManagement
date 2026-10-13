using AssetManagement.Application.Common;
using MySqlConnector;

namespace AssetManagement.Api.Configuration;

/// <summary>
/// 非开发环境启动时拒绝示例配置里的占位密钥与口令。开发环境不调用失败分支。
/// </summary>
public static class ProductionConfigurationGuard
{
    /// <summary>
    /// 部署示例使用的占位标记。大小写不敏感，避免把示例改成小写后仍能启动。
    /// </summary>
    public static bool ContainsChangeMe(string? value)
        => !string.IsNullOrEmpty(value)
           && value.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 路径类配置的 REPLACE_ 占位。保持原判断：忽略大小写的包含匹配。
    /// </summary>
    public static bool IsReplacementValue(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && value.Contains("REPLACE_", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 用驱动解析连接串密码。Password 与 pwd 都会归到 Password。空白连接串不解析。
    /// </summary>
    public static string? ReadConnectionPassword(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return null;
        return new MySqlConnectionStringBuilder(connectionString).Password;
    }

    public static void EnsureProductionSecrets(
        bool isDevelopment,
        string? jwtKey,
        string? adminPassword,
        string? connectionString)
    {
        if (isDevelopment)
            return;

        if (string.IsNullOrEmpty(jwtKey))
        {
            throw new InvalidOperationException("缺少 Jwt:Key 配置，请通过环境变量 Jwt__Key 注入");
        }

        if (jwtKey.Length < AppConstants.JwtKeyMinLength
            || jwtKey.StartsWith("REPLACE_WITH", StringComparison.Ordinal)
            || ContainsChangeMe(jwtKey))
        {
            throw new InvalidOperationException(
                $"生产环境必须配置强随机 Jwt:Key(至少 {AppConstants.JwtKeyMinLength} 字符)，且不能使用 REPLACE_WITH 或 CHANGE_ME 占位值，请通过环境变量 Jwt__Key 注入");
        }

        if (ContainsChangeMe(adminPassword))
        {
            throw new InvalidOperationException(
                "生产环境 ASSET_ADMIN_PASSWORD 不能使用 CHANGE_ME 占位值，请通过环境变量 ASSET_ADMIN_PASSWORD 注入真实初始管理员密码");
        }

        if (ContainsChangeMe(ReadConnectionPassword(connectionString)))
        {
            throw new InvalidOperationException(
                "生产环境数据库密码不能使用 CHANGE_ME 占位值，请通过环境变量 ConnectionStrings__Default 注入真实连接串（Docker 部署时设置 MYSQL_PASSWORD）");
        }
    }
}
