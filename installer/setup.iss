; ───────────────────────────────────────────────────────────────────────────
;  QuickStartup — Script Inno Setup
;  Para compilar: abrir este arquivo com o Inno Setup Compiler
;  Download Inno Setup: https://jrsoftware.org/isdl.php
; ───────────────────────────────────────────────────────────────────────────

#define AppName      "QuickStartup"
; Fonte única de verdade é <Version> em QuickStartup.csproj — build.ps1 (local e no CI)
; repassa o valor via "/DAppVersion=..." na linha de comando do ISCC. O define abaixo só
; serve de fallback para compilar este .iss direto pelo Inno Setup Compiler sem esse parâmetro.
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define AppPublisher "Seu Nome"
#define AppExeName   "QuickStartup.exe"
#define AppId        "{{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}"
; Mesmo GUID do mutex de instância única em App.xaml.cs (SingleInstanceId) — permite que o
; instalador detecte o app rodando e feche/reabra sozinho durante uma atualização silenciosa.
#define AppMutexName "QuickStartup-9f1e6b0a-3f0f-4a3f-a2b0-11f4d2a5c111"

; Caminho de saída do publish self-contained (ajuste se necessário)
#define PublishDir   "..\QuickStartup\bin\Release\net8.0-windows\win-x64\publish"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes
OutputDir=.\output
OutputBaseFilename=QuickStartup-Setup-v{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesInstallIn64BitMode=x64
; Auto-update silencioso (ver UpdateService.cs): permite fechar e reabrir o app sozinho.
AppMutex={#AppMutexName}
CloseApplications=yes
RestartApplications=yes

; Pede ao usuário se deseja iniciar com o Windows durante a instalação
[Tasks]
Name: "startupregistry"; Description: "Iniciar {#AppName} com o Windows"; GroupDescription: "Opções adicionais:"; Flags: unchecked

[Files]
; Copia todos os arquivos da pasta publish (self-contained)
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Atalho no Menu Iniciar
Name: "{group}\{#AppName}";           Filename: "{app}\{#AppExeName}"
Name: "{group}\Desinstalar {#AppName}"; Filename: "{uninstallexe}"
; Atalho na Área de Trabalho (opcional)
Name: "{autodesktop}\{#AppName}";     Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na Área de Trabalho"; GroupDescription: "Atalhos:"; Flags: unchecked

[Registry]
; Registra no Windows para iniciar automaticamente (só se o usuário marcou a opção)
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; \
  ValueType: string; ValueName: "QuickStartup"; \
  ValueData: """{app}\{#AppExeName}"" --startup"; \
  Flags: uninsdeletevalue; Tasks: startupregistry

[Run]
; Sem "skipifsilent" de propósito: também deve reabrir o app depois de uma atualização
; silenciosa (/VERYSILENT) disparada pelo próprio QuickStartup — ver UpdateService.cs.
Filename: "{app}\{#AppExeName}"; Description: "Iniciar {#AppName} agora"; Flags: nowait postinstall

[UninstallRun]
Filename: "reg"; Parameters: "delete ""HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"" /v ""QuickStartup"" /f"; Flags: runhidden; StatusMsg: "Removendo entrada de inicialização..."

[Code]
// Nada extra necessário
