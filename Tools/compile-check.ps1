<#
.SYNOPSIS
    Unity プロジェクトの C# コンパイルエラーを CLI から検査する。

.DESCRIPTION
    Unity Editor を batchmode で起動してスクリプトをコンパイルさせ、ログに
    コンパイルエラーが出ていないかを判定する。Editor を開かずに PR 時点の
    コンパイル可否を確認するためのもので、プロジェクトのファイルは変更しない
    (Unity が書き換える ProjectVersion.txt は終了時に元へ戻す)。

    終了コード
        0 : コンパイルエラーなし
        1 : コンパイルエラーあり
        2 : 検査を実行できなかった (Unity 未検出・タイムアウト等)

.PARAMETER ProjectPath
    検査するプロジェクトのルート。既定はこのスクリプトの 1 つ上の階層。

.PARAMETER UnityPath
    使用する Unity.exe のフルパス。省略時は ProjectVersion.txt のバージョンを
    Unity Hub のインストール先から探す。

.PARAMETER LogFile
    Unity のログ出力先。既定は <ProjectPath>/Logs/compile-check.log。

.PARAMETER AllowVersionMismatch
    ProjectVersion.txt と同じバージョンが無い場合に、同じメジャー.マイナーの
    別バージョンで代用する。ProjectVersion.txt が書き換わるため既定では許可しない。

.PARAMETER TimeoutMinutes
    Unity の実行を打ち切るまでの時間。初回はアセットのインポートが走るため長め。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Tools/compile-check.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Tools/compile-check.ps1 -AllowVersionMismatch -TimeoutMinutes 90
#>
[CmdletBinding()]
param(
    [string]$ProjectPath,
    [string]$UnityPath,
    [string]$LogFile,
    [switch]$AllowVersionMismatch,
    [int]$TimeoutMinutes = 60
)

$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

$ExitOk = 0
$ExitCompileError = 1
$ExitSetupError = 2

function Write-Section([string]$Text) {
    Write-Host ''
    Write-Host "=== $Text ==="
}

# Write-Error は $ErrorActionPreference = 'Stop' で終端エラーになり、
# 後続の exit へ到達できず終了コードが仕様どおりにならないため使わない
function Write-Failure([string]$Text) {
    [Console]::Error.WriteLine($Text)
}

function Get-EditorVersion([string]$Project) {
    $versionFile = Join-Path $Project 'ProjectSettings/ProjectVersion.txt'
    if (-not (Test-Path $versionFile)) {
        throw "ProjectVersion.txt が見つかりません: $versionFile (Unity プロジェクトのルートを -ProjectPath で指定してください)"
    }

    $line = Select-String -Path $versionFile -Pattern '^m_EditorVersion:\s*(.+)$' | Select-Object -First 1
    if ($null -eq $line) { throw "ProjectVersion.txt から m_EditorVersion を読み取れません: $versionFile" }

    return $line.Matches[0].Groups[1].Value.Trim()
}

