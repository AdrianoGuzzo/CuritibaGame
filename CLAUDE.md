# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Visão geral

Curitiba é construído sobre **MonoGame 3.8**, com alvo .NET 8. É um jogo multiplataforma: uma única biblioteca compartilhada `Curitiba.Core` move quatro projetos "cabeça" finos por plataforma (DesktopGL, WindowsDX, Android, iOS). Quase todo o código vive em `Curitiba.Core`; os projetos de plataforma contêm apenas um ponto de entrada e o empacotamento específico da plataforma.

O repositório nasceu como um **platformer 2D com rolagem lateral** (código em `Game/`, fases em `Content/Levels/NN.txt`). A entrega ativa, porém, é uma **demo de beat 'em up** ("Capão Raso": Sofia vs. inimigos "Pia Loco"), implementada em `BeatEmUp/` sobre a mesma infraestrutura (telas, input, resolução, pipeline). O **"Play"** do menu agora abre o beat 'em up; o platformer continua no código mas **dormante** (não acessível pelo menu). Ao mexer no jogo atual, trabalhe em `BeatEmUp/`; o material em `Game/` é referência/reuso, não o alvo.

> **Metodologia obrigatória**: todo trabalho de código neste repositório segue **TDD — RED → GREEN → REFACTOR**, com o teste escrito antes da implementação. Ver [Metodologia obrigatória: TDD](#metodologia-obrigatória-tdd).

O `README.md` na raiz é a porta de entrada do repositório (o que é o jogo, controles, como rodar, compilar, testar e gerar o relatório de cobertura). Este arquivo é o detalhamento.

## Comandos

Execute todos os comandos a partir do diretório `Curitiba/` (onde ficam o `.slnx` e as pastas de projeto). O arquivo de solução é `Curitiba.slnx` na raiz do repositório.

```bash
# Restaura as ferramentas do pipeline de conteúdo do MonoGame (necessário uma vez antes do primeiro build)
dotnet tool restore

# Há dois manifestos de ferramentas, com escopos diferentes e sem se enxergarem (ambos são isRoot):
#   Curitiba/.config/dotnet-tools.json  -> pipeline de conteúdo (mgcb, mgcb-editor)
#   .config/dotnet-tools.json (na raiz) -> ferramentas de desenvolvimento (ReportGenerator)
# Rodar `dotnet tool restore` na raiz do repo traz o segundo; dentro de Curitiba/, o primeiro.

# Compila tudo
dotnet build Curitiba.slnx

# Executa o build desktop (cabeça OpenGL multiplataforma — alvo usual de desenvolvimento)
dotnet run --project Curitiba.DesktopGL

# Cabeça DirectX apenas para Windows
dotnet run --project Curitiba.WindowsDX
```

Android e iOS exigem os workloads .NET correspondentes (`dotnet workload install android` / `ios`) e normalmente são compilados/implantados a partir de uma IDE.

### Testes

```bash
# Suite completa (Core + DesktopGL + Tests) — é exatamente o que o CI roda
dotnet test Curitiba.CI.slnx

# Loop rápido de TDD: só o projeto de testes, sem rebuildar as cabeças
dotnet test Curitiba/Curitiba.Tests/Curitiba.Tests.csproj

# Um comportamento específico (o RED de um ciclo)
dotnet test Curitiba/Curitiba.Tests/Curitiba.Tests.csproj --filter "FullyQualifiedName~HealthReachingZero"

# Só uma área
dotnet test Curitiba.CI.slnx --filter "FullyQualifiedName~BeatEmUp"

# Cobertura com relatório HTML navegável (o mesmo que o CI publica)
pwsh tools/coverage.ps1

# Cobertura só com os dados brutos, sem relatório
dotnet test Curitiba.CI.slnx --collect:"XPlat Code Coverage" --settings coverlet.runsettings
```

`Curitiba.Tests` (xUnit, `net8.0`) cobre combate, ondas, câmera, fase/JSON, input e configurações,
**sem GPU, janela ou pipeline de conteúdo**. Documentação completa — como adicionar teste, criar
fixture, ler falhas, limitações e questões em aberto — em `Curitiba/Curitiba.Tests/README.md`.

> **`dotnet test` sem argumento na raiz falha** (`MSB1011`: há duas soluções lá). Sempre nomeie
> `Curitiba.CI.slnx` ou o `.csproj` do projeto de testes.

> **`dotnet build Curitiba.slnx` falha neste ambiente** (as cabeças Android/iOS pedem
> `net8.0-android`/`net8.0-ios`, workloads fora de suporte — erro `NETSDK`, não erro de código).
> Use `Curitiba.CI.slnx`, que contém apenas Core + DesktopGL + Tests; é o que o CI roda.

CI em `.github/workflows/ci.yml`: restore, build Release e testes a cada push/PR para `master`.
Ele também gera o relatório de cobertura do ReportGenerator — HTML no artefato `coverage-html`,
tabela por classe no resumo do job e num comentário do PR reescrito a cada push — mas **não reprova
por cobertura**: o PR só é inválido por build quebrado ou teste vermelho.

Análise estática via `.editorconfig` na raiz (`AnalysisMode=None` + um punhado de regras que pegam
bug real); `TreatWarningsAsErrors` só no projeto de testes.

## Metodologia obrigatória: TDD

**RED → GREEN → REFACTOR. Nenhuma feature, regra de negócio, correção de bug ou alteração de
comportamento entra neste repositório sem um teste escrito _antes_ da implementação.** O teste faz
parte da entrega, não é acompanhamento posterior. `Curitiba.Tests` existe para isso e o CI reprova o
PR se o build ou a suíte falharem.

Regra de ouro: **se você ainda não viu o teste falhar pelo motivo certo, não comece a implementar.**

### 0. Antes de tocar no código — entender o requisito

Antes do primeiro teste, responda explicitamente:

- Qual é a **regra** — o comportamento observável que precisa passar a valer?
- Quais são as **entradas válidas e inválidas**? (dano negativo, `enemyCount: 0`, `corridor` nulo,
  JSON malformado, `top` abaixo de `bottom`, delta de tempo gigante depois de um freeze)
- Quais são os **cenários de erro e de borda**? (seção sem ondas, onda que nunca limpa, alvo morto
  no meio do combo, arquivo de settings corrompido, tira de sprite ausente)
- O que isso pode **regredir**? (`Fighter` é base da Sofia *e* do Pia Loco; `SpawnArea` é lida pela
  arena e pelo editor; mexer em `StageDefinition` atinge JSON, validator, Tiled, editor e hot-reload
  de uma vez só)
- Há **risco de segurança/robustez**? (§5 — aqui isso quer dizer dado externo hostil)
- Quais **dependências externas** entram? (`ContentManager`, `TitleContainer`, filesystem, `Random`,
  relógio, cultura da thread) — cada uma precisa de um substituto de `TestSupport/`.
- Portanto: **quais comportamentos precisam de teste?** Liste-os antes de escrever o primeiro.

Só então comece o ciclo — **um comportamento por vez**.

### 1. RED — o teste primeiro

1. Escreva (ou altere) o teste que descreve o comportamento esperado.
2. Rode-o:
   `dotnet test Curitiba/Curitiba.Tests/Curitiba.Tests.csproj --filter "FullyQualifiedName~<NomeDoTeste>"`.
3. **Confirme que ele falha pelo motivo certo**: asserção que não bate, ou o membro que ainda não
   existe. Falha de infraestrutura **não é RED** — `FileNotFoundException: MonoGame.Framework`,
   `CS0122`, `CS0051`, fixture não copiada, aviso de analyzer virando erro. A tabela *Como
   interpretar falhas* do README diz o que cada uma significa. Conserte o teste e volte ao passo 2.

Proibido: escrever a implementação antes de existir um teste falhando que a represente.

### 2. GREEN — o mínimo para passar

- Implemente **só** o necessário para o teste passar. Sem abstração antecipada, sem opção de
  configuração "que alguém vai querer", sem generalizar a partir de um caso único.
- Precisa de mais comportamento? Volte ao RED com **outro** teste — não amplie a implementação sem
  um teste novo apontando para ela.
- Se o "mínimo" for um número de balanceamento, ele vai para `capao-raso.json` / `FighterTuning`
  (ver [Modo Beat 'em up](#modo-beat-em-up-beatemup)) — nunca uma constante nova espalhada no código.
- Rode o teste; depois rode a área inteira.

### 3. REFACTOR — com a rede montada

Com tudo verde: remova duplicação, melhore nomes, reduza acoplamento, aumente coesão, simplifique
método longo. O comportamento validado pelos testes é preservado — **se um teste mudou de resultado,
não foi refatoração, foi mudança de comportamento**.

O movimento mais valioso aqui é **extrair a lógica pura da casca não testável**. Os dois exemplos
canônicos do repo:

- `ScreenManagers/PresentationLayout.cs` — a matemática de escala/letterbox tirada do
  `ScreenManager`: sem `GraphicsDevice`, sem janela.
- `DevTools/StageReloadPolicy.cs` — a máquina de estados do hot-reload tirada do `BeatEmUpScreen`:
  sem `FileSystemWatcher`, sem tela, sem conteúdo.

Quando um comportamento novo cair dentro de `Draw`, de uma `GameScreen` ou do `ImGuiDevEditor`,
**extraia a decisão** para um tipo assim e faça TDD nele; deixe na casca apenas a chamada.
Refatoração também não pode introduzir alocação por frame em hot path (ver *Convenções*).

Ao terminar: **rode novamente todos os testes relevantes.**

### 4. Níveis de teste

- **Unitário (o default).** Regra isolada, rápida e determinística: cálculo, validação,
  transformação, decisão condicional, tratamento de exceção, caso extremo. Sem GPU, sem arquivo,
  sem relógio, sem rede. Exemplos: `WaveManagerTests`, `ComboChainTests`, `CameraTests`,
  `StageValidatorTests`, `StageReloadPolicyTests`. Use os fakes de `TestSupport/`
  (`HeadlessContent`, `TestFighter`, `Enemies`, `SyntheticInput`, `RecordingEnemyFactory`,
  `InMemorySettingsStorage`) — nunca a dependência real.
- **Integração.** Quando o comportamento **é** a interação entre as peças, não force um unitário
  artificial mockando tudo. Não há banco, EF, Redis, fila nem API neste projeto: os "sistemas
  externos" reais são o **filesystem** e o **pipeline de conteúdo**. Exemplos legítimos:
  `ArenaTests` (arena → ondas → spawn → câmera → conclusão ao longo de frames), `AllStagesTests`
  (todos os `*.json` do jogo carregados por `StageLoader`/`TitleContainer`), `TiledImporterTests`
  (`.tmj` → seção), `SettingsTests` (grava e relê do disco em `TempDir`).
- **E2E.** O fluxo `Menu → Play → Arena → Fim da Demo` exige janela, GPU e conteúdo compilado.
  **Não existe infraestrutura E2E** e criá-la não é pré-requisito de um ciclo de TDD: o equivalente
  lógico está coberto em `ArenaTests`/`AllStagesTests`, e o fluxo real é validado pelo **checklist
  manual** do README antes de uma release. Se um dia houver E2E, reserve-o para esse fluxo crítico —
  não para regra interna.

### 5. Segurança e robustez de entrada

Este repositório não tem autenticação, autorização, multiusuário nem rede — os cenários clássicos
("usuário sem permissão", "acesso a recurso de outro usuário") não têm alvo aqui. O que **existe** é
uma fronteira de confiança real: **todo dado externo ao código** — JSON em `Content/Data/Stages`,
`.tmj` importado, arquivo de settings do jogador, campos do editor F1, arquivo salvo durante o
hot-reload. Trate-o como hostil e **cubra com teste**:

- arquivo ausente ou caminho inexistente → falha silenciosa, jogo de pé (`StageLoader`);
- JSON malformado → **nunca** derruba a arena em execução (`StageReloadPolicy`, `invalid-json.json`);
- campos nulos ou vazios (`corridor`, `sections: []`) e valores invertidos/fora de faixa
  (`inverted-corridor.json`) → problema reportado, não crash trinta frames depois;
- referência que não resolve (`spawnPoint` inexistente, personalidade desconhecida) →
  `StageValidator` sinaliza em vez de cair num default silencioso;
- dado que **prende o jogador** (onda que nunca limpa, lock de câmera inalcançável): o softlock é o
  "acesso indevido" deste projeto — a maior falha que um dado externo consegue causar aqui;
- settings corrompidas → defaults, não exceção na inicialização;
- exceção tratada onde o usuário não pode fazer nada, **sem vazar caminho absoluto** na tela.

**Não considere um caminho seguro só porque existe um `try/catch`** — o comportamento precisa estar
fixado por teste. Se um dia entrarem rede, perfis, telemetria ou compras, os itens clássicos
(autenticação, autorização, isolamento entre usuários, manipulação de parâmetro, exposição indevida
de informação) passam a valer literalmente e **também** vêm por TDD.

### 6. Correção de bug

**Bug → teste que reproduz → RED → correção → GREEN → REFACTOR → suíte de regressão.**

Nunca corrija primeiro. O teste fica no repositório para sempre, no arquivo da área correspondente,
com nome que descreve o **comportamento correto** (não "bug 42"). Se o bug só se manifesta em
`Draw`/janela/GPU, escreva o teste sobre a parte que **dá** para isolar (extraia-a, §3) e registre o
resíduo no checklist manual — não deixe o ciclo sem teste por conveniência.

As **questões em aberto** do README são comportamentos estranhos *já fixados pelos testes como estão
hoje*. Corrigir uma delas começa **alterando** aquele teste para o comportamento desejado (aí ele
fica RED) — é o único caso legítimo de mudar um teste que passava.

### 7. Testes de regressão

Depois do GREEN, rode a área tocada; antes de dar a tarefa por pronta, rode
`dotnet test Curitiba.CI.slnx` inteiro.

**Nunca altere um teste apenas para fazê-lo passar.** Um teste que quebrou faz uma pergunta: *o
comportamento antigo estava certo?*

- Sim → o defeito é da sua mudança. Conserte o código.
- Não → o comportamento mudou de propósito. Atualize o teste **e diga isso explicitamente** no
  commit/PR.

Afrouxar asserção, `Skip=`, comentar ou deletar teste sem essa justificativa é inaceitável.

Pontos de alto risco de regressão: `Fighter` (base de todos os combatentes), `StageDefinition` /
`StageLoader` (JSON + validator + Tiled + editor + hot-reload), `Camera2D` + `WaveManager` (o
travamento da câmera é o que faz a fase avançar) e `ScreenManager`/transform (quebra o input de toda
a UI de uma vez).

### 8. Qualidade dos testes

Determinístico, independente, legível, rápido, pequeno, focado em comportamento. Neste repo isso tem
tradução literal:

| Nunca | Use |
|---|---|
| `Thread.Sleep`, relógio real | `Frames.Step/Advance/AdvanceSeconds/AdvanceUntil` (passo fixo de 1/60 s) |
| `Random` de verdade (a IA do inimigo tem um) | `Enemies.AlwaysAttacks` / `Enemies.NeverAttacks` |
| teclado ou gamepad reais | `SyntheticInput.Held/Pressed/PressedWhileHolding` |
| `ContentManager` com conteúdo compilado | `HeadlessContent.Create()` |
| pasta do usuário, caminho fixo | `TempDir` |
| trocar a cultura da thread sem restaurar | `using var scope = new CultureScope();` |
| estado estático compartilhado (`BaseSettingsStorage.SpecialFolderPath`) | `[Collection(GlobalStateCollection.Name)]` |
| JSON grande embutido no `.cs` | fixture em `Fixtures/` + `Fixtures.Path_` |

Nenhum teste pode depender de ordem de execução, de outro teste, de máquina específica ou de
`FileSystemWatcher`. Precisou de um colaborador novo e não determinístico? **Adicione o fake em
`TestSupport/`** em vez de improvisar dentro do teste. `Curitiba.Tests` usa `TreatWarningsAsErrors`:
aviso de analyzer do xunit é para corrigir, não para silenciar.

### 9. AAA e nomes

Arrange → Act → Assert, **uma ação por teste**, no padrão que a suíte já usa:

```csharp
[Fact]
public void HealthReachingZero_ShouldDefeatTheFighter()
{
    // Arrange
    var fighter = new TestFighter(FighterTuning.PiaLocoDefaults());

    // Act
    fighter.TakeDamage(fighter.Health, Vector2.Zero);

    // Assert
    Assert.Equal(FighterState.Dead, fighter.State);
}
```

O nome descreve o comportamento — dá para entender sem abrir a implementação.
Bons: `TakeDamage_ShouldNeverDriveHealthBelowZero`, `Reset_ShouldStartAtTheFirstWave`,
`InvalidJson_ShouldKeepTheRunningArena`. Ruins: `Test1`, `TestMethod`, `TestAttack`, `ShouldWork`.
Cenários semelhantes → `[Theory]` + `[InlineData]`. **Tipo `internal` não pode aparecer na
assinatura pública de um método de teste** — passe o nome como `string` e converta dentro (README).

### 10. Cobertura

Cobertura é diagnóstico, não meta. O que vale é **cobertura comportamental**: regra de negócio,
caminho crítico, cenário de erro, validação, dado externo e área com histórico de regressão. O
alvo prático é **não baixar a branch coverage da área que você tocou** — e **não há gate**: o CI
publica o número, nunca reprova por ele.

- O relatório por classe é **gerado**, nunca escrito à mão: `pwsh tools/coverage.ps1` localmente
  (ReportGenerator fixado em `.config/dotnet-tools.json`, na raiz), e a cada run do CI no artefato
  `coverage-html` + resumo do job. Nenhuma tabela de percentual é mantida nos docs: uma tabela
  congelada diverge do relatório em silêncio, porque nada a verifica.

- `coverlet.runsettings` exclui de propósito o que não roda headless (`Screens/`, `DevTools/ImGui*`,
  `Effects/`, `CuritibaGame`, o platformer dormante em `Game/`). Não tente "cobrir" isso — extraia a
  lógica (§3).
- Stryker (`stryker-config.json`, fora do CI) mede se os testes **detectam** a mudança. Onde a regra
  é testada, os mutantes morrem (83–100%); o número baixo de `Fighter.cs` vem de mutantes sem
  cobertura em `Draw` e afins.
- Teste artificial só para subir percentual é ruído: será mantido para sempre e não protege nada.

### 11. Quando o TDD não alcança

Arte, `.mgcb`, tiras de sprite, constantes puramente visuais (`TargetRenderHeight`, `FootAnchor`),
layout do ImGui e qualquer coisa dentro de `Draw` não têm teste automatizado aqui. Nesses casos:
**extraia o que for decisão** para um tipo testável e faça TDD nele; o resíduo genuinamente visual
vai para o **checklist manual** do README (`dotnet run --project Curitiba/Curitiba.DesktopGL`), dito
explicitamente na entrega. Ajuste de valor de balanceamento em `capao-raso.json` não pede um teste
por número alterado, mas **mudança estrutural de fase** passa por `StageValidator` /
`AllStagesTests`.

O mesmo vale para a infraestrutura que não é código do jogo: o YAML de `.github/workflows/`, os
scripts de `tools/` e os manifestos de ferramentas não têm teste automatizado — não há regra de
negócio neles. A contrapartida é **manter a decisão fora do script**: o que conta como cobertura
vive em `coverlet.runsettings` e em lugar nenhum mais, e o CI só invoca ferramenta declarativa em
vez de recalcular o número à mão. Ao mexer nessa camada, a verificação é empírica e vai para o
checklist manual da entrega.

### 12. Definition of Done

Uma alteração só está concluída quando:

- [ ] o requisito foi compreendido e os cenários (válidos, inválidos, borda, erro) foram listados;
- [ ] os testes foram criados **antes** da implementação;
- [ ] cada teste foi visto **falhando pelo motivo esperado** (RED);
- [ ] a implementação é o mínimo que os torna verdes (GREEN);
- [ ] o código foi refatorado com os testes verdes, e a lógica nova não ficou presa em `Draw`/tela;
- [ ] robustez de entrada externa coberta quando aplicável (§5);
- [ ] bug corrigido tem teste de regressão permanente (§6);
- [ ] `dotnet test Curitiba.CI.slnx` passa inteiro;
- [ ] nenhum teste foi afrouxado, pulado ou removido sem justificativa explícita de mudança de
      comportamento;
- [ ] os testes são determinísticos, seguem AAA e têm nome que descreve comportamento;
- [ ] `CLAUDE.md` / `Curitiba.Tests/README.md` atualizados se arquitetura, infraestrutura de teste
      ou alguma "questão em aberto" mudou;
- [ ] o que não foi possível automatizar está no checklist manual e foi dito explicitamente.

## Pipeline de conteúdo

Os ativos (sprites, sons, fontes, planos de fundo) são compilados pelo pipeline de conteúdo do MonoGame, e **não** copiados crus. A fonte única de verdade é `Curitiba.Core/Content/Curitiba.mgcb`, referenciada por todos os projetos cabeça via `<MonoGameContentReference>`. O pacote `MonoGame.Content.Builder.Task` o compila automaticamente durante o build.

- Para alterar quais ativos são compilados, edite `Curitiba.mgcb` diretamente ou execute o editor: `dotnet mgcb-editor`.
- **Áudio**: `Music/*.mp3` entra por `Mp3Importer` + `SongProcessor` (`Quality=Best`). O `SongProcessor` **não** guarda o áudio dentro do `.xnb`: ele emite um `.xnb` de ~137 bytes (caminho + duração) e o áudio **ao lado**, transcodificado por plataforma (`.ogg` no DesktopGL, `.wma` no WindowsDX, `.m4a` no mobile). O `ffmpeg`/`ffprobe` usados nisso vêm dentro do próprio `dotnet-mgcb` — sem dependência externa. O `MonoGame.Content.Builder.Task` faz glob de `**\*.*` na saída do pipeline, então o arquivo companheiro é copiado sozinho e **nenhum `.csproj` de cabeça precisa de entrada nova**.
- **Efeitos sonoros**: `Sounds/*.wav` entra por `WavImporter` + `SoundEffectProcessor`. Ao contrário do `SongProcessor`, o PCM fica **dentro** do `.xnb` (um impacto de ~0,3 s dá ~60 KB) e não há arquivo companheiro. Os `.wav` shipados são passados por `tools/trim-sound-silence.ps1` antes de entrar em `Content/Sounds/` — ver [Áudio](#áudio-audio).
- **Alguns arquivos são exceção (texto puro, fora do pipeline)** — copiados crus para a saída e lidos em runtime via `TitleContainer.OpenStream`:
  - As fases do platformer dormante (`Content/Levels/NN.txt`).
  - Os **dados do beat 'em up** (`Content/Data/Stages/*.json`), copiados pelos `.csproj` cabeça via `<Content>`/`AndroidAsset`/`BundleResource` (ver [Modo Beat 'em up](#modo-beat-em-up-beatemup)). Os globs cobrem só `*.json`: o `.tmj` **não** vai para a saída — o `TiledImporter` o lê por caminho absoluto da árvore de fontes, no editor.

## Arquitetura

### Abstração de plataforma
`CuritibaGame` (`Curitiba.Core/CuritibaGame.cs`) é a subclasse de `Game` e o verdadeiro ponto de entrada; o `Program.cs` de cada cabeça apenas a instancia e chama `Run()`. As ramificações por plataforma são centralizadas em dois flags estáticos, `CuritibaGame.IsMobile` e `CuritibaGame.IsDesktop` (calculados a partir de `OperatingSystem.Is*()`). Eles escolhem a implementação de armazenamento de configurações, o comportamento de tela cheia/mouse e entrada por toque vs. teclado. Prefira esses flags em vez de reverificar o SO.

### Localizador de serviços (service locator)
Singletons compartilhados são registrados em `Game.Services` no construtor / `LoadContent` de `CuritibaGame` e recuperados em outros lugares com `Services.GetService<T>()`. Serviços registrados: `GraphicsDeviceManager`, `SettingsManager<CuritibaSettings>`, `IDevEditor`, `ParticleManager` e `IMusicPlayer`. Essa é a principal forma de os subsistemas obterem suas dependências — não há contêiner de DI.

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
- **Combatentes**: `Fighter` (base, máquina de estados `FighterState` = Idle/Walk/Dash/Attack/Attack2/Attack3/Jump/JumpAttack/Hit/Thrown/KnockedDown/Dead; stats aplicados por `ApplyTuning(FighterTuning)`) → `SofiaPlayer` (8 direções, **ataque no `J`**, pulo no `Space`, dash no `Shift`; gamepad A/X, B e shoulders; vida 100, dano 10) e `PiaLocoEnemy` (IA persegue+ataca, vida 30, dano 5; recebe um `EnemyProfile` resolvido de `personalities`).
- **Combos e reações**: o ataque não é um golpe único. `FighterTuning.ComboChain` define a sequência (a da Sofia é punch→punch→punch2→kick), resolvida por `CombatDefaults.BuildChain` em `ComboMove`s com `CancelPoint` (quando um press bufferizado cancela a recuperação) e `RequiresHitConfirm` (a corrente só avança se o golpe conectou). O finisher tem `Launches`, que joga o alvo em `FighterState.Thrown` — um corpo em voo derruba quem estiver no caminho ("boliche"). Golpes normais acumulam *poise*: `hitsToKnockdown` (vindo da onda, não do tuning) derruba no N-ésimo golpe seguido. `Position` é o **ponto dos pés** (centro-base); colisão por `HurtBox` (retângulo) vs. `AttackData` (hitbox temporária, ativa só nos frames do golpe, um acerto por alvo por golpe).
- **Câmera/ondas**: `Camera2D` segue a Sofia e **trava o avanço** via `MaxAdvanceX` até a área ser limpa. Cada onda (`SpawnArea`, mapeada de `WaveDef`) define o ponto de trava (`lockCameraX`) e os inimigos: `spawns[]` com posições/personalidades explícitas têm prioridade; sem eles, usa o spread procedural via `enemyCount`. Ao chegar ao fim da última seção → `Completed` → "Fim da Demo".
- **Validação (advisória)**: `BeatEmUp/Data/StageValidator` inspeciona um `StageDefinition` e devolve `StageIssue`s (Info/Warning/Error) para os problemas que quebram a arena de verdade — `corridor`/`backdrop` nulos, `sections` vazio, onda que nunca limpa, `spawnPoint` inexistente, lock de câmera inalcançável. **Nada no jogo o chama**: o carregamento continua exatamente como era. Ele existe para os testes (`AllStagesTests` valida todos os `*.json` do repo) e como base para o editor.
- **Pontuação (`BeatEmUp/Scoring/`)**: `ScoreSystem` é dono do score/combo/multiplicador e é **da arena** (`CapaoRasoArena.Score`), então arena nova = corrida nova. A fiação fica toda em `ResolveCombat`: `RegisterHit(attack.Type)` no acerto confirmado, `RegisterEnemyDefeated(EnemyType.Normal)` nas duas mortes (golpe e "boliche"), `RegisterPlayerDamage()` quando a Sofia é atingida; `score.Update(dt)` roda **depois** de `ResolveCombat` (antes, um combo que expira no frame morreria antes do golpe daquele frame poder salvá-lo) e `EndCombo()` é chamado em `Completed` e em `PlayerDefeated`. O combo pausa junto com o jogo de graça, porque a arena só tica pelo `HandleInput` da tela. **Todo número vive em `ScoreConfig`** (DTO `public sealed`, molde de `FighterTuning`), resolvido **uma vez** por `ScoreDefaults.Build` no `ScoreRules` imutável — mesmo padrão de `CombatDefaults.BuildChain`, e é o que impede uma edição do editor de mudar as regras no meio de um combo. Nas ladders, `null` = "não autorado, use o default" e vazio = "autorado como nenhum" (é assim que se desliga um bónus pelos dados). Há 7 eventos `Action<T>` (`OnScoreChanged`, `OnComboBroken`, …). O construtor headless da arena aceita um `ScoreConfig` opcional — costura para teste/tooling, não há bloco `scoring` no JSON ainda.
- **Peso do golpe para o score**: `ComboMoveDef.ScoreType` (`"normal"`/`"heavy"`/`"air"`/`"finisher"`, case-insensitive) é resolvido por `CombatDefaults` num `ComboMove.ScoreType` e viaja em `AttackData.Type` — a arena detecta o acerto mas não vê o `ComboMove`, que é privado do `Fighter`. Sem autorar, um golpe com `Launches` conta como finisher e o resto como normal; o ataque aéreo não tem `ComboMove` e é o único com o tipo fixo em código (`Air`). No `capao-raso.json` o `punch3` está marcado `"scoreType": "heavy"`.
- **HUD do score**: `Scoring/ScoreHud` decide os textos e o fade (funções puras, testadas); `DrawHud` só mede e posiciona. Score no canto superior direito com `Resources.Score`, e o combo logo abaixo com `Resources.Combo` (`"COMBO 12  x3"`, o `x` omitido em x1), aparecendo de `MinimumCombo` = 2 para cima e sumindo nos últimos `FadeSeconds` = 0,4 s da janela. **Substituiu o contador `Derrotados`** — `Resources.Defeated` ficou órfã.
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

### Áudio (`Audio/`)
Um canal de música e um canal de efeitos, cada um com a sua costura. `IMusicPlayer` (registrado em `Game.Services` por `CuritibaGame.LoadContent`) é a costura: `MusicPlayer` é a implementação real sobre `ContentManager` + `MediaPlayer`, e as **policies** são máquinas de estados puras — `MenuMusicPolicy` (`Idle → FadingIn → Playing → FadingOut → Stopped`) e `ArenaMusicPolicy` (a mesma, mais `Ducked`) — que decidem o que tocar e em que volume, sem nenhum tipo de `Microsoft.Xna.Framework.Media`. São elas que carregam o TDD desta área; as telas nunca escrevem `player.Volume` direto.
- **Faixa do menu**: `Music/SunlightOnTheShrubs` toca em loop desde a `MainMenuScreen`, com fade-in de 1 s. O **"Play"** chama `menuMusic.BeginExit()` e a faixa some ao longo dos 2,5 s de `CinematicDuration` — a policy recebe essa duração no construtor, então não há um segundo número para manter em sincronia. O `Update` da policy roda **antes** do bloco da cinemática justamente porque o frame em que o fade acaba é o frame que chama `LoadingScreen.Load` — a faixa do menu não vaza para a arena, que entra com a sua.
- **Faixa da arena**: `Music/RubberBassRiot` toca em loop desde a `BeatEmUpScreen`. É a **única** música da gameplay e fica deliberadamente **atrás da ação** — `ArenaMusicPolicy.BackgroundVolume` = **0,30**, nunca volume cheio; nenhum estado da policy pode ultrapassar esse teto, e há teste fixando isso. Com o menu de pausa aberto ela **abaixa** para `DuckedVolume` = 0,12 (sem reiniciar a faixa) e volta ao despausar: o sinal é o `coveredByOtherScreen` do `Update`, o mesmo que alimenta o `pauseAlpha`. `arena.Completed` e `arena.PlayerDefeated` chamam `BeginExit()` antes do `LoadingScreen.Load`, e o fade corre durante os `TransitionOffTime` da tela — as duas durações do construtor da policy **são** as transições da tela, então não há número duplicado. `UnloadContent` chama `Stop` como rede de segurança para as saídas sem fade (Sair pelo menu de pausa).
- **`Song` e não `SoundEffect`**: um `SoundEffect` de 58 s assaria ~10 MB de PCM dentro do `.xnb` e na RAM; o `Song` faz streaming de ~1,3 MB. Em mobile, `MediaPlayer` ainda respeita a convenção do SO (`MediaPlayer.GameHasControl`).
- **Música ausente é condição normal**, igual a tira de sprite ausente: `MusicPlayer.Play` engole `ContentLoadException`/`NoAudioHardwareException` e o jogo roda mudo. É isso que também o mantém utilizável nos testes headless.
- O `Song` é carregado do `ContentManager` **raiz do `Game`**, nunca do `ContentManager` privado de uma tela — assim o `content.Unload()` de `MainMenuScreen.UnloadContent` não consegue descartar uma faixa tocando, e voltar do "Fim da Demo" acerta o cache.
- **O cache do `MusicPlayer` é por nome de asset** (`MusicPlayer.NeedsReload`): há um único player para todas as telas, então guardar só o `Song` faria a segunda faixa pedida retocar a primeira — a arena tocaria o tema do menu. Um load que falha esquece a faixa anterior, para uma faixa ausente virar silêncio e não a música errada. A regra é pura porque o load em si exige dispositivo de áudio e não é alcançável headless.
- **Efeitos sonoros (`ISoundPlayer` / `SoundPlayer`)**: canal de one-shots, irmão do de música e registrado do mesmo jeito em `CuritibaGame.LoadContent`, sobre o `ContentManager` **raiz do `Game`** (mesma razão: nenhuma tela pode dar `Unload` num buffer que o mixer ainda lê). É deliberadamente fire-and-forget — não há handle para parar ou fazer fade de um impacto; o que precisa ser conduzido ao longo do tempo é música e mora na outra interface. Efeito ausente é condição normal, igual a música ausente, **mas a falha é memorizada** (tombstone `null` no cache por nome): uma faixa é pedida uma vez por tela, um impacto é pedido várias vezes por segundo, e repetir um load que não pode dar certo martelaria o disco em pleno combate. `InstancePlayLimitException` também é condição normal numa briga cheia — o golpe que não achou voz livre simplesmente não soa.
- **Quem decide o som do golpe é `Audio/CombatSounds`**, regra pura e sem estado. Um golpe tem **dois momentos audíveis** e eles não são o mesmo som. O *swing* sai quando o golpe **abre**, antes de acertar qualquer coisa: `SwingSoundFor(AttackType)` devolve `Sounds/KickSwing` para `Finisher` e `null` para todo o resto — um whoosh a cada jab dispararia várias vezes por segundo e viraria ruído; só o golpe que fecha a corrente se anuncia. O *impacto* sai no contato: `HasImpactSound(AttackType)` é `true` para `Normal`/`Heavy`/`Finisher` e `false` para `Air` — carne é carne, e o chute empresta o banco de socos porque a identidade dele já veio do swing; o ataque aéreo continua esperando som próprio. Ambas leem `AttackType` porque é a única identidade que um golpe carrega para fora do `Fighter` (o `ComboMove` é privado). Todo número e nome de asset vive lá, como em `ScoreConfig`/`FighterTuning`: `PunchHitVolume` = 0,7 e `KickSwingVolume` = 0,6, os dois acima da cama de 0,30 da música e o swing **abaixo** do impacto que ele anuncia (a antecipação não pode roubar o pagamento), com testes fixando as duas relações.
- **O soco não tem *um* som, tem um banco de cinco** (`CombatSounds.PunchHits`), gasto em ordem por `Audio/SoundRotation` (genérico, também gira o banco de gemidos da multidão). Um único sample disparado a cada golpe é audivelmente um loop, e um beat 'em up não é outra coisa senão golpes. É **ciclo e não sorteio** por dois motivos: é determinístico, então a regra é testável sem arrastar um `Random` para dentro da arena; e espalha o banco por completo, enquanto sortear mais cedo ou mais tarde toca o mesmo impacto duas vezes seguidas — exatamente o que se percebe como "repetitivo". A rotação é **da arena**, então fase nova recomeça o ciclo. Há teste de arena fixando que três socos seguidos soam três variações diferentes: a rotação funcionar não garante que a arena a use.
- **Quem dispara o impacto é a arena**, em `CapaoRasoArena.ResolveCombat`, ao lado do `score.RegisterHit` — é o único ponto que enxerga um acerto confirmado. O `Play` fica no bloco `if (closest != null)`, que roda **uma vez por frame** e não uma vez por alvo: um golpe que pega dois inimigos é *um* impacto, e duas cópias do mesmo one-shot no mesmo frame só somam amplitude e viram um clique. O canal entra pelo **construtor** da arena (`null` = roda mudo), não por evento — a arena é recriada a cada hot-reload e a derrota cria uma `BeatEmUpScreen` nova, então uma assinatura teria que ser religada e desligada em cada uma; `BeatEmUpScreen.BuildArena()` é o único lugar que constrói uma arena, justamente para o som não sumir depois do primeiro reload.
- **O swing vem do `Fighter`, não da arena**: o som do chute começa com a perna, não com o contato, e nesse instante não há acerto nenhum para a arena observar. `Fighter.OnSwingStarted` (`Action<AttackType>`) é disparado dentro de `BeginMove` — o único ponto por onde um golpe abre, tanto o primeiro da corrente quanto o que sai **cancelando** a recuperação do anterior, que é justamente como o chute aparece no jogo. É evento e não flag polida por frame porque a arena **constrói** os seus combatentes e vive mais que eles: há exatamente um lugar para assinar (`sofia.OnSwingStarted += PlaySwingSound`, no construtor), nada para desassinar, e um golpe que abre no meio do frame soa nesse frame. A ressalva contra evento do item anterior vale para o **canal** (`ISoundPlayer`), que vem de fora e muda de vida a cada rebuild — não para um sinal entre a arena e a própria Sofia dela. **A multidão fica de fora de propósito**: o canal de combate segue os golpes da Sofia, não todo swing da fase (há teste com inimigo autorado com chute fixando isso).
- **A multidão tem voz própria, e é outro canal de sentido: `Audio/ZombieAmbience`.** O de combate é *feedback* — soa porque a Sofia fez algo e precisa **ler por cima** da música. A ambiência soa porque os Piá Locos simplesmente **estão lá**, e precisa ficar **por baixo de tudo**: `MoanVolume` = **0,18** é o primeiro som do jogo **abaixo** da cama de 0,30 de `ArenaMusicPolicy.BackgroundVolume`. Por isso é classe separada e não mais constantes em `CombatSounds` — juntar poria um número que ninguém pode subir ao lado de um que ninguém pode descer. Mesmo padrão dos outros: todo número e nome de asset mora lá, com testes fixando que o gemido está sob a música e sob qualquer golpe.
- **O gemido é da multidão, não do inimigo.** Num canal de uma voz e **sem pan**, um gemido do zumbi da esquerda é audivelmente idêntico ao do da direita — instanciar por inimigo custaria um parâmetro novo no ctor do `PiaLocoEnemy`, um `ISoundPlayer` descendo por `EnemyFactory`/`SpawnManager` e timers empilhando gemidos, tudo sem diferença nenhuma no alto-falante. O que o ouvido percebe como "cada zumbi geme" é **frequência proporcional a quantos são**: `ZombieAmbience.WindowFor(vivos)` encurta a janela linearmente de 5–8 s com um vivo para 2–3,5 s a partir de `FullCrowd` = 5, e aí **para de encurtar** — sem esse piso uma onda autorada grande gemeria sem parar. **Calibre pelo meio da curva, não pelas pontas**: as ondas do `capao-raso.json` têm 2 a 4 inimigos, então o que se ouve de verdade é a faixa de 2,75–6,88 s (uma onda de três fica em 3,5–5,75 s). A primeira versão foi afinada pelas pontas e entregou 10–17 s numa onda de três — sparse demais, e foi exatamente a reclamação. Estas janelas são **mais curtas que os samples** (~3,5 a 4,3 s), então corredor cheio sobrepõe gemidos: é intencional nesta densidade, vozes sobrepostas leem como mais zumbis. Se um dia ler como lama, o botão é o **comprimento dos samples**, não o piso.
- **`Audio/ZombieMoanScheduler` decide o *instante*, `SoundRotation` decide a *variante*** — e essa divisão é o desenho todo. O instante é **sorteado**, para a multidão não soar como metrônomo; a variante é **ciclo**, para uma sequência gastar o banco inteiro e nunca repetir em seguida. Sortear os dois mais cedo ou mais tarde toca o mesmo gemido duas vezes seguidas, que é exatamente o que se percebe como repetitivo. O `Random` entra pelo **construtor** (o único ponto não determinístico do tipo), então o unitário fixa o seed; todo o resto — piso, teto, rearme, corredor vazio — é aritmética que vale para qualquer seed, e é assim que os testes de arena asseguram sem controlar seed nenhum. O agendador **arma preguiçosamente**, no primeiro frame em que há alguém no corredor — mas pela **janela de abertura** (`OpeningMoanMin/Max` = 0,5–1,5 s) e não pela janela regular: **toda onda se anuncia**. Onda que o jogador enxerga é onda que o jogador tem de ouvir; vários segundos de silêncio sobre zumbis visíveis leem como som quebrado, não como contenção — e é a outra metade da mesma reclamação que encurtou as janelas. Não é instantâneo de propósito: gemido no frame exato em que o primeiro corpo aparece soa disparado pelo spawn, não pelos zumbis. Corredor vazio **não corre relógio** (só desarma), senão a onda seguinte entraria com um gemido já vencido; como o rearme é pela janela de abertura, ela também não herda o intervalo longo da onda anterior.
- **Quem dispara o gemido é a arena**, em `CapaoRasoArena.Update`, **depois** da varredura de corpos expirados — a multidão contra a qual a janela é dimensionada é a que está de pé no fim do frame, não uma que ainda incluía um corpo já removido. `CountStandingEnemies()` é laço e não LINQ (roda todo frame; hot path não aloca). Mudo quando `Completed` ou `PlayerDefeated`: gemido por cima do fade de derrota lê como som esquecido ligado. A pausa sai de graça, como o combo — a arena só tica pelo `HandleInput` da tela.
- **A fonte do gemido precisou de normalização, e isso é regra e não acaso.** O `.wav` original tinha pico de **0,14** de fundo de escala contra **1,00** dos socos shipados (~17 dB mais baixo): a 0,18 de volume ele sairia em ~0,025 efetivos, inaudível sob a música, e a feature "não funcionaria" sem erro nenhum. **Meça o pico de todo `.wav` novo antes de confiar na constante de volume** — uma constante de volume só significa o que diz se os samples do banco estiverem no mesmo nível. As três variantes saem de um arquivo só, por `ffmpeg` (mono, ganho de pico, pitch ×0,90/×1,00/×1,10, 44,1 kHz) e depois pelo trim de silêncio; mono de propósito, porque não há pan e estéreo 48 kHz custaria o dobro em `.xnb` (o banco de gemidos já é ~1,0 MB contra 0,35 MB do de combate).
- **`SoundEffect` aqui, `SongProcessor` lá**: cada impacto tem ~0,3 s e ~60 KB (0,29 MB o banco inteiro), então o PCM dentro do `.xnb` é o certo (`WavImporter` + `SoundEffectProcessor` no `.mgcb`, em `Content/Sounds/`). A ressalva do item **`Song` e não `SoundEffect`** vale para faixa de ~1 min, não para um golpe.
- **O `.wav` de `Content/Sounds/` não é o `.wav` de `Curitiba.Art/`**: packs de som costumam entregar o impacto com uma fração de segundo de silêncio na frente (os cinco socos vinham com 0,19 a 0,35 s). Num sampler isso é inofensivo; aqui é fatal, porque o efeito dispara no frame em que o golpe conecta — 0,25 s de silêncio são **15 frames** entre o soco na tela e o som, e lê como áudio quebrado, não como som atrasado. `tools/trim-sound-silence.ps1` corta o silêncio (com alguns ms de pre-roll, porque o transiente **é** o soco) e fecha a cauda com um fade curto para o corte não estalar. Ao adicionar um som novo, passe por ele: `pwsh tools/trim-sound-silence.ps1 -InputPath <fonte>.wav -Output Curitiba/Curitiba.Core/Content/Sounds/<Nome>.wav`. O script só lê **RIFF/WAVE 16-bit PCM**; fonte em outro formato (o swing do chute veio `.ogg`) converte antes com o `ffmpeg` que já vem dentro do `dotnet-mgcb` — `~/.nuget/packages/dotnet-mgcb/<versão>/tools/net8.0/any/windows-x64/ffmpeg.exe -i <fonte>.ogg -acodec pcm_s16le -ar 44100 <saída>.wav` — sem dependência externa.

### Persistência de configurações e leaderboard
`SettingsManager<T>` (genérico, com backend JSON) encapsula um `ISettingsStorage`. Armazenamentos específicos por plataforma: `DesktopSettingsStorage`, `MobileSettingsStorage`, `ConsoleSettingsStorage` (escolhidos em `CuritibaGame` conforme a plataforma). `CuritibaSettings` guarda preferências do usuário (idioma, tela cheia, efeito de partícula; `Language` tem default **2**, que indexa a lista de culturas de `LocalizationManager`). O `SettingsFileName` é trocável em runtime, para um armazenamento por fase. **Não existe `CuritibaLeaderboard`** no código — o tipo é citado em versões antigas desta documentação, mas nunca foi implementado.

### Localização (`Localization/`)
As strings vêm de recursos RESX (`Resources.resx` padrão, mais `Resources.es-ES`, `Resources.fr-FR`). `LocalizationManager.GetSupportedCultures()` descobre os idiomas sondando as satellite assemblies; `SetCulture()` define a cultura da thread. O índice do idioma selecionado é armazenado nas configurações e aplicado em `CuritibaGame.Initialize()`. Referencie strings de UI pelos membros gerados `Resources.*`, nunca por literais embutidos.

## Convenções

- **Os namespaces são inconsistentes**: gameplay/configurações do núcleo usam `Curitiba.Core.*`, mas as telas usam `Curitiba.Screens` e o gerenciador de telas usa `Curitiba.ScreenManagers` (sem `.Core`). Acompanhe o namespace já usado pela pasta que você está editando em vez de assumir `Curitiba.Core.<Pasta>`.
- `Level` e `SettingsManager<T>` são `internal`; mantenha novos tipos de gameplay como `internal` a menos que um projeto cabeça realmente precise deles.
- O loop do jogo reutiliza structs `Vector2`/posição entre frames para evitar alocações por frame (veja `Level.UpdateGems`/`DrawTiles`) — siga esse padrão em caminhos quentes (hot paths).
