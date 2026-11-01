#define MyAppName "Lampik"
#define MyAppVersion "0.5.0-beta.1"
#define MyAppPublisher "Daniil Goncharov"
#define MyAppExeName "Lampik.exe"

[Setup]
AppId={{84B2B990-C406-47B9-9D7E-3759349BC6B3}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\Lampik
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=..\release-installer
OutputBaseFilename=Lampik-Setup-0.5.0-beta.1-win-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[CustomMessages]
english.DesktopGroup=Shortcuts:
russian.DesktopGroup=Ярлыки:
english.DesktopIcon=Create a desktop shortcut
russian.DesktopIcon=Создать ярлык на рабочем столе
english.Launch=Launch Lampik
russian.Launch=Запустить Лампик

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; GroupDescription: "{cm:DesktopGroup}"; Flags: unchecked

[Files]
Source: "..\release\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:Launch}"; Flags: nowait postinstall skipifsilent