function Get-UnityInstallRoot {
    $roots = New-Object System.Collections.Generic.List[string]
    $roots.Add('C:\Program Files\Unity\Hub\Editor')

    # Unity Hub の「別の場所にインストール」設定
    $secondary = Join-Path $env:APPDATA 'UnityHub/secondaryInstallPath.json'
    if (Test-Path $secondary) {
        $raw = (Get-Content $secondary -Raw).Trim().Trim('"')
        if ($raw) {
            $raw = $raw.Replace('\\', '\')
            $roots.Add((Join-Path $raw 'Editor'))
            $roots.Add($raw)
        }
    }

    return $roots | Where-Object { Test-Path $_ } | Select-Object -Unique
}

function Get-InstalledVersion($Roots) {
    $installed = foreach ($root in $Roots) {
        Get-ChildItem $root -Directory -ErrorAction SilentlyContinue |
            Where-Object { Test-Path (Join-Path $_.FullName 'Editor/Unity.exe') } |
            ForEach-Object { $_.Name }
    }

    return @($installed | Select-Object -Unique | Sort-Object)
}

function Find-UnityEditor([string]$Version, [bool]$AllowMismatch) {
    # 明示指定が最優先
    if ($UnityPath) {
        if (-not (Test-Path $UnityPath)) { throw "-UnityPath に指定された Unity.exe がありません: $UnityPath" }
        return $UnityPath
    }

    $roots = @(Get-UnityInstallRoot)
    if ($env:UNITY_EDITOR_PATH) { $roots = @($env:UNITY_EDITOR_PATH) + $roots }

    if ($roots.Count -eq 0) {
        throw 'Unity のインストール先が見つかりません。-UnityPath で Unity.exe を指定するか、UNITY_EDITOR_PATH を設定してください'
    }

    foreach ($root in $roots) {
        $exe = Join-Path $root "$Version/Editor/Unity.exe"
        if (Test-Path $exe) { return $exe }
    }

    # 要求バージョンが無いとき、黙って別バージョンで代用しない
    $installed = Get-InstalledVersion $roots

    if (-not $AllowMismatch) {
        throw ("プロジェクトが要求する Unity {0} が見つかりません。インストール済み: {1}. " -f $Version, ($installed -join ', ')) +
              '同系列で代用する場合は -AllowVersionMismatch を付けてください (ProjectVersion.txt が書き換わりますが実行後に復元します)'
    }

    # 同じメジャー.マイナーだけを代用候補にする
    $prefix = (($Version -split '\.')[0..1]) -join '.'
    $fallback = @($installed | Where-Object { $_.StartsWith("$prefix.") } | Sort-Object) | Select-Object -Last 1
    if (-not $fallback) {
        throw ("Unity {0} と同じ {1} 系のインストールがありません。インストール済み: {2}" -f $Version, $prefix, ($installed -join ', '))
    }

    foreach ($root in $roots) {
        $exe = Join-Path $root "$fallback/Editor/Unity.exe"
        if (Test-Path $exe) {
            Write-Warning ("Unity {0} が無いため {1} で検査します" -f $Version, $fallback)
            return $exe
        }
    }

    throw "代用バージョン $fallback の Unity.exe を解決できませんでした"
}

# Unity がプロジェクトを開いたときに書き換える可能性のある、Git 管理下のファイル。
# 検査でリポジトリを汚さないよう、実行前に控えて実行後に戻す
$GuardedFile = @(
    'ProjectSettings/ProjectVersion.txt'
    'Packages/manifest.json'
    'Packages/packages-lock.json'
)

function Read-GuardedFile([string]$Project) {
    $snapshot = @{}

    foreach ($relative in $GuardedFile) {
        $full = Join-Path $Project $relative
        if (Test-Path $full) { $snapshot[$relative] = Get-Content $full -Raw -Encoding UTF8 }
    }

    return $snapshot
}

function Restore-GuardedFile([string]$Project, $Snapshot) {
    $restored = New-Object System.Collections.Generic.List[string]
    # Set-Content -Encoding UTF8 は BOM を付けてしまい、元と別内容になるため使わない
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)

    foreach ($relative in $Snapshot.Keys) {
        $full = Join-Path $Project $relative
        if (-not (Test-Path $full)) { continue }
        if ((Get-Content $full -Raw -Encoding UTF8) -eq $Snapshot[$relative]) { continue }

        [System.IO.File]::WriteAllText($full, $Snapshot[$relative], $utf8NoBom)
        $restored.Add($relative)
    }

    return $restored
}

# Unity メニューの September/Import (アセットインポートツール) で入れる外部依存の名前空間。
# これらは .gitignore 対象 (/Assets/Plugins/, Assets/CRIMW/) でリポジトリに含まれないため、
# クローンしただけの作業ツリーでは大量の CS0246 が出る。コードの問題と区別する必要がある
$ExternalNamespace = @(
    'Fusion'          # Photon Fusion
    'Photon'
    'CriWare'         # CRI ADX
    'NaughtyAttributes'
    'RootMotion'      # Final IK
    'WebSocketSharp'
)

function Invoke-Git([string]$Project, [string[]]$GitArgs) {
    # ネイティブコマンドの stderr を成功ストリームへ混ぜると PS5.1 では
    # NativeCommandError になるため、出力だけ受け取り $LASTEXITCODE で判定する
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { return & git -C $Project @GitArgs } finally { $ErrorActionPreference = $previous }
}

# Unity はプロジェクトを開くとマテリアルや .asset を自分のバージョンで
# 再シリアライズすることがある。GuardedFile だけでは追い切れないため、
# git チェックアウトなら tracked ファイルの変更有無で検知する
function Get-TrackedChange([string]$Project) {
    $output = Invoke-Git $Project @('status', '--porcelain', '--untracked-files=no')
    if ($LASTEXITCODE -ne 0) { return $null }

    $changes = @($output | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })

    # 関数から空配列をそのまま返すと展開されて $null になり、
    # 「変更なし」と「git 管理外」が区別できなくなる。カンマ演算子で配列のまま返す
    return , $changes
}

function Test-ExternalAssetMissing($ErrorLines) {
    $names = ($ExternalNamespace -join '|')
    $pattern = "error CS0246: The type or namespace name '($names)'"

    return @($ErrorLines | Where-Object { $_ -match $pattern }).Count -gt 0
}

function Get-CompileErrorLine([string]$Path) {
    if (-not (Test-Path $Path)) { return @() }

    # 「Assets/Foo.cs(12,34): error CS1002: ...」形式と、Unity 自身の失敗行を拾う
    $pattern = '(:\s*error\s+CS\d+:)|(^Scripts have compiler errors)|(^Compilation failed)'
    $lines = Select-String -Path $Path -Pattern $pattern -Encoding UTF8 | ForEach-Object { $_.Line.Trim() }

    return @($lines | Select-Object -Unique)
}

# ---------------------------------------------------------------- 実行
if (-not $ProjectPath) { $ProjectPath = Split-Path -Parent $PSScriptRoot }
$ProjectPath = (Resolve-Path $ProjectPath).Path

