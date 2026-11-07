# CLAUDE.md — Music Player

Orientações para trabalhar neste repositório. Visão geral e funcionalidades no [README.md](README.md).

## O que é

Tocador de músicas **WinUI 3** (Windows App SDK 2.x, `net10.0-windows10.0.26100.0`, mínimo Windows 10 2004/19041, somente x64) com visual Fluent do Windows 11. Segue as convenções do projeto `D:\Fontes\MarkdownEditor` (nomes em português, `JanelaPrincipal` partial, `Textos`/`Resources.resw`, instalador Inno Setup) e a organização em biblioteca + app do `D:\Fontes\OracleTools2026`.

## Solução (`MusicPlayer.slnx`)

```
MusicPlayer.Nucleo/          # biblioteca sem WinUI: regras da fila, formatos, metadados, preferências
├── FilaReproducao.cs        # ObservableCollection<ItemFila> + Atual (por referência) + próximo/anterior/remoção
├── FiltroFila.cs            # filtro da fila: todas as palavras em título/artista/álbum/arquivo, sem maiúsculas nem acentos
├── ItemFila.cs              # ObservableObject (CommunityToolkit.Mvvm, propriedades partial)
├── ModoRepeticao.cs         # Desligado, Todas, Uma
├── FormatosAudio.cs         # extensões suportadas (manter igual ao Instalador.iss)
├── ExpansorCaminhos.cs      # arquivos + pastas (recursivo) → lista de músicas
├── LeitorMetadados.cs       # tags pelo Windows: arquivo a arquivo (GetMusicPropertiesAsync) ou pasta inteira em lote (LerPastaAsync, prefetch)
├── EditorMetadados.cs       # lê e grava TagsMusica (MusicProperties.SavePropertiesAsync), só .mp3 e .m4a
├── ArgumentosAtivacao.cs    # linha de comando: [--enfileirar] <arquivos/pastas>
├── Configuracoes.cs         # %LocalAppData%\MusicPlayer\configuracoes.json (idioma, volume, mudo, repetição, painel da capa)
├── EstadoFila.cs            # %LocalAppData%\MusicPlayer\fila.json (fila com as tags em MusicaSalva, item atual e posição)
└── FormatoTempo.cs, MetadadosMusica.cs, MusicaSalva.cs, TagsMusica.cs, ResultadoRemocao.cs, PastasAplicativo.cs, ContextoJson.cs
MusicPlayer/                 # app WinUI 3 não empacotado
├── Program.cs               # Main próprio (DISABLE_XAML_GENERATED_MAIN): instância única
├── App.xaml(.cs)            # idioma, exceções não tratadas, repassa ativações para a janela
├── JanelaPrincipal.xaml(.cs) e partials .Reproducao, .Fila, .Filtro, .Carga, .ArrastarSoltar, .Ativacao, .Dialogos, .Metadados, .Sobre, .Idioma
├── Textos.cs                # textos de Strings\<idioma>\Resources.resw
├── LinhaComando.cs, NativeMethods.cs
└── Instalador/Instalador.iss
```

## Arquitetura

