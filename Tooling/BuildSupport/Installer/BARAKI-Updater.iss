#define AppVersion GetEnv("BARAKI_UPDATER_VERSION")
#if AppVersion == ""
#define AppVersion "0.0.0"
#endif

#define BuildDir GetEnv("BARAKI_UPDATER_BUILD_DIR")
#if BuildDir == ""
#define BuildDir "..\..\build\Updater"
#endif

#define InstallerOutDir GetEnv("BARAKI_INSTALLER_OUTPUT_DIR")
#if InstallerOutDir == ""
#define InstallerOutDir "..\..\dist"
#endif

[Setup]
AppId={{B12721F5-AD48-4D37-BF5D-48D16037D0B7}
AppName=BARAKI
AppPublisher=Unio Games
AppVersion={#AppVersion}
DefaultDirName={autopf}\BARAKI
DefaultGroupName=BARAKI
DisableProgramGroupPage=yes
OutputDir={#InstallerOutDir}
OutputBaseFilename=BARAKI-Setup
SetupIconFile=BARAKI.ico
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\BARAKI.exe
WizardStyle=modern

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; GroupDescription: "Ярлыки:"

[Files]
Source: "{#BuildDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\BARAKI"; Filename: "{app}\BARAKI.exe"
Name: "{autodesktop}\BARAKI"; Filename: "{app}\BARAKI.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\BARAKI.exe"; Description: "Запустить BARAKI"; Flags: nowait postinstall skipifsilent
