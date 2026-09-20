# Curitiba.Tests

Suíte de testes automatizados do Curitiba. O foco é o modo **beat 'em up** (`BeatEmUp/`): combate,
ondas, câmera, dados de fase, input e configurações.

O princípio que organiza tudo: **testes rápidos e determinísticos, sem GPU, sem janela e sem
pipeline de conteúdo**. Nenhum teste dorme, usa relógio real, teclado real ou `FileSystemWatcher`.

---

## Como executar

```bash
# Tudo (Core + DesktopGL + testes)
dotnet test Curitiba.CI.slnx

# Só os testes, mais rápido
dotnet test Curitiba/Curitiba.Tests/Curitiba.Tests.csproj

# Build de tudo que compila neste ambiente
dotnet build Curitiba.CI.slnx --configuration Release
```

> **`dotnet test` sem argumento não funciona a partir da raiz**: existem duas soluções lá
> (`Curitiba.slnx` e `Curitiba.CI.slnx`) e o MSBuild pede que você escolha (`MSB1011`). Use
> `dotnet test Curitiba.CI.slnx`, ou rode `dotnet test` de dentro de `Curitiba/Curitiba.Tests/`.

> **Por que `Curitiba.CI.slnx` e não `Curitiba.slnx`?**
> `Curitiba.slnx` inclui as cabeças `Curitiba.Android` e `Curitiba.iOS`, que usam
> `net8.0-android` / `net8.0-ios` — workloads fora de suporte no SDK instalado. Elas falham por
> motivo alheio a qualquer mudança de código (erro `NETSDK`), então o CI e o dia a dia usam
> `Curitiba.CI.slnx`, que contém apenas **Core + DesktopGL + Tests**. Os projetos mobile continuam
> na solução principal e são compilados à parte, a partir de uma IDE com os workloads corretos.

### Por categoria

```bash
# Todo o beat 'em up
dotnet test Curitiba.CI.slnx --filter "FullyQualifiedName~BeatEmUp"

# Só combate
dotnet test Curitiba.CI.slnx --filter "FullyQualifiedName~CombatTests"

# Máquina de estados dos combatentes
dotnet test Curitiba.CI.slnx --filter "FullyQualifiedName~FighterStateMachineTests"

# Fase / JSON / validação
dotnet test Curitiba.CI.slnx --filter "FullyQualifiedName~Data"

# Música do menu
dotnet test Curitiba.CI.slnx --filter "FullyQualifiedName~Curitiba.Tests.Audio"

# Ondas e spawn
dotnet test Curitiba.CI.slnx --filter "FullyQualifiedName~Wave|FullyQualifiedName~Spawn"

# Câmera
dotnet test Curitiba.CI.slnx --filter "FullyQualifiedName~CameraTests"

# Um único teste
dotnet test Curitiba.CI.slnx --filter "FullyQualifiedName~Fighter_ShouldBeKnockedDown"
```

---

## Cobertura

```bash
# Suíte + relatório HTML navegável (o mesmo que o CI publica)
pwsh tools/coverage.ps1

# Só os dados brutos, sem relatório
dotnet test Curitiba.CI.slnx --collect:"XPlat Code Coverage" --settings coverlet.runsettings
```

