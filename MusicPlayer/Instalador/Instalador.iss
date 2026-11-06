[Languages]
Name: "brazilian"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Setup]
; Identificador fixo da instalação, para upgrades e desinstalação estáveis
AppId=Music Player
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
AppName=Music Player
AppVersion=1.0.0
AppPublisher=Filipe Kanitz
DefaultDirName={commonpf}\MusicPlayer
DefaultGroupName=MusicPlayer
OutputDir=.
SetupIconFile="..\Assets\MusicPlayer.ico"
UninstallDisplayIcon={app}\MusicPlayer.exe
OutputBaseFilename=MusicPlayer-1.0.0
Compression=lzma
SolidCompression=yes
DisableProgramGroupPage=yes
ChangesAssociations=yes

[Files]
; Copia todo o resultado do dotnet publish (inclui Assets e o Windows App SDK self-contained)
Source: "..\bin\Publicar\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Music Player"; Filename: "{app}\MusicPlayer.exe"
Name: "{commondesktop}\Music Player"; Filename: "{app}\MusicPlayer.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; GroupDescription: "Opções adicionais:"
Name: "associarmp3"; Description: "Abrir arquivos .mp3 com o Music Player por padrão"; GroupDescription: "Opções adicionais:"; Flags: unchecked

[Registry]
; ProgID usado pelo "Abrir com" e pela associação padrão
Root: HKA; Subkey: "Software\Classes\MusicPlayer.Audio"; ValueType: string; ValueName: ""; ValueData: "Arquivo de áudio do Music Player"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\MusicPlayer.Audio\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\MusicPlayer.exe,0"
Root: HKA; Subkey: "Software\Classes\MusicPlayer.Audio\shell"; ValueType: string; ValueName: ""; ValueData: "open"
Root: HKA; Subkey: "Software\Classes\MusicPlayer.Audio\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\MusicPlayer.exe"" ""%1"""

; "Adicionar à fila do Music Player" no menu de contexto de todo arquivo de áudio, seja qual for o app padrão
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\audio\shell\MusicPlayer.Enfileirar"; ValueType: string; ValueName: ""; ValueData: "Adicionar à fila do Music Player"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\audio\shell\MusicPlayer.Enfileirar"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\MusicPlayer.exe,0"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\audio\shell\MusicPlayer.Enfileirar\command"; ValueType: string; ValueName: ""; ValueData: """{app}\MusicPlayer.exe"" --enfileirar ""%1"""

; Registro do aplicativo
Root: HKA; Subkey: "Software\Classes\Applications\MusicPlayer.exe"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "Music Player"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\MusicPlayer.exe\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\MusicPlayer.exe"" ""%1"""

; Extensões suportadas (manter igual a FormatosAudio.Extensoes): SupportedTypes e opção no "Abrir com"
Root: HKA; Subkey: "Software\Classes\Applications\MusicPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".mp3"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\MusicPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".m4a"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\MusicPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".aac"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\MusicPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".wav"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\MusicPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".wma"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\MusicPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".flac"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\MusicPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".alac"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\MusicPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".ogg"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\MusicPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".oga"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\MusicPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".opus"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\MusicPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".ac3"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\MusicPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".ec3"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\MusicPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".3gp"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\MusicPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".amr"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\MusicPlayer.exe\SupportedTypes"; ValueType: string; ValueName: ".adts"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.mp3\OpenWithProgids"; ValueType: string; ValueName: "MusicPlayer.Audio"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.m4a\OpenWithProgids"; ValueType: string; ValueName: "MusicPlayer.Audio"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.aac\OpenWithProgids"; ValueType: string; ValueName: "MusicPlayer.Audio"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.wav\OpenWithProgids"; ValueType: string; ValueName: "MusicPlayer.Audio"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.wma\OpenWithProgids"; ValueType: string; ValueName: "MusicPlayer.Audio"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.flac\OpenWithProgids"; ValueType: string; ValueName: "MusicPlayer.Audio"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.alac\OpenWithProgids"; ValueType: string; ValueName: "MusicPlayer.Audio"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.ogg\OpenWithProgids"; ValueType: string; ValueName: "MusicPlayer.Audio"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.oga\OpenWithProgids"; ValueType: string; ValueName: "MusicPlayer.Audio"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.opus\OpenWithProgids"; ValueType: string; ValueName: "MusicPlayer.Audio"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.ac3\OpenWithProgids"; ValueType: string; ValueName: "MusicPlayer.Audio"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.ec3\OpenWithProgids"; ValueType: string; ValueName: "MusicPlayer.Audio"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.3gp\OpenWithProgids"; ValueType: string; ValueName: "MusicPlayer.Audio"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.amr\OpenWithProgids"; ValueType: string; ValueName: "MusicPlayer.Audio"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.adts\OpenWithProgids"; ValueType: string; ValueName: "MusicPlayer.Audio"; ValueData: ""; Flags: uninsdeletevalue

; Associação padrão de .mp3 (opcional)
Root: HKA; Subkey: "Software\Classes\.mp3"; ValueType: string; ValueName: ""; ValueData: "MusicPlayer.Audio"; Flags: uninsdeletevalue; Tasks: associarmp3

[Run]
; Executa o programa após a instalação
Filename: "{app}\MusicPlayer.exe"; Description: "Executar o Music Player"; Flags: nowait postinstall skipifsilent

[Code]
const
  UrlDotNet = 'https://dotnet.microsoft.com/download/dotnet/10.0';

// O WinUI 3 usa só o runtime base do .NET (não o Desktop Runtime do WinForms/WPF).
function DotNetInstalado: Boolean;
var
  Busca: TFindRec;
begin
  Result := FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.NETCore.App\10.*'), Busca);
  if Result then
    FindClose(Busca);
end;

function InitializeSetup: Boolean;
var
  CodigoErro: Integer;
begin
  Result := True;

  if not DotNetInstalado then
    if SuppressibleMsgBox('O Music Player precisa do .NET 10 Runtime (x64), que não foi encontrado neste computador.'
      + #13#10#13#10 + 'Deseja abrir a página de download agora? A instalação continuará normalmente.',
      mbConfirmation, MB_YESNO, IDNO) = IDYES then
      ShellExec('open', UrlDotNet, '', '', SW_SHOWNORMAL, ewNoWait, CodigoErro);
end;
