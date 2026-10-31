; ============================================================
; OrbisExtract — Instalador para Windows (Inno Setup 6)
;
; Compila (a partir da raiz do repositório):
;   dotnet publish src\OrbisExtract.App\OrbisExtract.App.Desktop\OrbisExtract.App.Desktop.csproj `
;     -c Release -r win-x64 --self-contained true -o artifacts\windows
;   "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" packaging\windows\orbisextract.iss
;
; Todos os caminhos são relativos ao diretório deste script.
; ============================================================

#ifndef AppVersion
  #define AppVersion "1.0.4"
#endif

#define AppName "OrbisExtract"
#define AppExeName "OrbisExtract.App.Desktop.exe"

[Setup]
; GUID fixo: é o que permite detectar versões antigas e instalar por cima (update)
AppId={{6E1F0B8A-4B34-4C7A-9D21-8A5C2E7F3B91}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=flugeru
AppPublisherURL=https://github.com/flugeru/OrbisExtract
AppSupportURL=https://github.com/flugeru/OrbisExtract/issues
AppUpdatesURL=https://github.com/flugeru/OrbisExtract/actions
DefaultDirName={autopf}\{#AppName}
; Always show folder selection, including when updating an existing install.
DisableDirPage=no
DisableProgramGroupPage=yes
; Avoid the oversized empty summary panel shown on the Ready page.
DisableReadyMemo=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\..\artifacts\installer
OutputBaseFilename=OrbisExtract-Setup-x64
SetupIconFile=orbisextract.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern dark windows11 includetitlebar hidebevels
; Neutral charcoal background matching the requested #252525 reference.
; Native dark controls are styled by Inno Setup; its built-in accent
; is not independently configurable without a custom VCL style file.
WizardBackColor=#252525
WizardImageBackColor=#252525
WizardSmallImageBackColor=#252525
WizardImageFile=wizard-brand.png
WizardSmallImageFile=
; Comportamento de atualização: com o mesmo AppId, o instalador lê as
; configurações da versão anterior do registro e instala por cima.
UsePreviousAppDir=no
UsePreviousGroup=yes
; Fecha o programa se estiver rodando durante a instalação
CloseApplications=yes

; Avoid the native Windows blue highlight in the language dropdown.
; Inno Setup still detects the Windows UI language automatically.
ShowLanguageDialog=no

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"; LicenseFile: "license.pt-BR.txt"
Name: "english"; MessagesFile: "compiler:Default.isl"; LicenseFile: "license.en.txt"

; The Inno Setup language selection follows Windows, with manual override.
; Built-in dark styling preserves high contrast accessibility and native controls.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; Programa publicado (self-contained): exe + dlls + runtimes
Source: "..\..\artifacts\windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Executar {#AppName}"; Flags: nowait postinstall skipifsilent
