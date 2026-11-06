# Music Player

Tocador de músicas para Windows 10/11 feito em **WinUI 3** (Windows App SDK) e **.NET 10**, com o visual Fluent do Windows 11 (Mica, barra de título integrada, tema claro/escuro do Windows).

> Sem propagandas, sem coleta de dados, sem acesso a internet, apenas um tocador de músicas.

## Funcionalidades

- **Duplo clique em uma música** abre o programa e toca. Com o programa já aberto, a **mesma janela** recebe o arquivo: ele entra logo depois da música atual e começa a tocar. Vários arquivos abertos juntos no Explorer entram em sequência.
- **Fila de reprodução**: as músicas tocam na ordem da fila; ao terminar uma, começa a próxima.
- **Arrastar e soltar** arquivos ou pastas do Explorer em qualquer ponto da janela: entram no fim da fila (pastas incluem as subpastas).
- **Reordenar a fila** arrastando os itens com o mouse, ou **embaralhar** (a música atual vai para o início e as demais ficam em ordem aleatória).
- Botão **Capa** para mostrar ou ocultar o painel "Tocando agora" (a escolha é salva).
- Tocar/pausar, anterior, próxima, barra de posição, volume e mudo, repetir (desligado / todas / esta música).
- Título, artista, álbum, duração e capa lidos das tags do arquivo; integração com as **teclas de mídia** e o painel de mídia do Windows.
- Menu de contexto da fila: tocar, abrir local do arquivo e remover.
- A fila, a música atual e a posição são salvas ao fechar e restauradas ao abrir.
- Interface em português, inglês e espanhol (menu Idioma).
- Verbo "Adicionar à fila do Music Player" no menu de contexto do Explorer (instalado pelo instalador).

## Formatos

`.mp3`, `.m4a`, `.aac`, `.wav`, `.wma`, `.flac`, `.alac`, `.ogg`, `.oga`, `.opus`, `.ac3`, `.ec3`, `.3gp`, `.amr`, `.adts` — os decodificados pelo próprio Windows (Media Foundation). Ogg e Opus usam as Web Media Extensions, que já vêm no Windows 11.

## Atalhos

| Tecla | Ação |
| --- | --- |
| Espaço | Tocar / pausar |
| Ctrl+→ / Ctrl+← | Próxima / anterior |
| Ctrl+O | Adicionar arquivos |
| Enter | Tocar a música selecionada |
| Delete | Remover as selecionadas da fila |
| F1 | Sobre |
| Teclas de mídia | Tocar/pausar, próxima, anterior |

## Projetos

- `MusicPlayer` — aplicativo WinUI 3 (não empacotado, Windows App SDK incluído).
- `MusicPlayer.Nucleo` — biblioteca com a fila, formatos, metadados e preferências (sem interface).

O projeto não tem testes automatizados.

Requisitos para executar: Windows 10 2004 ou superior (x64) e o .NET 10 Runtime.

## Gerar o instalador

Com o [Inno Setup](https://jrsoftware.org/isinfo.php) (`iscc` no PATH), execute `Release.cmd` na raiz. O instalador `MusicPlayer-X.Y.Z.exe` é gerado em `MusicPlayer\Instalador\`. Ele registra o programa no "Abrir com" de todos os formatos suportados e, opcionalmente, como padrão dos arquivos `.mp3`.
