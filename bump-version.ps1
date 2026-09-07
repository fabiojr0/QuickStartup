# ─────────────────────────────────────────────────────────────────
#  bump-version.ps1  —  Muda a versão do QuickStartup em um lugar só
#
#  <Version> em QuickStartup.csproj é a fonte única de verdade: build.ps1 lê
#  esse valor pro nome do .exe e repassa pro instalador (Inno Setup), e o
#  workflow de release (.github/workflows/release.yml) confere que a tag
#  publicada bate com ele. Este script só cuida de editar o csproj e criar
#  a tag/commit — nada é enviado ao GitHub automaticamente.
#
#  Uso:  .\bump-version.ps1 1.1.0
# ─────────────────────────────────────────────────────────────────
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version
)

$ErrorActionPreference = "Stop"
$root    = $PSScriptRoot
$project = Join-Path $root "QuickStartup\QuickStartup.csproj"

Write-Host "`n=== Atualizando versão para $Version em $project ===" -ForegroundColor Cyan
$content = Get-Content $project -Raw
$content = $content -replace '<Version>[\d.]+</Version>',               "<Version>$Version</Version>"
$content = $content -replace '<AssemblyVersion>[\d.]+</AssemblyVersion>', "<AssemblyVersion>$Version.0</AssemblyVersion>"
$content = $content -replace '<FileVersion>[\d.]+</FileVersion>',        "<FileVersion>$Version.0</FileVersion>"
Set-Content $project -Value $content -NoNewline

Write-Host "`n=== Commitando e criando a tag v$Version ===" -ForegroundColor Cyan
git -C $root add $project
git -C $root commit -m "Bump version to $Version"
git -C $root tag "v$Version"

Write-Host "`n=== Pronto! ===" -ForegroundColor Green
Write-Host "Commit e tag criados localmente. Revise com 'git show' e, quando quiser"
Write-Host "publicar o release de verdade, rode:"
Write-Host "  git push origin main --follow-tags" -ForegroundColor Yellow
Write-Host "Isso dispara o GitHub Actions, que compila e publica o release com o"
Write-Host "instalador anexado — o app vai detectar a atualização automaticamente."
