namespace AssetManagement.Application.Common;

public static class PasswordPolicy
{
    public static void EnsureStrong(string password)
    {
        if (password.Length < 6 || password.Length > 12)
        {
            throw new BizException(1004, "密码须为 6-12 位");
        }
    }
}