- **Núcleo x app**: tudo que não depende de interface fica no `MusicPlayer.Nucleo`. O projeto não tem testes automatizados; a validação é manual, rodando o app. O núcleo usa WinRT só para ler e gravar tags (`Windows.Storage`), nunca WinUI. O app (`JanelaPrincipal`) liga a fila à interface e ao `MediaPlayer`.
- **Instância única** (`Program.cs`, padrão da Microsoft para apps sem pacote): `AppInstance.FindOrRegisterForKey`; uma segunda instância chama `RedirectActivationToAsync` (aguardado com `CreateEvent` + `CoWaitForMultipleObjects`, sem bloquear a STA), passa o foco com `SetForegroundWindow` e fecha. Na principal, `AppInstance.Activated` (thread de fundo) → `LinhaComando.Separar` (`CommandLineToArgvW`) → `ArgumentosAtivacao.Interpretar` → `App.ReceberAtivacao` → `DispatcherQueue` → `JanelaPrincipal.ReceberAtivacao`.
- **Ativações em lote** (`JanelaPrincipal.Ativacao.cs`): o Explorer abre um processo por arquivo selecionado; o que chega dentro de `IntervaloLoteAtivacaoMs` é tratado junto. "Abrir" (duplo clique) insere logo após a música atual e toca o primeiro; `--enfileirar` ("Adicionar à fila") vai para o fim sem interromper. Na abertura, a fila salva é restaurada (pausada) e só depois os argumentos são aplicados, pelo mesmo caminho.
- **Fila** (`FilaReproducao`): o atual é guardado **por referência**, por isso a reordenação do `ListView` (`CanReorderItems`, que faz Remove/Insert na `ObservableCollection`) não afeta a reprodução, e o próximo segue a nova ordem. `ObterProximo(automatico)`: "Uma" só repete quando a música termina sozinha. Remover o atual devolve o `Seguinte`. Embaralhar: `SortearOrdem` põe o atual no topo e o resto em ordem aleatória (`RandomNumberGenerator.Shuffle`); o app (`EmbaralharAsync`) aplica com `MoverPara` (`Move`, para não recriar os itens do `ListView`) de uma vez, para a lista ser desenhada uma vez só (em fatias ela piscava). Durante o embaralhamento o ícone do `abbEmbaralhar` vira `E895` (Sync) girando por animação do compositor (`ElementCompositionPreview`, `RotationAngleInDegrees`), que continua com a thread da interface ocupada — iniciada dois quadros antes do trabalho e com no mínimo uma volta (700 ms); com "Efeitos de animação" desligado no Windows só troca o ícone. A pesquisa fica desabilitada (`Ocupado`); o botão não (desabilitado apagaria o ícone), cliques repetidos são ignorados por `_embaralhando`.
- **Reprodução** (`JanelaPrincipal.Reproducao.cs`): `Windows.Media.Playback.MediaPlayer` (Media Foundation; sem bibliotecas de áudio de terceiros). Cada música vira um `MediaPlaybackItem` com `ApplyDisplayProperties` (título, artista, álbum e capa no painel de mídia do Windows). Próximo/Anterior das teclas de mídia chegam por `CommandManager.NextReceived/PreviousReceived` (com `EnablingRule = Always`). Eventos do player chegam em threads de fundo → `_filaInterface.TryEnqueue`. `_versaoCarga` descarta cargas antigas quando outra música é pedida no meio. `_ocioso` = nada carregado ou fim da fila (arquivos adicionados começam a tocar). Falha de mídia avisa na `InfoBar` e passa para a próxima; se todas falharem seguidas, para.
- **Barra de posição**: atualizada por `DispatcherQueueTimer` (250 ms). O `Slider` trata o ponteiro, então `PointerPressed/Released/CaptureLost` são registrados com `handledEventsToo`; durante o arraste só o tempo exibido muda, e a busca acontece ao soltar.
- **Capa**: `GetThumbnailAsync(ThumbnailMode.MusicView)`; quando o tipo não é `Image` (o Windows devolve o ícone do arquivo), mostra o glifo padrão. A miniatura fica viva em `_capaAtual` porque o SMTC lê o stream depois.
- **Edição de tags** (`JanelaPrincipal.Metadados.cs`, menu de contexto da fila "Editar informações..."): título, artista, álbum, faixa, ano e gênero, gravados pelo property handler do Windows (sem bibliotecas de terceiros; a capa não é editável por essa API). Só `.mp3` e `.m4a` (`EditorMetadados.PodeEditar`), onde a gravação é garantida. O nome do arquivo no topo é selecionável (copiar para os campos); sem título nem artista, `EditorMetadados.SugerirPeloNome` preenche os dois com "Artista - Título" ou "Artista_Título" (primeiro ` - `; sem ele, primeiro `-` ou `_` da esquerda), e a sugestão só é gravada se o usuário salvar. Em lote, "Preencher artista e título pelo nome" (`abbPreencherPeloNome`, no menu "Mais" da CommandBar, em `SecondaryCommands`, por ser pouco usado) confirma num diálogo e aplica a mesma regra a toda a fila (`PreencherPeloNomeAsync`): faz a triagem pelas tags carregadas, relê cada arquivo e só grava se título e artista continuarem vazios, um por vez, por `GravarTagsAsync`. Enquanto `_preenchendo`, `lvwFila`, a pesquisa e o embaralhar ficam desabilitados, e o progresso aparece no `prbCarga` e em "Atualizando X de Y" no resumo; o resultado sai na `InfoBar`. Se for a música carregada, o `MediaPlayer` mantém o arquivo aberto: a fonte é solta antes de gravar (`SalvarAsync` repete em caso de violação de compartilhamento) e recarregada na mesma posição e estado, preservando `_ocioso`. Depois de gravar, o `ItemFila` relê as tags pelo `LeitorMetadados`; título vazio volta ao nome do arquivo.
- **Carga das tags** (`JanelaPrincipal.Carga.cs`): 8 leitores (`LeiturasSimultaneasMetadados`) consomem `_pendentes`, sempre começando pelo que está visível (`FirstVisibleIndex..LastVisibleIndex` do `ItemsStackPanel`); pastas adicionadas também são lidas em lote por `LeitorMetadados.LerPastaAsync` (consulta do Windows com `SetPropertyPrefetch`, usa o índice quando há). Resumo, filtro e progresso são atualizados por um temporizador de 200 ms que limita a frequência sem adiar (`AgendarAtualizacaoFila`), nunca a cada item (`DuracaoTotal` soma a fila inteira). Enquanto `EmCarga`, a pesquisa (limpa se estava ativa) e o embaralhar ficam desabilitados, com `prbCarga` e "Carregando X de Y" no resumo. Tags lidas fora dos leitores (tocar, editar) saem dos pendentes por `MarcarLidoForaDaCarga`; limpar a fila incrementa `_geracaoCarga`.
- **Filtro** (`JanelaPrincipal.Filtro.cs`, `asbFiltro` no cartão da fila): filtra enquanto digita (`DispatcherQueueTimer` de 150 ms; mudanças na fila e nas tags reaplicam pelo lote da carga). Com filtro, o `lvwFila` mostra `_itensFiltrados`, sincronizada por Move/Insert/Remove (sem recriar itens nem perder a seleção), e a reordenação fica desligada; a fila real não muda (próximo/anterior seguem a ordem completa). `RolarAteItem` só rola se o item está visível. Esc limpa; ao voltar à fila completa, a música atual fica selecionada e à vista. Resumo "X de Y" e `pnlFilaVazia` com texto próprio quando nada corresponde.
- **Música atual na barra** (`btnMusicaAtual`, padrão do Media Player do Windows 11): miniatura, título e artista à esquerda dos botões de transporte, preenchidos em `ExibirTocandoAgora`; a capa é a mesma `BitmapImage` do painel (`MostrarImagemCapa`). Clicar seleciona e rola até a música, limpando o filtro se ela estiver escondida. Conforme a largura da coluna (`grdMusicaAtual`, `AplicarLarguraMusicaAtual`), primeiro some a capa e depois o botão inteiro.
- **Arrastar e soltar** (`JanelaPrincipal.ArrastarSoltar.cs`): `DragOver`/`Drop` no `grdRaiz` com `handledEventsToo` (o `ListView` marca o arraste como tratado). Só `StandardDataFormats.StorageItems` é aceito, o que separa arquivos do Explorer do arraste interno de reordenação. Pastas são expandidas (`ExpansorCaminhos`, em `Task.Run`).
- **Layout**: duas colunas (Tocando agora | Fila); abaixo de `LarguraLayoutEmpilhado` o `AplicarLayout` (no `SizeChanged` do `grdCorpo`) empilha e reduz a capa. Feito em código porque `AdaptiveTrigger` em `Window` não é confiável. `AplicarPainelTocando` (botão `abbPainelTocando`) oculta o painel da capa e zera a coluna e os espaçamentos. Na segunda linha da fila, dois `Run`: `ItemFila.PrefixoSubtitulo` (artista · álbum, quando houver) e o `Caminho` completo, sempre por último e mais apagado (`TextFillColorDisabledBrush`).
- **Teclado**: Espaço é tratado no `PreviewKeyDown` do `grdRaiz` (antes de botões e da lista), exceto quando o foco está num campo de texto ou fora da árvore do `grdRaiz` (diálogos e menus ficam na camada de popups, mas a tecla também passa por ali) — `EspacoEhDoPlayer`; Ctrl+←/→, Ctrl+O e Ctrl+F (no `grdRaiz`, foca o filtro) por `KeyboardAccelerator`; Enter/Delete no `KeyDown` do `lvwFila`.
- **Persistência**: no `Closed` salva `Configuracoes` (volume, mudo, repetição, painel da capa) e `EstadoFila`. A fila guarda as tags e a data do arquivo (`MusicaSalva`, `File.GetLastWriteTimeUtc` lido junto com as tags); na restauração, arquivos inalterados voltam com as tags salvas e só os demais passam pelos leitores. Arquivos que não existem mais são ignorados; o formato antigo (`arquivos`, só caminhos) ainda é lido.
- **Idioma**: igual ao MarkdownEditor — `Textos` (ResourceManager do MRT Core com contexto próprio; troca vale na hora), `App.DefinirIdiomaDoWinUI` (`PrimaryLanguageOverride`), menu `abbIdioma` com `RadioMenuFlyoutItem`, `AplicarIdioma()` no construtor e a cada troca. Botões só com ícone usam `DefinirDica` (tooltip + `AutomationProperties.Name`).

