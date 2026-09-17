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
dotnet test Curitiba.CI.slnx --collect:"XPlat Code Coverage" --settings coverlet.runsettings
```

O relatório Cobertura sai em `Curitiba/Curitiba.Tests/TestResults/<guid>/coverage.cobertura.xml`.
Para um HTML navegável:

```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:artifacts/coverage -reporttypes:Html
```

`coverlet.runsettings` (na raiz do repo) exclui o que não dá para exercitar sem GPU/janela
(`Screens/`, `DevTools/ImGui*`, `Effects/`, `CuritibaGame`, o platformer legado em `Game/`).
**O número que importa é branch coverage nas regras críticas**, não o total:

| Área | Linha | Branch |
|---|---|---|
| `Camera2D`, `WaveManager`, `AttackSlotManager`, `SpawnArea`, `SpawnPoint` | 100% | 100% |
| `CombatDefaults`, `ComboMove`, `InputBuffer`, `EnemyProfile`, `FighterTuning` | 100% | 100% |
| `StageDefinition`, `SettingsManager<T>`, `CuritibaSettings`, `StageReloadPolicy` | 100% | 100% |
| `MenuMusicPolicy` | 100% | 100% |
| `SpawnManager` | 98% | 94% |
| `TiledImporter` | 97% | 89% |
| `SofiaPlayer` | 96% | 97% |
| `PiaLocoEnemy` | 95% | 94% |
| `StageValidator` | 94% | 92% |
| `Fighter` | 76% | 73% |
| `MusicPlayer` | 70% | 63% |
| `CapaoRasoArena` | 60% | 61% |

O que falta em `Fighter` e `CapaoRasoArena` é essencialmente `Draw`/HUD — ver *Limitações*.

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
│   └── ArenaTests.cs                 integração: ondas → seções → Completed
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
│   └── MusicPlayerTests.cs           faixa ausente/sem nome, clamp de volume
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
| Áudio de verdade (`MusicPlayer` chamando `MediaPlayer`) | exige dispositivo de áudio. Os testes cobrem os guardas que importam para robustez — faixa ausente, faixa sem nome, `Stop` sem nada tocando, clamp de volume — e param aí: com a faixa carregada (`song != null`) qualquer caminho chama `MediaPlayer`. Daí os 70%/62,5% de `MusicPlayer` na tabela de cobertura; `Audio/` **não** está em `coverlet.runsettings` de propósito, para o número ficar visível em vez de escondido. Ouvir a faixa, o ponto de loop e o fade é checklist manual |

**Checklist manual** (`dotnet run --project Curitiba/Curitiba.DesktopGL`):
menu abre → Play carrega a arena → Sofia anda nas 8 direções, ataca, pula, dá dash → inimigos
entram e atacam → câmera trava e libera ao limpar a área → transição de seção → "Fim da Demo" →
F1 abre o editor → salvar o JSON recarrega a cena.

**Checklist manual de áudio** (mesmo comando):
música entra no menu com fade-in de ~1 s, não em volume cheio → ouvir o ponto de loop em 57,8 s
(o `IsRepeating` do DesktopGL reinicia por callback e pode estalar) → Configurações e Sobre não
cortam nem reiniciam a faixa → Play: o fade do som e o fade-to-black terminam juntos e o som está
zerado **antes** da tela de loading → arena em silêncio do começo ao fim → "Fim da Demo" e voltar
ao menu: a faixa recomeça do início → Esc → Sair sem travar → renomear
`Content/Music/SunlightOnTheShrubs.xnb` na pasta de saída: o jogo roda **mudo, sem quebrar**.

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
dotnet build   Curitiba.CI.slnx --configuration Release --no-restore
dotnet test    Curitiba.CI.slnx --configuration Release --no-build --collect:"XPlat Code Coverage"
```

O PR não é válido se o build ou os testes falharem. Resultados (`.trx`) e cobertura sobem como
artefatos, e a cobertura aparece no resumo do job.

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
