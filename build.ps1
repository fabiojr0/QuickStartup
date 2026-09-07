# ─────────────────────────────────────────────────────────────────
#  build.ps1  —  Compila e publica o QuickStartup
# ─────────────────────────────────────────────────────────────────
param(
    [switch]$NoInstaller   # Pula a etapa do Inno Setup
)

$ErrorActionPreference = "Stop"
$root    = $PSScriptRoot
$project = Join-Path $root "QuickStartup\QuickStartup.csproj"
$publish = Join-Path $root "QuickStartup\bin\Release\net8.0-windows\win-x64\publish"
$builds  = Join-Path $root "builds"

# Versão lida do <Version> do .csproj — fonte única de verdade. Para lançar uma nova
# versão, basta atualizar essa tag e rodar o build de novo.
$version = (Select-Xml -Path $project -XPath "//Version").Node.InnerText
if (-not $version) { throw "Não foi possível ler <Version> em $project" }

Write-Host "`n=== Gerando ícone placeholder (se não existir) ===" -ForegroundColor Cyan
$iconPath = Join-Path $root "QuickStartup\Assets\app.ico"
if (-not (Test-Path $iconPath)) {
    & "$root\tools\generate-icon.ps1" -Output $iconPath
    Write-Host "Ícone gerado em $iconPath"
} else {
    Write-Host "Ícone já existe, pulando geração."
}

Write-Host "`n=== Restaurando pacotes NuGet ===" -ForegroundColor Cyan
dotnet restore $project

Write-Host "`n=== Publicando (self-contained, win-x64) ===" -ForegroundColor Cyan
dotnet publish $project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    --output $publish

Write-Host "`n=== Publicação concluída! ===" -ForegroundColor Green
Write-Host "Arquivos em: $publish"

Write-Host "`n=== Copiando executável versionado para builds\ ===" -ForegroundColor Cyan
New-Item -ItemType Directory -Force $builds | Out-Null
$versionedExe = Join-Path $builds "QuickStartup-v$version.exe"
Copy-Item (Join-Path $publish "QuickStartup.exe") $versionedExe -Force
Write-Host "Gerado: $versionedExe"

if (-not $NoInstaller) {
    $innoPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    if (Test-Path $innoPath) {
        Write-Host "`n=== Compilando instalador (Inno Setup) ===" -ForegroundColor Cyan
        New-Item -ItemType Directory -Force "$root\installer\output" | Out-Null
        & $innoPath "$root\installer\setup.iss"
        Write-Host "`n=== Instalador gerado em installer\output\ ===" -ForegroundColor Green
    } else {
        Write-Host "`n[AVISO] Inno Setup não encontrado em '$innoPath'." -ForegroundColor Yellow
        Write-Host "        Instale em https://jrsoftware.org/isdl.php e rode novamente."
        Write-Host "        Ou pule o instalador com: .\build.ps1 -NoInstaller"
    }
}