`tools/coverage.ps1` limpa os resultados antigos, roda a suíte com `coverlet.runsettings`, gera o
relatório do [ReportGenerator](https://github.com/danielpalme/ReportGenerator) em
`artifacts/coverage/` e abre o `index.html`. A ferramenta é **local**, fixada em
`.config/dotnet-tools.json` na raiz do repo, então CI e máquina de dev rodam a mesma versão. Use
`-NoBrowser` em ambiente headless e `-ReportOnly` para reprocessar dados já coletados.

> A limpeza não é higiene, é correção: `--results-directory` cria uma subpasta GUID **por execução**
> e o ReportGenerator **soma** todas as que encontrar — sem limpar, o relatório mistura o run de
> agora com os de ontem. Se você rodar o `dotnet test` cru acima, os dados vão para
> `Curitiba/Curitiba.Tests/TestResults/<guid>/` e o mesmo vale lá.

No CI o relatório sai em todo push e PR: HTML no artifact **`coverage-html`**, tabela por classe no
resumo do job e num comentário do PR reescrito a cada push. **Não há gate** — o CI só reprova por
build quebrado ou teste vermelho.

`coverlet.runsettings` (na raiz do repo) exclui de propósito o que não dá para exercitar sem
GPU/janela (`Screens/`, `DevTools/ImGui*`, `Effects/`, `CuritibaGame`, o platformer dormante em
`Game/`). Se alguma dessas classes **aparecer** no relatório, a exclusão parou de valer — isso é bug
de configuração, não número novo.

**O número que importa é branch coverage nas regras críticas**, não o total, e a leitura correta é o
relatório do run mais recente — nenhuma tabela de percentual é mantida à mão neste arquivo, porque
uma tabela congelada diverge do relatório em silêncio. O retrato estável: as classes puras — câmera,
ondas, slots de ataque, spawn, combo/buffer, tuning, score, fase/JSON, settings, políticas de música
— ficam no topo, em linha e em branch; o que fica para trás é `Fighter`, `CapaoRasoArena`,
`MusicPlayer` e `FighterAnimator`, e o que falta neles é essencialmente `Draw`/HUD — ver
*Limitações*.

Aparecem baixos no relatório, e é esperado: `ScreenManagers/ScreenManager`, `Inputs/InputState`,
`Inputs/TouchControls` e os armazenamentos de settings por plataforma. Nada disso roda headless e
nada disso está nas exclusões — o caminho para eles é extrair a decisão para um tipo testável
(`ScreenManagers/PresentationLayout`, que está em 100%, é o exemplo canônico), não acrescentar uma
linha ao `coverlet.runsettings`.

---

## Organização

```
Curitiba.Tests/
├── BeatEmUp/
│   ├── FighterStateMachineTests.cs   dano, poise, knockdown, morte, thrown
│   ├── CombatTests.cs                janela ativa da hitbox, geometria, um acerto por alvo
│   ├── ComboChainTests.cs            combo chain, cancel point, hit confirm, buffer
│   ├── SofiaPlayerTests.cs           stats, bindings, movimento em 8 direções, dash/pulo
│   ├── PiaLocoEnemyTests.cs          IA, alcance, tokens de ataque, personalidades
│   ├── WaveManagerTests.cs           sequência de ondas, delay, avanço
│   ├── SpawnManagerTests.cs          spawns[] vs enemyCount, posições, spawn points
│   ├── AttackSlotManagerTests.cs     ring de slots e limite de atacantes
│   ├── CameraTests.cs                follow, clamp, lock/release
│   ├── ArenaTests.cs                 integração: ondas → seções → Completed
│   ├── ScoreSystemTests.cs           combo, janela, multiplicador, bónus, EndCombo, Reset
│   ├── ScoreEventsTests.cs           quais eventos disparam, o que carregam, em que ordem
│   ├── ScoreConfigTests.cs           defaults do balanceamento e config hostil
│   └── ScoreHudTests.cs              textos e fade do HUD de score (decisão fora do Draw)
├── Data/
│   ├── StageLoaderTests.cs           JSON válido/inválido/ausente, round-trip
│   ├── StageDefinitionTests.cs       defaults de cada POCO, desserialização
│   ├── StageValidatorTests.cs        uma regra de validação por teste
│   ├── TiledImporterTests.cs         import .tmj → seção
│   └── AllStagesTests.cs             integração: todos os *.json do jogo
├── DevTools/
│   └── StageReloadPolicyTests.cs     hot-reload: JSON inválido mantém a arena
├── Input/
│   └── PresentationTests.cs          letterbox e transformação virtual ↔ tela
├── Settings/
│   └── SettingsTests.cs              salvar, carregar, defaults, arquivo corrompido
├── Localization/
│   └── LocalizationTests.cs          culturas, troca, fallback
├── Animation/
│   └── FighterSpritesTests.cs        mapeamento estado → tira de sprite
├── Audio/
│   ├── MenuMusicPolicyTests.cs       fade-in, fade-out da cinemática, parada, reentrada
│   ├── ArenaMusicPolicyTests.cs      teto de volume, duck na pausa, fade-out da saída, reinício
│   ├── MusicPlayerTests.cs           faixa ausente/sem nome, clamp de volume, troca de faixa
│   ├── CombatSoundsTests.cs         que golpe soa e quando (swing do chute, impacto), banco e volumes
│   ├── SoundRotationTests.cs        ciclo de um banco de variantes, wrap, nunca repete em seguida
│   ├── ZombieAmbienceTests.cs       banco de gemidos, volume sob a música, janela por multidão
│   ├── ZombieMoanSchedulerTests.cs  quando a multidão geme: janela, corredor vazio, gasto do banco
│   └── SoundPlayerTests.cs          efeito ausente/sem nome, load que falhou não é repetido
├── Fixtures/                         JSONs de cenário para casos de sucesso e falha
└── TestSupport/                      infraestrutura compartilhada
```

---

## Como adicionar um teste

1. Coloque-o no arquivo da área correspondente (crie um novo só se for uma área nova).
2. Use **Arrange / Act / Assert** e um nome que descreva **comportamento**:

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

Padrões de nome: `Should…`, `When…`, `Given…`. Evite `TestAttack`, `Test1`, `TestMethod`.

3. Vários cenários parecidos → `[Theory]` + `[InlineData]`:

```csharp
[Theory]
[InlineData(2)]
[InlineData(3)]
public void Fighter_ShouldBeKnockedDown_OnTheNthBlowInARow(int hitsToKnockdown) { ... }
```

4. **Tipos `internal` não podem aparecer na assinatura de um método de teste público.**
   `FighterState`, `EnemyPersonality` e companhia são `internal`, então passe o **nome** em
   `[InlineData]` e converta dentro do teste:

```csharp
[InlineData("Aggressive", 0.92f)]
public void BuiltInProfile_ShouldMatchItsArchetype(string personality, float chance)
{
    EnemyProfile profile = EnemyProfile.From(
        (EnemyPersonality)Enum.Parse(typeof(EnemyPersonality), personality));
    ...
}
```

### Infraestrutura disponível (`TestSupport/`)

| Helper | Para quê |
|---|---|
| `HeadlessContent.Create()` | `ContentManager` que nunca carrega nada — combatentes sem GPU |
| `Frames.Step()` / `Advance` / `AdvanceSeconds` / `AdvanceUntil` | avançar o loop com passo fixo de 1/60s |
| `TestFighter` | `Fighter` mínimo, com os movimentos `protected` expostos |
| `Enemies.Create(...)` | monta um `PiaLocoEnemy` sem repetir os 6 colaboradores |
| `Enemies.AlwaysAttacks` / `NeverAttacks` | neutraliza o único `Random` da IA |
| `SyntheticInput.Held/Pressed/PressedWhileHolding` | `InputState` sintético, sem teclado |
| `RecordingEnemyFactory` | `IEnemyFactory` que grava os pedidos em vez de criar inimigos |
| `RecordingMusicPlayer` | `IMusicPlayer` que grava volume/play/stop em vez de tocar — `Operations` guarda a **ordem** sem formatar float |
| `RecordingSoundPlayer` | `ISoundPlayer` que grava o que foi disparado em vez de tocar — `Assets`/`PlayCount` provam *quantas* vezes um impacto soou |
| `CountingContentManager` | `ContentManager` que conta os `Load` e falha — é o que revela um load repetido, que o resultado audível esconderia |
| `ScoreRecorder` | assina os 7 eventos do `ScoreSystem` e grava o que passou — `Events` guarda a **ordem** dos disparos |
| `InMemorySettingsStorage` | `ISettingsStorage` em memória, com modos de falha |
| `TempDir` | pasta temporária por teste, com limpeza |
| `CultureScope` | salva/restaura a cultura da thread |
| `GlobalStateCollection` | `[Collection]` que serializa testes com estado global |
| `Fixtures.Path_/Read` | localiza os JSONs de fixture |

---

## Como criar uma fixture

JSONs grandes ficam em arquivo, nunca embutidos no código:

1. Crie `Fixtures/meu-caso.json`. O `.csproj` já copia `Fixtures/**` para a saída.
2. Use `Fixtures.Path_("meu-caso.json")`:

```csharp
StageLoader.TryLoadFile(Fixtures.Path_("meu-caso.json"), out StageDefinition def);
```

Fixtures existentes:

| Arquivo | Representa |
|---|---|
| `valid-stage.json` | fase completa e jogável (caminho feliz) |
| `minimal-stage.json` | só o obrigatório — testa os defaults |
| `invalid-json.json` | sintaxe quebrada; o loader tem de falhar em silêncio |
| `null-corridor.json` | `"corridor": null` → NRE na arena |
| `empty-sections.json` | `sections: []` → IndexOutOfRange na arena |
| `invalid-wave.json` | onda que nunca limpa → trava a fase |
| `invalid-spawn.json` | `spawnPoint` inexistente na seção |
| `unknown-personality.json` | personalidade com caixa errada (o parse é case-sensitive) |
| `inverted-corridor.json` | `top` abaixo de `bottom` |

---

## Como interpretar falhas

| Sintoma | Causa provável |
|---|---|
| `FileNotFoundException: MonoGame.Framework` | falta o `PackageReference` do MonoGame no projeto de teste — `Curitiba.Core` marca o pacote como `PrivateAssets=All`, então ele **não** flui por `ProjectReference` |
| `error CS0051: Inconsistent accessibility` | um tipo `internal` apareceu na assinatura de um método de teste público — passe o nome como `string` |
| `error CS0122` num tipo do jogo | falta `<InternalsVisibleTo Include="Curitiba.Tests" />` em `Curitiba.Core.csproj` |
| `error xUnit2030` e afins | o analyzer do xunit; o projeto de teste usa `TreatWarningsAsErrors`, então é para corrigir, não ignorar |
| Teste de cultura instável | faltou `using var scope = new CultureScope();` — `SetCulture` escreve na thread e não desfaz |
| Teste de settings instável | `BaseSettingsStorage.SpecialFolderPath` é `protected static` e é compartilhado: use a `[Collection(GlobalStateCollection.Name)]` |
| Teste de timing falha por um frame | `Frames.FramesFor(0.5f)` arredonda **para cima**: 30 frames a 1/60s dão exatamente 0,5s, não menos |
| `ContentLoadException` inesperada | algum caminho novo carrega conteúdo sem `try/catch` — ver *Limitações* |

---

## Limitações conhecidas

Não são testadas automaticamente. Viram **checklist manual** antes de uma release:

| Área | Por quê |
|---|---|
| `Draw`, HUD, parallax, ordenação por profundidade | exigem GPU e `SpriteBatch` |
| `Animation` / `AnimationPlayer` | o construtor desreferencia `Texture2D.Height` |
| `FighterAnimator.Draw` e as tiras reais | exigem conteúdo compilado |
| `BeatEmUpScreen`, `GameScreen`, pilha do `ScreenManager` | exigem um `Game` vivo |
| `ImGuiDevEditor` (F1) | só desktop Debug, depende do nativo `cimgui` |
| Smoke E2E `Menu → Play → Arena → Fim da Demo` | exigiria janela e conteúdo compilado; **não** foi criada infraestrutura E2E para isso. O equivalente lógico (arena → onda → combate → conclusão) está coberto em `ArenaTests` e `AllStagesTests` sem GPU |
| Largura real das seções | vem da textura de fundo escalada; headless cai em `fallbackWidth` — ver a questão em aberto nº 11 |
| Áudio de verdade (`MusicPlayer` chamando `MediaPlayer`) | exige dispositivo de áudio. Os testes cobrem os guardas que importam para robustez — faixa ausente, faixa sem nome, `Stop` sem nada tocando, clamp de volume — e a regra de **qual** faixa carregar, extraída para o predicado puro `MusicPlayer.NeedsReload` (o player é um só para todas as telas; sem essa regra a arena retocaria o tema do menu). Param aí: com a faixa carregada (`song != null`) qualquer caminho chama `MediaPlayer`. Daí o `MusicPlayer` aparecer atrás no relatório de cobertura; `Audio/` **não** está em `coverlet.runsettings` de propósito, para o número ficar visível em vez de escondido. Ouvir a faixa, o ponto de loop, o fade e **que a arena toca a faixa certa** é checklist manual |
| Efeitos sonoros de verdade (`SoundPlayer` chamando `SoundEffect.Play`) | mesmo motivo. Sem conteúdo compilado todo load falha, então o que fica coberto é a robustez (efeito ausente, efeito sem nome) e a regra que só importa por ser hot path: **um load que falhou não é repetido** — provada com `CountingContentManager`, porque o resultado audível de um load repetido é idêntico ao de um load único. A partir do efeito carregado qualquer caminho chama o mixer. **Quando** cada som dispara, ao contrário, está inteiramente coberto: a regra está em `CombatSounds` (pura) e o disparo em `CapaoRasoArena` (headless) |

**Checklist manual** (`dotnet run --project Curitiba/Curitiba.DesktopGL`):
menu abre → Play carrega a arena → Sofia anda nas 8 direções, ataca, pula, dá dash → inimigos
entram e atacam → câmera trava e libera ao limpar a área → transição de seção → "Fim da Demo" →
F1 abre o editor → salvar o JSON recarrega a cena.

**Checklist manual do HUD de score** (mesmo comando) — `ScoreHud` decide os textos e o fade e está
coberto por teste, mas nada disso prova que aparece na tela no lugar certo:
o score aparece no canto superior direito e **não colide** com "Capão Raso" no centro → o número
sobe a cada golpe que conecta e dá um salto ao derrotar um inimigo → do 2º golpe da sequência em
diante o combo aparece logo abaixo, em amarelo → o `x2` só entra no 5º golpe (antes disso o combo
aparece sem multiplicador) → parar de atacar apaga o combo com um fade curto, e o score fica →
levar dano corta o combo na hora → a fonte tem todos os glifos de `Resources.Combo` nos 4 idiomas.

**Checklist manual de áudio** (mesmo comando):
música entra no menu com fade-in de ~1 s, não em volume cheio → ouvir o ponto de loop em 57,8 s
(o `IsRepeating` do DesktopGL reinicia por callback e pode estalar) → Configurações e Sobre não
cortam nem reiniciam a faixa → Play: o fade do som e o fade-to-black terminam juntos e o som está
zerado **antes** da tela de loading → **a arena entra com `RubberBassRiot`, não com a faixa do
menu** (é este item que prova o cache por nome do `MusicPlayer` no dispositivo real) → ela sobe com
fade e fica nitidamente **atrás** da ação, sem cansar numa partida inteira → ouvir o ponto de loop
dela em 1min39s → Esc abre a pausa: a música **abaixa** e não reinicia; Resume devolve o volume →
terminar a fase: o som some junto com a troca de tela e o **"Fim da Demo" fica em silêncio** →
voltar ao menu: a faixa do menu recomeça do início → morrer: a fase reinicia e a faixa recomeça do
zero → Sair pelo menu de pausa sem faixa vazando para o menu → Esc → Sair sem travar → renomear
`Content/Music/SunlightOnTheShrubs.xnb` e `Content/Music/RubberBassRiot.xnb` na pasta de saída: o
jogo roda **mudo, sem quebrar**.

**Checklist manual de efeitos sonoros** (mesmo comando):
encostar num Pia Loco e socar (`J`): o impacto soa **a cada soco que conecta**, nos três socos da
corrente, e **lê acima** da música de fundo em 0,30 → **o som muda a cada soco**: socar sem parar por
uns 15 golpes e confirmar que não se ouve o mesmo sample duas vezes seguidas, nem um padrão óbvio de
cinco → **o impacto é simultâneo ao golpe na tela**, não alguns frames depois (é isto que o trim de
silêncio compra; um som atrasado lê como bug) → socar o ar: silêncio → o **chute finalizador**
(4º golpe da corrente): o whoosh sai **quando a perna sai**, não quando acerta — chutar o ar e ouvir
só o whoosh, chutar um inimigo e ouvir whoosh **e depois** o impacto, sem soarem colados → o whoosh
fica **abaixo** do impacto (0,6 contra 0,7) e não rouba o golpe → o **ataque aéreo**: silêncio, ainda
sem som próprio → um Pia Loco atacando: nenhum whoosh (o canal segue a Sofia) → um soco que pega dois
inimigos encostados: **um** impacto, não dois sobrepostos (dois one-shots no mesmo frame somam
amplitude e viram um clique) → martelar `J` num grupo grande: sem estalo e sem travada de frame →
salvar o JSON com o jogo aberto (hot-reload) e socar de novo: o som **continua** → renomear
os `Content/Sounds/PunchHit*.xnb` e `Content/Sounds/KickSwing.xnb` na pasta de saída: o jogo roda
**mudo, sem quebrar**, e sem engasgar a cada soco (a falha de load é memorizada, por variante).

**Checklist manual da ambiência da multidão** (mesmo comando): **a onda se anuncia** — no segundo
em que os Piá Locos entram, o gemido sai em ~0,5 a 1,5 s, sem esperar o intervalo regular; vale para
**toda** onda, não só a primeira → parado **sem bater**, os gemidos seguintes vêm a cada poucos
segundos (uma onda de três fica em 3,5–5,75 s) e **leem por baixo** da música em 0,30 — é presença,
não evento; se chamar atenção, `ZombieAmbience.MoanVolume` está alto → **não atrapalha o combate**:
socar durante um gemido e confirmar que o impacto continua nítido por cima → **a horda é mais
barulhenta**: uma onda de quatro geme nitidamente mais que o último inimigo vivo → **corredor limpo é
silêncio**: limpar a onda e esperar além de 8 s sem ouvir nada → **sem loop audível**: ~2 min parado,
as três variantes se revezam e nenhuma repete em seguida → **gemidos sobrepostos são esperados** com
a horda cheia (as janelas são menores que os ~4,3 s do sample mais longo): vozes sobrepostas devem
ler como **mais zumbis**; se lerem como lama, o botão é **encurtar os samples**, não alargar a
janela — o arco natural de 3,5 a 4,3 s é o único número deste bloco decidido **de ouvido** → pausa
(`Esc`) com um gemido no ar: a ambiência **para de agendar** junto com o jogo → morrer: nenhum
gemido novo durante o fade de derrota → hot-reload: a ambiência **continua** depois do rebuild →
renomear os `Content/Sounds/ZombieMoan*.xnb` na saída: o jogo roda **mudo, sem quebrar**.

---

## Questões em aberto

Comportamentos estranhos encontrados ao escrever os testes. **Nada foi corrigido** — os testes
fixam o comportamento *atual*. Cada item precisa de uma decisão (bug, regra intencional ou legado).

1. `InputState.inputTransformation` começa como `default(Matrix)` (tudo zero, não identidade), então
   `TransformCursorLocation` mapeia qualquer clique para `(0,0)` até `ScalePresentationArea` rodar.
   → `InputTransformationTests.AFreshInputState_ShouldMapEveryCursorPositionToTheOrigin`
2. `LocalizationManager.DEFAULT_CULTURE_CODE` é `"en-EN"`, que não é uma cultura real. O ICU fabrica
   uma em vez de lançar, então "funciona" por acidente, caindo no recurso neutro.
3. `CuritibaGame.Initialize` indexa `languages[settings.Language]` sem guarda. `Language` vem das
   settings salvas e o default é `2`: remover uma satellite assembly quebra a inicialização.
4. `ConsoleSettingsStorage` nunca define `SpecialFolderPath`, e o campo é `protected static`
   compartilhado — instanciar dois storages diferentes redireciona o primeiro.
5. `EnemyProfile.PreferredDistance` não é lido em lugar nenhum (dado morto).
6. `ParsePersonality` (arena) é **case-sensitive** e cai em `Balanced` em silêncio, enquanto
   `ParseSpawnType` é case-insensitive. O `StageValidator` sinaliza isso como *Warning*.
7. `StageSection.Mode` compara com o literal `800` em vez da largura real da viewport.
8. `Fighter.throwDuration` (0,5 s) não é configurável por `FighterTuning`, ao contrário de todo o
   resto das durações.
9. `StageHotReloader.Dispose()` não é idempotente: a segunda chamada lança `ObjectDisposedException`.
10. Hot-reload com JSON inválido falha em silêncio — 20 tentativas e desiste, sem nenhum sinal.
11. **`fallbackWidth` não comporta os próprios locks.** A seção 1 de `capao-raso.json` é autorada com
    `fallbackWidth: 1600`, mas tem ondas travando a câmera em 1002 e 1500 — além dos 800 que a câmera
    alcançaria nessa largura. Com a arte presente a largura real é bem maior e a fase funciona; se a
    textura `WallInfinite` faltasse, a fase **travaria para sempre**. O `StageValidator` reporta como
    *Warning*. → `AllStagesTests.FallbackWidths_ShouldSupportTheirOwnLocks`
12. `CA1001`: `BackgroundScreen`, `MainMenuScreen` e `ImGuiRenderer` guardam campos `IDisposable`
    sem serem `IDisposable`. Configurado como *suggestion* no `.editorconfig`.

Divergências do `CLAUDE.md` encontradas e já corrigidas lá: o ataque da Sofia é `J` (não `Space`),
`CuritibaLeaderboard` não existe no código, e o `.tmj` **não** é copiado para a saída.

---

## CI

`.github/workflows/ci.yml` roda em `windows-latest`, em push e PR para `master`:

```bash
dotnet restore Curitiba.CI.slnx
dotnet tool restore                   # ReportGenerator (manifesto da raiz)
dotnet build   Curitiba.CI.slnx --configuration Release --no-restore
dotnet test    Curitiba.CI.slnx --configuration Release --no-build --collect:"XPlat Code Coverage"                --settings coverlet.runsettings --results-directory artifacts/test-results
dotnet reportgenerator -reports:artifacts/test-results/**/coverage.cobertura.xml                -targetdir:artifacts/coverage -reporttypes:"Html;MarkdownSummaryGithub;TextSummary"
```

O PR não é válido se o build ou os testes falharem — **e só por isso**. Cobertura é publicada, nunca
exigida: não há limite mínimo nem gate, e um relatório que não sai vira aviso, não falha.

| Artefato | Conteúdo |
|---|---|
| `test-results` | os `.trx` |
| `coverage` | o `coverage.cobertura.xml` cru |
| `coverage-html` | o relatório navegável (baixe, descompacte, abra `index.html`) |

A tabela por classe também vai para o **resumo do job** e para um **comentário do PR**, reescrito a
cada push (`gh pr comment --edit-last --create-if-none`, sem action de terceiro) em vez de empilhar
um comentário por commit. PR vindo de *fork* pula o comentário — o token dele é read-only —, mas
continua recebendo o resumo do job e os artefatos.

O cache de pacotes NuGet é chaveado pelo hash dos `.csproj`/`.slnx`/manifestos de ferramentas: como
não há `packages.lock.json` no repo, o `cache: true` do `setup-dotnet` não serve aqui.

---

## Mutation testing

Prova de conceito com [Stryker.NET](https://stryker-mutator.io/), escopada ao código de combate
(`stryker-config.json` na raiz). Não faz parte do CI e não bloqueia nada.

```bash
dotnet tool install -g dotnet-stryker
dotnet-stryker --config-file stryker-config.json
```

Roda em ~75 s e gera um relatório HTML em `StrykerOutput/` (ignorado pelo git). Resultado atual:

| Arquivo | Score | Mutantes sobreviventes | Sem cobertura |
|---|---|---|---|
| `ComboMove.cs`, `ComboChainDef.cs`, `EnemyAi.cs` | 100% | 0 | 0 |
| `WaveManager.cs` | 96% | 1 | 0 |
| `CombatDefaults.cs` | 95% | 1 | 0 |
| `Camera2D.cs` | 89% | 2 | 0 |
| `InputBuffer.cs` | 83% | 1 | 0 |
| `Fighter.cs` | 42% | 106 | **108** |
| **Total** | **51%** | | |

A leitura correta é a segunda coluna, não a primeira: **onde a regra é testada, os testes de fato
detectam a alteração** — as classes puras matam de 83% a 100% dos mutantes. O 42% de `Fighter.cs`
vem quase todo dos 108 mutantes *sem cobertura*, que estão em `Draw`, no cálculo do tremor de
acerto e nos caminhos de pulo/meio-fio que a suíte deliberadamente não dirige (ver *Limitações*).

Vale investigar os poucos sobreviventes em código coberto — é exatamente para isso que a
ferramenta serve — mas isso é trabalho de acompanhamento, não um bloqueio.

## Análise estática

`.editorconfig` na raiz. A formatação apenas escreve o estilo que o código já usa. Os analyzers
ficam em `AnalysisMode=None`: **nada é ligado por padrão**, e o `.editorconfig` opta por um punhado
de regras onde um acerto é um bug de verdade (`CA2200`, `CA2011`, `CA2245`, `CA2017` como erro;
`CA2201`, `CA2215`, `CA1806`, `CA2214` como aviso). Assim um aviso no build sempre significa algo.

`TreatWarningsAsErrors` está ligado **só** em `Curitiba.Tests` — o código do jogo não é
nullable-aware e ligá-lo lá geraria centenas de avisos sem valor.
