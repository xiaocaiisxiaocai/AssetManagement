using AssetManagement.Api.Configuration;
using FluentAssertions;

namespace AssetManagement.Tests.Configuration;

public class ProductionConfigurationGuardTests
{
    private const string RealJwtKey = "real-production-jwt-secret-key-32ch";
    private const string RealAdminPassword = "a-real-admin-password";
    private const string RealConnectionString =
        "Server=mysql;Port=3306;Database=assetmgmt;User=assetmgmt_user;Password=real-database-secret;CharSet=utf8mb4";

    [Fact]
    public void Production_rejects_change_me_jwt_key()
    {
        var act = () => Ensure(
            jwtKey: "CHANGE_ME_AT_LEAST_32_CHARACTERS_RANDOM_SECRET");

        act.Should().Throw<InvalidOperationException>().WithMessage("*Jwt__Key*");
    }

    [Fact]
    public void Production_rejects_change_me_admin_password_even_when_database_is_not_empty()
    {
        var act = () => Ensure(adminPassword: "CHANGE_ME_ADMIN_PASSWORD");

        act.Should().Throw<InvalidOperationException>().WithMessage("*ASSET_ADMIN_PASSWORD*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Production_allows_unset_admin_password(string? adminPassword)
    {
        var act = () => Ensure(adminPassword: adminPassword);

        act.Should().NotThrow();
    }

    [Fact]
    public void Production_rejects_change_me_connection_password()
    {
        var act = () => Ensure(connectionString:
            "Server=mysql;Port=3306;Database=assetmgmt;User=assetmgmt_user;Password=CHANGE_ME_DATABASE_PASSWORD;CharSet=utf8mb4");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ConnectionStrings__Default*MYSQL_PASSWORD*");
    }

    [Fact]
    public void Production_rejects_pwd_alias_and_lowercase_change_me()
    {
        var pwd = () => Ensure(connectionString: "Server=mysql;pwd=CHANGE_ME_DATABASE_PASSWORD");
        var lower = () => Ensure(connectionString: "Server=mysql;password=change_me_secret");

        pwd.Should().Throw<InvalidOperationException>().WithMessage("*MYSQL_PASSWORD*");
        lower.Should().Throw<InvalidOperationException>().WithMessage("*MYSQL_PASSWORD*");
    }

    [Fact]
    public void Production_ignores_change_me_outside_the_connection_password()
    {
        var act = () => Ensure(connectionString:
            "Server=mysql;Database=assetmgmt;User=CHANGE_ME_user;Password=real-database-secret");

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Production_allows_missing_connection_string(string? connectionString)
    {
        var act = () => Ensure(connectionString: connectionString);

        act.Should().NotThrow();
    }

    [Fact]
    public void Production_accepts_real_secrets()
    {
        var act = () => Ensure();

        act.Should().NotThrow();
    }

    [Fact]
    public void Production_still_rejects_replace_with_prefix_and_short_jwt_key()
    {
        var prefixed = () => Ensure(jwtKey: "REPLACE_WITH_STRONG_RANDOM_SECRET_AT_LEAST_32_CHARS");
        var shortKey = () => Ensure(jwtKey: "1234567890123456789012345678901");
        var exactLength = () => Ensure(jwtKey: "12345678901234567890123456789012");

        prefixed.Should().Throw<InvalidOperationException>().WithMessage("*Jwt__Key*");
        shortKey.Should().Throw<InvalidOperationException>().WithMessage("*Jwt__Key*");
        exactLength.Should().NotThrow();
    }

    [Fact]
    public void Production_does_not_broaden_replace_with_match()
    {
        var lowercasePrefix = () => Ensure(jwtKey: "replace_with_not_the_ordinal_prefix_xx");
        var middleMarker = () => Ensure(jwtKey: "xxxxxxxxREPLACE_yyyyyyyyyyyyyyyy");

        lowercasePrefix.Should().NotThrow();
        middleMarker.Should().NotThrow();
    }

    [Fact]
    public void Production_rejects_missing_jwt_key()
    {
        var act = () => Ensure(jwtKey: null);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Jwt__Key*");
    }

    [Fact]
    public void Development_accepts_placeholders_and_short_keys()
    {
        var act = () => ProductionConfigurationGuard.EnsureProductionSecrets(
            isDevelopment: true,
            jwtKey: "CHANGE_ME",
            adminPassword: "CHANGE_ME_ADMIN_PASSWORD",
            connectionString: "Server=mysql;Password=CHANGE_ME_DATABASE_PASSWORD");

        act.Should().NotThrow();
    }

    [Fact]
    public void Replacement_value_check_stays_on_replace_marker_only()
    {
        ProductionConfigurationGuard.IsReplacementValue("REPLACE_with_path").Should().BeTrue();
        ProductionConfigurationGuard.IsReplacementValue("CHANGE_ME").Should().BeFalse();
        ProductionConfigurationGuard.IsReplacementValue(null).Should().BeFalse();
        ProductionConfigurationGuard.IsReplacementValue("  ").Should().BeFalse();
    }

    private static void Ensure(
        string? jwtKey = RealJwtKey,
        string? adminPassword = RealAdminPassword,
        string? connectionString = RealConnectionString)
        => ProductionConfigurationGuard.EnsureProductionSecrets(
            isDevelopment: false,
            jwtKey,
            adminPassword,
            connectionString);
}