## Convenções

- Nomes de variáveis, métodos e controles em português. Textos de interface sempre por `Textos.Obter`/`Textos.Formatar`, com a chave nos três `Resources.resw` (pt-BR, en-US, es-ES); nunca texto fixo no C# ou no XAML (exceção: nomes dos idiomas no menu Idioma).
- Prefixos de controles: `tlb` (TitleBar), `cbr` (CommandBar), `abb` (AppBarButton), `btn` (Button), `sld` (Slider), `lvw` (ListView), `txt` (TextBlock), `txb` (TextBox), `asb` (AutoSuggestBox), `prb` (ProgressBar), `nmb` (NumberBox), `img` (Image), `fnt` (FontIcon), `ifb` (InfoBar), `grd` (Grid), `brd` (Border), `pnl` (StackPanel), `col`/`row` (definições do Grid), `mfo`/`mfi` (MenuFlyout/Item), `rmi` (RadioMenuFlyoutItem).
- C# moderno: file-scoped namespaces, primary constructors/records, collection expressions, pattern matching.
- Cores só por `ThemeResource`; glifos da Segoe Fluent Icons. Estilos compartilhados em `App.xaml` (`CartaoStyle`, `BotaoTransporteStyle`, `BotaoTocarStyle`).
- Nenhum `async void` além do `Disparar(tarefa)`: event handlers assíncronos são `=> Disparar(XxxAsync())`. O `Disparar` trata a exceção (grava em `RegistroErros` e mostra o erro), porque uma exceção relançada por `async void` volta pela `DispatcherQueue` sem passar pelo `Application.UnhandledException` e o WinUI encerra o processo (código `0xc000027b`). Erros inesperados ficam em `%LocalAppData%\MusicPlayer\erros.log`.
- P/Invoke só sem alternativa no WinUI/Windows App SDK/WinRT, em `NativeMethods.cs` com `LibraryImport`.
- Não adicionar comentários XML `<summary>`.
- `AnalysisLevel` = `latest-recommended`; o build deve ficar sem avisos.
- Ao mudar a lista de formatos, atualizar `FormatosAudio.Extensoes` **e** o `Instalador.iss`.

