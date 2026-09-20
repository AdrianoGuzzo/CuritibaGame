# Curitiba

[![CI](https://github.com/AdrianoGuzzo/CuritibaGame/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/AdrianoGuzzo/CuritibaGame/actions/workflows/ci.yml)

**Capão Raso**: uma demo de *beat 'em up* 2D em que **Sofia** atravessa o bairro na porrada contra
hordas de **Pia Loco**. Construída sobre **MonoGame 3.8**, com alvo **.NET 8**.

Quase todo o código vive na biblioteca compartilhada `Curitiba.Core`; quatro projetos "cabeça" finos
apenas empacotam o jogo por plataforma — **DesktopGL**, **WindowsDX**, **Android** e **iOS**. O
repositório também guarda um platformer 2D anterior (`Game/`), hoje dormante e inacessível pelo menu.

## Rodar

```bash
# Cabeça OpenGL — o alvo usual de desenvolvimento
dotnet run --project Curitiba/Curitiba.DesktopGL

# Cabeça DirectX (só Windows)
dotnet run --project Curitiba/Curitiba.WindowsDX
```

O primeiro build restaura sozinho as ferramentas do pipeline de conteúdo do MonoGame e compila os
ativos: ele demora, os seguintes não.

| Ação | Teclado |
|---|---|
| Andar (8 direções) | `WASD` ou setas |
| Atacar | `J` |
| Pular | `Space` |
| Dash | `Shift` |
| Pausar | `Esc` |
| Editor de fase in-game | `F1` (só desktop, build Debug) |

O gamepad espelha o teclado: direcional para andar, `A`/`X` para atacar, `B` para pular e os
shoulders para o dash.

## Compilar e testar

```bash
# Compila o que roda em qualquer SDK .NET: Core + DesktopGL + Tests
dotnet build Curitiba.CI.slnx --configuration Release

# Suíte completa — é exatamente o que o CI roda
dotnet test Curitiba.CI.slnx
```

> `Curitiba.slnx` inclui as cabeças Android e iOS, cujos workloads de mobile estão fora de suporte:
> ela falha num SDK comum, por motivo alheio ao código. Use `Curitiba.CI.slnx` no dia a dia; as
> cabeças mobile são compiladas à parte, numa IDE com os workloads certos.

Todo trabalho de código aqui segue **TDD — RED → GREEN → REFACTOR**, com o teste escrito antes da
implementação. As regras estão no [`CLAUDE.md`](CLAUDE.md).

## Cobertura

```bash
pwsh tools/coverage.ps1      # ou: powershell -File tools/coverage.ps1
```

Limpa os resultados anteriores, roda a suíte com `coverlet.runsettings`, gera o relatório do
ReportGenerator em `artifacts/coverage/` e abre o `index.html`. Use `-NoBrowser` em ambiente sem
navegador e `-ReportOnly` para reprocessar dados já coletados.

O CI gera **o mesmo relatório** em todo push e PR: baixe o artifact **`coverage-html`** do run e abra
o `index.html`, ou leia a tabela por classe direto no **resumo do job** — e no comentário do PR, que
é reescrito a cada push em vez de empilhar.

Não há badge nem limite mínimo de cobertura, de propósito: **cobertura aqui é diagnóstico, não
meta**. O CI só reprova por build quebrado ou teste vermelho.

## Documentação

| Documento | O que tem |
|---|---|
| [`CLAUDE.md`](CLAUDE.md) | visão geral, comandos, arquitetura, pipeline de conteúdo, convenções e a metodologia de TDD |
| [`Curitiba/Curitiba.Tests/README.md`](Curitiba/Curitiba.Tests/README.md) | como escrever e rodar testes, fixtures, checklist manual, limitações e questões em aberto |
