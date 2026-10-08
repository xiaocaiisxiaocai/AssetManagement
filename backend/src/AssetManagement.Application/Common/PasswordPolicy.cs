namespace AssetManagement.Application.Common;

public static class PasswordPolicy
{
    public static void EnsureStrong(string password)
    {
        if (password.Length < 8 || password.Length > 64
            || !password.Any(char.IsAsciiLetter)
            || !password.Any(char.IsAsciiDigit))
        {
            throw new BizException(1004, "密码须为 8-64 位，且同时包含字母和数字");
        }
    }
}
