[CmdletBinding()]
param(
    [string]$OutputPath,
    [switch]$SkipInstall,
    [switch]$SkipValidation,
    [switch]$IncludeInitialData,
    [switch]$Force
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Invoke-ExternalCommand {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "命令执行失败 ($LASTEXITCODE): $FilePath $($Arguments -join ' ')"
    }
}

function Assert-SafeOutputPath {
    param(
        [Parameter(Mandatory = $true)][string]$Candidate,
        [Parameter(Mandatory = $true)][string]$RepositoryRoot
    )

    $forbiddenPaths = @(
        $RepositoryRoot,
        $PSScriptRoot,
        [IO.Path]::GetPathRoot($Candidate)
    )
    foreach ($forbiddenPath in $forbiddenPaths) {
        if ($Candidate.TrimEnd('\') -ieq $forbiddenPath.TrimEnd('\')) {
            throw "发布输出目录不能是仓库、deploy 或磁盘根目录: $Candidate"
        }
    }
}

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$OutputPath = if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    Join-Path $PSScriptRoot "artifacts\AssetManagement-IIS"
} else {
    $OutputPath
}
$resolvedOutputPath = if ([IO.Path]::IsPathRooted($OutputPath)) {
    [IO.Path]::GetFullPath($OutputPath)
} else {
    [IO.Path]::GetFullPath((Join-Path $repoRoot $OutputPath))
}
$archivePath = "$resolvedOutputPath.zip"
Assert-SafeOutputPath -Candidate $resolvedOutputPath -RepositoryRoot $repoRoot

if ((Test-Path -LiteralPath $resolvedOutputPath) -or (Test-Path -LiteralPath $archivePath)) {
    if (-not $Force) {
        throw "输出已存在，请更换 -OutputPath 或显式使用 -Force: $resolvedOutputPath"
    }
}

Get-Command dotnet -ErrorAction Stop | Out-Null
Get-Command pnpm -ErrorAction Stop | Out-Null

$stagingPath = Join-Path ([IO.Path]::GetTempPath()) ("asset-management-iis-" + [Guid]::NewGuid().ToString("N"))
$webRoot = Join-Path $repoRoot "web"
$frontendDist = Join-Path $webRoot "apps\web-ele\dist"
$backendProject = Join-Path $repoRoot "backend\src\AssetManagement.Api\AssetManagement.Api.csproj"
$productionTemplate = Join-Path $PSScriptRoot "appsettings.Production.json"
$initialDataSource = Join-Path $PSScriptRoot "initial-data.local.json"

if ($IncludeInitialData -and -not (Test-Path -LiteralPath $initialDataSource)) {
    throw "已指定 -IncludeInitialData，但未找到: $initialDataSource"
}

New-Item -ItemType Directory -Path $stagingPath | Out-Null

