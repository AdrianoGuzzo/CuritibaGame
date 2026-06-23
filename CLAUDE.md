# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Visão geral

Curitiba é construído sobre **MonoGame 3.8**, com alvo .NET 8. É um jogo multiplataforma: uma única biblioteca compartilhada `Curitiba.Core` move quatro projetos "cabeça" finos por plataforma (DesktopGL, WindowsDX, Android, iOS). Quase todo o código vive em `Curitiba.Core`; os projetos de plataforma contêm apenas um ponto de entrada e o empacotamento específico da plataforma.

O repositório nasceu como um **platformer 2D com rolagem lateral** (código em `Game/`, fases em `Content/Levels/NN.txt`). A entrega ativa, porém, é uma **demo de beat 'em up** ("Capão Raso": Sofia vs. inimigos "Pia Loco"), implementada em `BeatEmUp/` sobre a mesma infraestrutura (telas, input, resolução, pipeline). O **"Play"** do menu agora abre o beat 'em up; o platformer continua no código mas **dormante** (não acessível pelo menu). Ao mexer no jogo atual, trabalhe em `BeatEmUp/`; o material em `Game/` é referência/reuso, não o alvo.

## Comandos

Execute todos os comandos a partir do diretório `Curitiba/` (onde ficam o `.slnx` e as pastas de projeto). O arquivo de solução é `Curitiba.slnx` na raiz do repositório.

```bash
# Restaura as ferramentas do pipeline de conteúdo do MonoGame (necessário uma vez antes do primeiro build)
dotnet tool restore

# Compila tudo
dotnet build Curitiba.slnx

# Executa o build desktop (cabeça OpenGL multiplataforma — alvo usual de desenvolvimento)
dotnet run --project Curitiba.DesktopGL

# Cabeça DirectX apenas para Windows
dotnet run --project Curitiba.WindowsDX
```

Android e iOS exigem os workloads .NET correspondentes (`dotnet workload install android` / `ios`) e normalmente são compilados/implantados a partir de uma IDE.

**Não há projeto de testes** neste repositório e nenhum linter configurado além do compilador C#.

## Pipeline de conteúdo

Os ativos (sprites, sons, fontes, planos de fundo) são compilados pelo pipeline de conteúdo do MonoGame, e **não** copiados crus. A fonte única de verdade é `Curitiba.Core/Content/Curitiba.mgcb`, referenciada por todos os projetos cabeça via `<MonoGameContentReference>`. O pacote `MonoGame.Content.Builder.Task` o compila automaticamente durante o build.