## Build

- Não compilar nem rodar build automaticamente após edições; o usuário compila no Visual Studio e informa os erros.
- Solução só com a plataforma x64, cada projeto mapeado com `<Platform Project="x64" />` no `.slnx` (sem isso o Visual Studio avisa "unknown project configuration mappings").
- App não empacotado (`WindowsPackageType=None`), Windows App SDK self-contained, `EnableMsixTooling=true` (gera e publica o `MusicPlayer.pri` com XAML e textos), .NET framework-dependent.
- Pacotes: app — `Microsoft.WindowsAppSDK`, `Microsoft.Windows.SDK.BuildTools`; núcleo — `CommunityToolkit.Mvvm` (`LangVersion` preview).

## Instalador

- Inno Setup (`iscc` no PATH): `MusicPlayer\Instalador\Instalador.iss` e `Release.cmd` (raiz): `dotnet publish` para `MusicPlayer\bin\Publicar` → apaga o `MusicPlayer-*.exe` anterior → `iscc`.
- `[Files]` copia só o que está listado: arquivos da raiz do publish, `Assets`, `Microsoft.UI.Xaml` e as pastas de idioma do WinUI dos idiomas do app (`pt-BR`, `en-US`, `es-ES`); as demais pastas de idioma ficam de fora. Ao atualizar o Windows App SDK, conferir se o `bin\Publicar` ganhou alguma pasta nova que não seja de idioma e incluí-la no `.iss`. Ao adicionar um idioma ao app, incluir a pasta correspondente.
- `AppId=Music Player` fixo (não alterar). Verifica o .NET 10 Runtime.
- Registro (`HKA`): ProgID `MusicPlayer.Audio` (abrir = `"MusicPlayer.exe" "%1"`), `Applications\MusicPlayer.exe\SupportedTypes` e `OpenWithProgids` de cada extensão ("Abrir com"), verbo "Adicionar à fila do Music Player" em `SystemFileAssociations\audio\shell` (todo arquivo de áudio, com `--enfileirar`) e tarefa opcional para ser o padrão do `.mp3`. Como no MarkdownEditor, o Windows pergunta o app padrão no primeiro duplo clique (`UserChoice`); isso é esperado.
- Antes de gerar, atualizar a versão no `Instalador.iss` (`AppVersion`, `OutputBaseFilename`), no `MusicPlayer.csproj` (`AssemblyVersion`, `FileVersion`, `InformationalVersion`) e no `app.manifest` (`X.Y.Z.0`).
- O ícone `MusicPlayer\Assets\MusicPlayer.ico` foi gerado por script (gradiente + glifo de nota da Segoe Fluent Icons, 16 a 256 px).