try {
    Push-Location $webRoot
    try {
        if (-not $SkipInstall) {
            Invoke-ExternalCommand -FilePath "pnpm" -Arguments @("install", "--frozen-lockfile")
        }
        if (-not $SkipValidation) {
            Invoke-ExternalCommand -FilePath "pnpm" -Arguments @("-F", "@vben/web-ele", "run", "typecheck")
        }

        $env:VITE_GLOB_API_URL = "/api"
        Invoke-ExternalCommand -FilePath "pnpm" -Arguments @("--filter", "@vben/web-ele...", "run", "build")
    } finally {
        Pop-Location
    }

    if (-not (Test-Path -LiteralPath (Join-Path $frontendDist "index.html"))) {
        throw "前端构建未生成 index.html: $frontendDist"
    }

    Invoke-ExternalCommand -FilePath "dotnet" -Arguments @(
        "publish",
        $backendProject,
        "-c", "Release",
        "-o", $stagingPath,
        "--no-self-contained"
    )

    Get-ChildItem -LiteralPath $frontendDist -Force |
        Where-Object { $_.Name -notmatch '^dist\.(zip|tar|war)$' } |
        Copy-Item -Destination $stagingPath -Recurse -Force

    $productionConfig = Get-Content -Raw -LiteralPath $productionTemplate | ConvertFrom-Json
    $databaseConfig = [PSCustomObject]@{
        AutoMigrate = $true
        AutoSeed = $true
    }
    $productionConfig | Add-Member -NotePropertyName "Database" -NotePropertyValue $databaseConfig -Force
    $productionConfig | ConvertTo-Json -Depth 20 |
        Set-Content -LiteralPath (Join-Path $stagingPath "appsettings.Production.json") -Encoding UTF8

    $afterInitConfig = $productionConfig | ConvertTo-Json -Depth 20 | ConvertFrom-Json
    $afterInitConfig.Database.AutoMigrate = $false
    $afterInitConfig.Database.AutoSeed = $false
    $afterInitConfig | ConvertTo-Json -Depth 20 |
        Set-Content -LiteralPath (Join-Path $stagingPath "appsettings.Production.after-init.json") -Encoding UTF8

    $webConfig = @'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <defaultDocument enabled="true">
        <files>
          <clear />
          <add value="index.html" />
        </files>
      </defaultDocument>
      <handlers>
        <remove name="aspNetCore" />
        <add name="aspNetCore" path="api/*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet"
                  arguments=".\AssetManagement.Api.dll"
                  stdoutLogEnabled="false"
                  stdoutLogFile=".\logs\stdout"
                  hostingModel="inprocess">
        <environmentVariables>
          <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
        </environmentVariables>
      </aspNetCore>
    </system.webServer>
  </location>
</configuration>
'@
    if ($IncludeInitialData) {
        Copy-Item -LiteralPath $initialDataSource -Destination (Join-Path $stagingPath "initial-data.json")
        $environmentMarker = '          <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />'
        $initialDataEnvironment = $environmentMarker + [Environment]::NewLine +
            '          <environmentVariable name="ASSET_INITIAL_DATA_PATH" value="initial-data.json" />'
        $webConfig = $webConfig.Replace($environmentMarker, $initialDataEnvironment)
    }
    Set-Content -LiteralPath (Join-Path $stagingPath "web.config") -Value $webConfig -Encoding UTF8

    $disableInitializationScript = @'
$ErrorActionPreference = "Stop"
$configPath = Join-Path $PSScriptRoot "appsettings.Production.json"
$afterInitPath = Join-Path $PSScriptRoot "appsettings.Production.after-init.json"
if (-not (Test-Path -LiteralPath $afterInitPath)) {
    throw "未找到初始化后配置: $afterInitPath"
}
Copy-Item -LiteralPath $afterInitPath -Destination $configPath -Force
$initialDataPath = Join-Path $PSScriptRoot "initial-data.json"
if (Test-Path -LiteralPath $initialDataPath) {
    Remove-Item -LiteralPath $initialDataPath -Force
    Write-Host "已删除发布目录中的一次性初始化数据文件。" -ForegroundColor Green
}
Write-Host "已关闭 Database:AutoMigrate 和 Database:AutoSeed。请回收 IIS 应用程序池。" -ForegroundColor Green
'@
    Set-Content -LiteralPath (Join-Path $stagingPath "关闭数据库自动初始化.ps1") -Value $disableInitializationScript -Encoding UTF8

    New-Item -ItemType Directory -Path (Join-Path $stagingPath "logs") | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "README-部署.md") -Destination (Join-Path $stagingPath "部署说明.md")

    $runtimeFiles = Get-ChildItem -LiteralPath $stagingPath -Recurse -File |
        Where-Object { $_.Extension -in @(".html", ".js", ".css") }
    $forbiddenRuntimeReferences = $runtimeFiles |
        Select-String -Pattern "hm\.baidu\.com|unpkg\.com/@vbenjs/static-source" -List
    if ($forbiddenRuntimeReferences) {
        $paths = ($forbiddenRuntimeReferences | ForEach-Object Path) -join ", "
        throw "发布包仍包含运行时外网资源引用: $paths"
    }

    $manifestPath = Join-Path $stagingPath "SHA256SUMS.txt"
    Get-ChildItem -LiteralPath $stagingPath -Recurse -File |
        Where-Object { $_.FullName -ne $manifestPath } |
        Sort-Object FullName |
        ForEach-Object {
            $relativePath = $_.FullName.Substring($stagingPath.Length + 1).Replace('\', '/')
            $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            "$hash  $relativePath"
        } | Set-Content -LiteralPath $manifestPath -Encoding UTF8

    $outputParent = Split-Path -Parent $resolvedOutputPath
    New-Item -ItemType Directory -Path $outputParent -Force | Out-Null
    if (Test-Path -LiteralPath $resolvedOutputPath) {
        Remove-Item -LiteralPath $resolvedOutputPath -Recurse -Force
    }
    if (Test-Path -LiteralPath $archivePath) {
        Remove-Item -LiteralPath $archivePath -Force
    }
    New-Item -ItemType Directory -Path $resolvedOutputPath | Out-Null
    Get-ChildItem -LiteralPath $stagingPath -Force |
        Copy-Item -Destination $resolvedOutputPath -Recurse -Force
    Compress-Archive -Path (Join-Path $resolvedOutputPath "*") -DestinationPath $archivePath -CompressionLevel Optimal

    Write-Host ""
    Write-Host "IIS 发布包已生成:" -ForegroundColor Green
    Write-Host "  目录: $resolvedOutputPath"
    Write-Host "  ZIP : $archivePath"
    Write-Host ""
    Write-Host "首次启动前必须完成:" -ForegroundColor Yellow
    Write-Host "  1. 替换 appsettings.Production.json 中所有 REPLACE_ 占位值。"
    Write-Host "  2. 在 IIS 中配置 ASSET_ADMIN_PASSWORD（空库首次初始化必需）。"
    Write-Host "  3. 给 IIS 应用程序池账号授予 logs、附件和备份目录的修改权限。"
    Write-Host "  4. 健康检查成功后运行 '关闭数据库自动初始化.ps1'。"
    if ($IncludeInitialData) {
        Write-Host "  5. 本包包含一次性组织/人员初始化数据；初始化后请妥善删除 ZIP 包。" -ForegroundColor Yellow
    }
} finally {
    if (Test-Path -LiteralPath $stagingPath) {
        Remove-Item -LiteralPath $stagingPath -Recurse -Force
    }
}
