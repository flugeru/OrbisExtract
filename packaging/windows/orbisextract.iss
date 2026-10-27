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
  #define AppVersion "1.0.3"
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
DisableProgramGroupPage=yes
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
WizardStyle=modern
LicenseFile=..\..\LICENSE
; Comportamento de atualização: com o mesmo AppId, o instalador lê as
; configurações da versão anterior do registro e instala por cima.
UsePreviousAppDir=yes
UsePreviousGroup=yes
; Fecha o programa se estiver rodando durante a instalação
CloseApplications=yes

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; GroupDescription: "Atalhos:"

[Files]
; Programa publicado (self-contained): exe + dlls + runtimes
Source: "..\..\artifacts\windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Executar {#AppName}"; Flags: nowait postinstall skipifsilent