- Para alterar quais ativos são compilados, edite `Curitiba.mgcb` diretamente ou execute o editor: `dotnet mgcb-editor`.
- **Alguns arquivos são exceção (texto puro, fora do pipeline)** — copiados crus para a saída e lidos em runtime via `TitleContainer.OpenStream`:
  - As fases do platformer dormante (`Content/Levels/NN.txt`).
  - Os **dados do beat 'em up** (`Content/Data/Stages/*.json` e `*.tmj`), copiados pelos `.csproj` cabeça via `<Content>`/`AndroidAsset`/`BundleResource` (ver [Modo Beat 'em up](#modo-beat-em-up-beatemup)).

## Arquitetura

### Abstração de plataforma
`CuritibaGame` (`Curitiba.Core/CuritibaGame.cs`) é a subclasse de `Game` e o verdadeiro ponto de entrada; o `Program.cs` de cada cabeça apenas a instancia e chama `Run()`. As ramificações por plataforma são centralizadas em dois flags estáticos, `CuritibaGame.IsMobile` e `CuritibaGame.IsDesktop` (calculados a partir de `OperatingSystem.Is*()`). Eles escolhem a implementação de armazenamento de configurações, o comportamento de tela cheia/mouse e entrada por toque vs. teclado. Prefira esses flags em vez de reverificar o SO.

### Localizador de serviços (service locator)
Singletons compartilhados são registrados em `Game.Services` no construtor / `LoadContent` de `CuritibaGame` e recuperados em outros lugares com `Services.GetService<T>()`. Serviços registrados: `GraphicsDeviceManager`, `SettingsManager<CuritibaSettings>`, `SettingsManager<CuritibaLeaderboard>` e `ParticleManager`. Essa é a principal forma de os subsistemas obterem suas dependências — não há contêiner de DI.

### Pilha de telas (`ScreenManagers/ScreenManager.cs`)
Todo o modelo de UI/estado de jogo é uma pilha de objetos `GameScreen` gerenciada pelo `ScreenManager` (um `DrawableGameComponent` adicionado a `Game.Components`). Comportamentos-chave:
- As telas são atualizadas de cima para baixo; **a entrada é roteada apenas para a tela ativa mais ao topo e não coberta**.
- Telas `IsPopup` (pausa, caixas de mensagem) não cobrem as telas abaixo delas.
- As telas transicionam via `ScreenState` (`TransitionOn`/`Active`/`TransitionOff`/`Hidden`) com `TransitionOnTime`/`TransitionOffTime`.
- Adicione/remova com `AddScreen(screen, controllingPlayer)` / `RemoveScreen(screen)`; nunca altere a lista interna diretamente.

Fluxo: `Initialize()` empilha `BackgroundScreen` + `MainMenuScreen`. Os menus (`MainMenuScreen`, `SettingsScreen`, `AboutScreen`, `PauseScreen`) derivam de `MenuScreen`. O **"Play"** carrega `BeatEmUpScreen` (via `LoadingScreen.Load`); ao concluir a fase, ele transiciona para `EndOfDemoScreen` ("Fim da Demo"). O platformer (`GameplayScreen`) permanece no código mas não é mais aberto pelo menu.

### Independência de resolução
O jogo renderiza contra uma resolução virtual fixa `BaseScreenSize = 800×480`. `ScreenManager.ScalePresentationArea()` constrói `GlobalTransformation`, uma matriz de escala+letterbox passada para todo `SpriteBatch.Begin`. As coordenadas de entrada são desprojetadas com a matriz inversa (`inputState.UpdateInputTransformation`). Ao adicionar código de renderização ou entrada, sempre passe por esse transform em vez de usar pixels brutos do backbuffer.

### Modelo de gameplay (`Game/`)
`Level` (interno, dono do jogador, gemas, inimigos e da grade de tiles) é o núcleo de uma sessão de jogo, construído por `GameplayScreen.LoadNextLevel()`.
- **As fases são tilemaps ASCII**: `Content/Levels/NN.txt`, analisados caractere a caractere em `Level.LoadTile`. Cada caractere mapeia para um tile/entidade — ex.: `P` início do jogador, `X` saída, `1`–`4` gemas (por valor/power-up), `A`–`D` tipos de inimigo, `#` intransponível, `-`/`~` plataformas, `;` quebrável, `:` bloco transponível, `.` vazio. Todas as linhas devem ter o mesmo comprimento (validado no carregamento).
- `Level.NUMBER_OF_LEVELS` controla quantas fases existem; `GameplayScreen` percorre todas elas. A fase `00.txt` é tratada como tutorial (`onMainMenu`) e fica excluída de pontuação/leaderboard.
- Os tiles carregam um `TileCollision` (Passable / Impassable / Platform / Breakable). A física de `Player`/`Enemy` consulta `Level.GetCollision`. Planos de fundo em parallax são desenhados como três `Layer`s, com as entidades compostas no índice de camada `EntityLayer = 2`.
- Condição de vitória: todas as gemas coletadas, jogador no chão e o retângulo delimitador contendo a saída. A pontuação recompensa o tempo restante; um "recorde" exige simultaneamente um tempo mais rápido e 100% das gemas.

### Modo Beat 'em up (`BeatEmUp/`)
Modo isolado, sem física de gravidade nem tiles. Núcleo: `CapaoRasoArena` (análogo a `Level`) — dona de `SofiaPlayer`, `List<PiaLocoEnemy>`, `Camera2D` e das ondas. `BeatEmUpScreen` (análoga a `GameplayScreen`) carrega os dados do estágio, cria a arena e roteia o `InputState` a ela em `HandleInput`.
- **Dirigido por dados (importante)**: o cenário **não é mais hardcoded em C#**. A fonte canônica é `Content/Data/Stages/capao-raso.json` (classe `StageDefinition`, em `BeatEmUp/Data/`), carregada por `StageLoader` (System.Text.Json + `TitleContainer`). Cobre `corridor`, `backdrop`/parallax, `tuning` (Sofia/PiaLoco via `FighterTuning`), `personalities` e `sections[]` com `waves[]`/`spawns[]` e `setPieces[]`. `CapaoRasoArena` é construída a partir de um `StageDefinition`; os defaults no código (`FighterTuning.*Defaults()`, `StageDefinition.CapaoRasoDefault()`) reproduzem os valores antigos 1:1, então sem JSON o comportamento é idêntico. **Ao mexer em cenário/balanceamento, edite o JSON (ou o editor F1), não constantes.**
- **Combatentes**: `Fighter` (base, máquina de estados `FighterState` = Idle/Walk/Attack/Hit/KnockedDown/Dead; stats aplicados por `ApplyTuning(FighterTuning)`) → `SofiaPlayer` (8 direções, ataque no `Space`/A/X, vida 100, dano 10) e `PiaLocoEnemy` (IA persegue+ataca, vida 30, dano 5; recebe um `EnemyProfile` resolvido de `personalities`). `Position` é o **ponto dos pés** (centro-base); colisão por `HurtBox` (retângulo) vs. `AttackData` (hitbox temporária, ativa só nos frames do golpe, um acerto por alvo por golpe).
- **Câmera/ondas**: `Camera2D` segue a Sofia e **trava o avanço** via `MaxAdvanceX` até a área ser limpa. Cada onda (`SpawnArea`, mapeada de `WaveDef`) define o ponto de trava (`lockCameraX`) e os inimigos: `spawns[]` com posições/personalidades explícitas têm prioridade; sem eles, usa o spread procedural via `enemyCount`. Ao chegar ao fim da última seção → `Completed` → "Fim da Demo".
- **Hot-reload**: em desktop, `DevTools/StageHotReloader` (FileSystemWatcher na pasta-fonte) sinaliza mudanças; `BeatEmUpScreen.PollHotReload` recria a arena **na game thread** ao salvar o JSON (JSON inválido nunca derruba o jogo — mantém a arena anterior). Permite iterar no mapa sem recompilar.
- **Desenho**: fundo em espaço de tela (`GlobalTransformation`); mundo com `camera.GetTransform() * GlobalTransformation`; combatentes **ordenados por `Position.Y`** (mais baixo desenha por cima); set pieces e HUD desenhados na arena. Siga a convenção de hot-path (sem alocar por frame: `drawOrder` e o `Comparison` são reaproveitados).

### Editor in-game / dev tools (`BeatEmUp/Data/Tiled/`, `DevTools/`)
Editor de cena WYSIWYG com **ImGui.NET**, **só em desktop e build Debug**.
- **Toggle F1** abre o editor (congela a cena); edita o `StageDefinition` vivo — campos de corredor/fundo/tuning/personalidades, ondas e spawns, set pieces — com botões **Aplicar** (rebuild da arena), **Salvar** (grava o JSON), **Recarregar** e **Importar do Tiled**. Arrasta spawns/set pieces no mundo (gizmos via `ImGui.GetBackgroundDrawList`).
- **Isolamento**: todo o código ImGui fica sob `#if CURITIBA_DEVTOOLS`; em `Curitiba.Core.csproj` o símbolo + o `PackageReference` do `ImGui.NET` (1.91.6.1) + `AllowUnsafeBlocks` são **condicionados a `Debug`**. Release e mobile não incluem o nativo `cimgui`. Interface `IDevEditor` (sempre compilada) + `NullDevEditor` no-op; o `ImGuiDevEditor : DrawableGameComponent` real só existe no Debug, registrado em `Game.Services` e adicionado a `Components` **depois** do `ScreenManager` (para desenhar no backbuffer cheio). O renderer (`DevTools/ImGuiRenderer.cs` + `DrawVertDeclaration.cs`) é vendorizado do sample do ImGui.NET.
- **Tiled**: `BeatEmUp/Data/Tiled/TiledImporter` importa um `.tmj` para uma seção do `StageDefinition` (one-way; regenera fundo/zona-andável/ondas/spawns/set-pieces e preserva tuning/personalities). Convenções de camadas: `background`/`sky`/`buildings` (image layers), `spawns`/`setpieces`/`walkzone` (object layers); coordenadas do mapa = unidades do mundo virtual.
- **Gotcha de build**: feche o jogo antes de rebuildar — o processo `Curitiba` trava `Curitiba.Core.dll` na saída do DesktopGL e o build do head falha por **file-lock** (não é erro de código).

### Animação e sprites (`BeatEmUp/FighterAnimator.cs`)
`FighterAnimator` carrega uma tira por estado **por convenção** — `Sprites/<set>/<Estado>` (`set` = `Sofia` ou `PiaLoco`; nomes em `FighterSprites`) — via `Content.Load` com `try/catch (ContentLoadException)`. Se a tira não existir, desenha um **placeholder** legível; quando o PNG é adicionado e registrado no `.mgcb`, o sprite real entra **sem mudar gameplay**.
- **Formato da tira**: o `Animation` (`Game/Animation.cs`) assume **uma única tira horizontal de quadros quadrados** (`FrameCount = Largura/Altura`). Logo, `Largura = lado × nº de quadros`, `Altura = lado`. Use 64/96/128… desde que **quadrado, mesmo tamanho, sem espaços/rótulos**.
- **Escala e pés**: `FighterAnimator` desenha com `TargetRenderHeight` (altura na tela, desacopla a resolução da arte) e `FootAnchor` (fração do quadro onde ficam os pés, p/ não flutuar). Ajuste essas constantes em vez de reexportar a arte.
- **Montar quadros soltos → tira**: `tools/montage-sprites.ps1` (PowerShell + System.Drawing) concatena uma pasta de quadros numa tira. Ex.: `pwsh tools/montage-sprites.ps1 -InputDir <pasta_de_quadros> -Output Curitiba/Curitiba.Core/Content/Sprites/Sofia/Idle.png`. Depois registre a tira no `.mgcb` (mesmo template das outras texturas) e buildar.
- A arte-fonte em `Curitiba.Art/` (folhas de design 1536×1024 e quadros exportados) **não** está ligada ao build; só os PNGs colocados em `Content/Sprites/...` e registrados no `.mgcb` são compilados.

### Persistência de configurações e leaderboard
`SettingsManager<T>` (genérico, com backend JSON) encapsula um `ISettingsStorage`. Armazenamentos específicos por plataforma: `DesktopSettingsStorage`, `MobileSettingsStorage`, `ConsoleSettingsStorage` (escolhidos em `CuritibaGame` conforme a plataforma). `CuritibaSettings` guarda preferências do usuário (idioma, etc.); `CuritibaLeaderboard` guarda os recordes por fase. O `SettingsFileName` do armazenamento do leaderboard é trocado por fase (`NN.json`) conforme as fases carregam.

### Localização (`Localization/`)
As strings vêm de recursos RESX (`Resources.resx` padrão, mais `Resources.es-ES`, `Resources.fr-FR`). `LocalizationManager.GetSupportedCultures()` descobre os idiomas sondando as satellite assemblies; `SetCulture()` define a cultura da thread. O índice do idioma selecionado é armazenado nas configurações e aplicado em `CuritibaGame.Initialize()`. Referencie strings de UI pelos membros gerados `Resources.*`, nunca por literais embutidos.

## Convenções

- **Os namespaces são inconsistentes**: gameplay/configurações do núcleo usam `Curitiba.Core.*`, mas as telas usam `Curitiba.Screens` e o gerenciador de telas usa `Curitiba.ScreenManagers` (sem `.Core`). Acompanhe o namespace já usado pela pasta que você está editando em vez de assumir `Curitiba.Core.<Pasta>`.
- `Level` e `SettingsManager<T>` são `internal`; mantenha novos tipos de gameplay como `internal` a menos que um projeto cabeça realmente precise deles.
- O loop do jogo reutiliza structs `Vector2`/posição entre frames para evitar alocações por frame (veja `Level.UpdateGems`/`DrawTiles`) — siga esse padrão em caminhos quentes (hot paths).
