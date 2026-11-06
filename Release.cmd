@echo off
setlocal

REM 1. Publicar em Release (x64): .NET framework-dependent e Windows App SDK self-contained
if exist MusicPlayer\bin\Publicar rmdir /s /q MusicPlayer\bin\Publicar
dotnet publish MusicPlayer\MusicPlayer.csproj -c Release -r win-x64 --self-contained false -p:Platform=x64 -o MusicPlayer\bin\Publicar
if errorlevel 1 (
    echo Erro ao publicar o projeto.
    pause
    exit /b 1
)

REM 2. Excluir o arquivo do instalador anterior, se existir
if exist MusicPlayer\Instalador\MusicPlayer-*.exe (
    del /f /q MusicPlayer\Instalador\MusicPlayer-*.exe
)

REM 3. Gerar o instalador com o Inno Setup
iscc MusicPlayer\Instalador\Instalador.iss
if errorlevel 1 (
    echo Erro ao gerar o instalador.
    pause
    exit /b 1
)

echo Processo concluido com sucesso.
endlocal
pause
