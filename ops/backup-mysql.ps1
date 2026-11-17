# Daily backup of the TextileConnect MySQL databases on Windows (the same as backup-mysql.sh).
#
#   $env:MYSQL_PWD = "<password>"      # never on the command line
#   .\ops\backup-mysql.ps1 -Folder D:\Backups\textileconnect -DbHost 127.0.0.1 -User backup
#
# Needs mysqldump (it comes with MySQL Server, for example C:\Program Files\MySQL\MySQL Server 8.0\bin) on the PATH or given with -MysqlBin.
# Run it every day from Task Scheduler and let the task alert when the exit code is not 0.
# Not tested against a server here (none was available when it was written): run it once by hand and restore the file with restore-mysql.sh --verify.
param(
    [Parameter(Mandatory)] [string] $Folder,
    [string] $DbHost = "127.0.0.1",
    [int] $Port = 3306,
    [string] $User = "root",
    [string[]] $Databases = @("textileconnect"),
    [int] $KeepDays = 14,
    [string] $MysqlBin = ""
)
$ErrorActionPreference = "Stop"
$dump = if ($MysqlBin) { Join-Path $MysqlBin "mysqldump.exe" } else { "mysqldump" }
New-Item -ItemType Directory -Force -Path $Folder | Out-Null
$stamp = (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ")

foreach ($db in $Databases) {
    $file = Join-Path $Folder "$db-$stamp.sql.gz"
    $partial = "$file.partial"
    Write-Host "Backing up $db to $file"
    # mysqldump writes SQL to its output; it is compressed here as it streams, so the plain file never sits on disk
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $dump
    $psi.Arguments = "--host=$DbHost --port=$Port --user=$User --single-transaction --routines --events --triggers --hex-blob --set-gtid-purged=OFF --default-character-set=utf8mb4 $db"
    $psi.RedirectStandardOutput = $true
    $psi.UseShellExecute = $false
    $process = [System.Diagnostics.Process]::Start($psi)
    $out = [System.IO.File]::Create($partial)
    $gzip = New-Object System.IO.Compression.GZipStream($out, [System.IO.Compression.CompressionLevel]::Optimal)
    $process.StandardOutput.BaseStream.CopyTo($gzip)
    $gzip.Dispose(); $out.Dispose()
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { Remove-Item $partial -Force; throw "mysqldump failed for $db with exit code $($process.ExitCode)" }
    if ((Get-Item $partial).Length -lt 1024) { Remove-Item $partial -Force; throw "The dump of $db is smaller than 1 KB: something is wrong." }
    Move-Item $partial $file -Force
    Write-Host ("  ok, {0:N0} bytes" -f (Get-Item $file).Length)
}

Get-ChildItem $Folder -Filter "*.sql.gz" | Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$KeepDays) } | ForEach-Object { Write-Host "Deleting $($_.Name)"; Remove-Item $_.FullName -Force }
Write-Host "Backup finished."