try {
    $version = Get-EditorVersion $ProjectPath
    $unity = Find-UnityEditor $version ([bool]$AllowVersionMismatch)
} catch {
    Write-Failure $_.Exception.Message
    exit $ExitSetupError
}

if (-not $LogFile) { $LogFile = Join-Path $ProjectPath 'Logs/compile-check.log' }
$logDir = Split-Path -Parent $LogFile
if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir -Force | Out-Null }
if (Test-Path $LogFile) { Remove-Item $LogFile -Force }

Write-Section 'Compile check'
Write-Host "Project : $ProjectPath"
Write-Host "Unity   : $unity"
Write-Host "Log     : $LogFile"

# Unity はプロジェクトを開くと ProjectVersion.txt を自分のバージョンで上書きし、
# バージョンが違えばパッケージも解決し直して packages-lock.json を書き換える
$guardSnapshot = Read-GuardedFile $ProjectPath

# 実行前に tracked な変更が無かった場合だけ、実行後に Unity が付けた変更を戻す。
# 元から変更があった場合は利用者の作業を消しかねないので触らず警告するだけにする
# Get-TrackedChange は git チェックアウトでなければ $null、そうなら配列を返す。
# 型で判定して「git 管理下か」と「変更が無いか」を取り違えないようにする
$trackedBefore = Get-TrackedChange $ProjectPath
$canRestoreTracked = ($trackedBefore -is [System.Array]) -and ($trackedBefore.Count -eq 0)

$unityArgs = @(
    '-batchmode'
    '-nographics'
    '-quit'
    '-accept-apiupdate'
    '-projectPath', $ProjectPath
    '-logFile', $LogFile
)

$started = Get-Date
$process = Start-Process -FilePath $unity -ArgumentList $unityArgs -PassThru -NoNewWindow

# Start-Process -PassThru が返す Process は、終了前にハンドルを掴んでおかないと
# 終了後に ExitCode を読めず空になる。参照するだけでハンドルがキャッシュされる
$null = $process.Handle

$timedOut = $false

if (-not $process.WaitForExit($TimeoutMinutes * 60 * 1000)) {
    $timedOut = $true
    try { $process.Kill() } catch { <# 既に終了している場合がある #> }
    try { $process.WaitForExit(30000) | Out-Null } catch { <# kill 済みで待てないことがある #> }
}

$elapsed = (Get-Date) - $started

$restored = Restore-GuardedFile $ProjectPath $guardSnapshot
if ($restored.Count -gt 0) {
    Write-Warning ("Unity が書き換えたため復元しました: {0}" -f ($restored -join ', '))
}

$trackedAfter = Get-TrackedChange $ProjectPath
if (($trackedAfter -is [System.Array]) -and ($trackedAfter.Count -gt 0)) {
    if ($canRestoreTracked) {
        Invoke-Git $ProjectPath @('checkout', '--', '.') | Out-Null
        Write-Warning ("Unity が再シリアライズした {0} 件の変更を git checkout で戻しました" -f $trackedAfter.Count)
    } else {
        Write-Warning ("Unity が {0} 件の tracked ファイルを書き換えた可能性がありますが、実行前から変更があったため戻していません。git status で確認してください" -f $trackedAfter.Count)
    }
}

Write-Host ("Elapsed : {0:hh\:mm\:ss}" -f $elapsed)

if ($timedOut) {
    Write-Failure "$TimeoutMinutes 分を超えたため Unity を打ち切りました。初回はアセットのインポートに時間がかかります。-TimeoutMinutes を伸ばして再実行してください"
    exit $ExitSetupError
}

$errorLines = @(Get-CompileErrorLine $LogFile)

if ($errorLines.Count -gt 0) {
    Write-Section 'Compile errors'
    $errorLines | ForEach-Object { Write-Host $_ }
    Write-Host ''

    # 外部アセット未インポートが混ざっている時点で、残りのエラーもその巻き添えの可能性が高い。
    # コードの問題として報告すると誤解を招くため、検査不能として扱う
    if (Test-ExternalAssetMissing $errorLines) {
        Write-Failure ("外部アセットが未インポートのため検査できません ({0} など)。" -f ($ExternalNamespace -join ' / ')) 
        Write-Failure 'Unity の September/Import からアセットをインポート済みの作業ツリーに対して実行してください。クローン直後の作業ツリーでは検査できません'
        exit $ExitSetupError
    }

    Write-Host ("コンパイルエラー {0} 件。詳細は {1}" -f $errorLines.Count, $LogFile)
    exit $ExitCompileError
}

$unityExitCode = $process.ExitCode
if ($null -eq $unityExitCode) {
    Write-Failure ("Unity の終了コードを取得できませんでした。詳細は {0}" -f $LogFile)
    exit $ExitSetupError
}

if ($unityExitCode -ne 0) {
    Write-Failure ("Unity が終了コード {0} で終了しました。コンパイル以外の失敗の可能性があります。詳細は {1}" -f $unityExitCode, $LogFile)
    exit $ExitSetupError
}

Write-Host 'コンパイルエラーはありません'
exit $ExitOk
