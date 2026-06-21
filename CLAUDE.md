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
- **Os arquivos de fase (`Content/Levels/NN.txt`) são uma exceção** — são texto puro copiado para a saída, carregado em tempo de execução via `TitleContainer.OpenStream`, e não compilado pelo pipeline.

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
Modo isolado, sem física de gravidade nem tiles. Núcleo: `CapaoRasoArena` (análogo a `Level`) — dona de `SofiaPlayer`, `List<PiaLocoEnemy>`, `Camera2D` e da fila de `SpawnArea`. `BeatEmUpScreen` (análoga a `GameplayScreen`) cria a arena e roteia o `InputState` a ela em `HandleInput`.
- **Combatentes**: `Fighter` (base, máquina de estados `FighterState` = Idle/Walk/Attack/Hit/KnockedDown/Dead) → `SofiaPlayer` (8 direções, ataque no `Space`/A/X, vida 100, dano 10) e `PiaLocoEnemy` (IA persegue+ataca, vida 30, dano 5). `Position` é o **ponto dos pés** (centro-base); colisão por `HurtBox` (retângulo) vs. `AttackData` (hitbox temporária, ativa só nos frames do golpe, um acerto por alvo por golpe).
- **Câmera/ondas**: `Camera2D` segue a Sofia e **trava o avanço** via `MaxAdvanceX` até a área ser limpa; as `SpawnArea` definem o ponto de trava e a quantidade de inimigos (ondas 2 → 3 → 4). Mundo ~4 telas; ao chegar ao fim → `Completed` → "Fim da Demo".
- **Desenho**: fundo em espaço de tela (`GlobalTransformation`); mundo com `camera.GetTransform() * GlobalTransformation`; combatentes **ordenados por `Position.Y`** (mais baixo desenha por cima); HUD (nome da fase, barra de vida da Sofia, derrotados) em espaço de tela. Siga a convenção de hot-path (sem alocar por frame: `drawOrder` e o `Comparison` são reaproveitados).

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
