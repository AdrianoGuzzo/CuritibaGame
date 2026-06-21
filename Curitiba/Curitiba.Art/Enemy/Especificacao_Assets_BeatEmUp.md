# Especificação de Assets - Beat 'em Up Curitiba

## Observação Importante

As imagens fornecidas são folhas de referência de design (1536x1024 com
retrato, paleta e textos em PT), não spritesheets prontas.

As próprias folhas indicam: - Quadros 64x64 px - PNG transparente - Uma
linha por animação

Elas precisam ser fatiadas antes de virar asset de jogo.

## Divergência de nomes

A arte rotula os personagens como: - LIA - MORADOR

Mas a especificação oficial do jogo determina:

-   Heroína: Sofia
-   Inimigo comum: Pia Loco

Devemos seguir a especificação oficial nos nomes de jogo, HUD, código e
documentação, utilizando os sprites apenas como referência visual.

## Inimigo Comum: Pia Loco

Visual utilizado: - Sprite identificado como "Morador"

Função: - Inimigo básico da fase Capão Raso - Aparece diversas vezes
durante a fase

Animações: - Idle: 6 frames - Walk: 6 frames - Attack: 6 frames - Hit: 3
frames - Death: 4 frames

Total: 25 frames

## Estrutura recomendada

Assets/ ├── Characters/ │ ├── Sofia/ │ └── PiaLoco/
