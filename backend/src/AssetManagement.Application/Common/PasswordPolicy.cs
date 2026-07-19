using System.Text.RegularExpressions;

namespace AssetManagement.Application.Common;

public static partial class PasswordPolicy
{
    public static void EnsureStrong(string password)
    {
        if (password.Length < 8
            || password.Length > 128
            || !LetterRegex().IsMatch(password)
            || !NumberRegex().IsMatch(password))
        {
            throw new BizException(1004, "密码须为 8-128 位，并同时包含字母和数字");
        }
    }

    [GeneratedRegex("[A-Za-z]")]
    private static partial Regex LetterRegex();

    [GeneratedRegex("[0-9]")]
    private static partial Regex NumberRegex();
}
