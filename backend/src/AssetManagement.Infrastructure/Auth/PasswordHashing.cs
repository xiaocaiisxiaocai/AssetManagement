namespace AssetManagement.Infrastructure.Auth;

/// <summary>
/// 为 bcrypt 增加显式的 SHA-384 预哈希标记，避免标准 bcrypt 对长 UTF-8 密码的
/// 输入截断产生“前缀相同即可通过”的歧义；未带标记的历史哈希仍可验证并在登录时升级。
/// </summary>
internal static class PasswordHashing
{
    private const string EnhancedPrefix = "{bcrypt-sha384}";

    internal static string Hash(string password)
        => EnhancedPrefix + BCrypt.Net.BCrypt.EnhancedHashPassword(password);

    internal static bool Verify(string password, string storedHash)
        => IsEnhanced(storedHash)
            ? BCrypt.Net.BCrypt.EnhancedVerify(password, storedHash[EnhancedPrefix.Length..])
            : BCrypt.Net.BCrypt.Verify(password, storedHash);

    internal static bool NeedsUpgrade(string storedHash) => !IsEnhanced(storedHash);

    private static bool IsEnhanced(string storedHash)
        => storedHash.StartsWith(EnhancedPrefix, StringComparison.Ordinal);
}
