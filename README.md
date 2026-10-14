# MSC Duo

Mod multiplayer P2P para **My Summer Car** com **progresso individual**: dois jogadores no mesmo mundo, cada um com a própria Satsuma, casa, garagem, dinheiro, necessidades e progresso de rally, com interação física total entre os dois.

> Projeto de fãs, sem afiliação com a Amistech Games. Requer uma cópia legítima do jogo na Steam.

## Status

Fase 0: base do projeto e ferramentas de inspeção.

O plano completo está em [docs/plano-msc-duo.pdf](docs/plano-msc-duo.pdf).

## Requisitos

- My Summer Car (Steam)
- [MSCLoader](https://github.com/piotrulos/MSCModLoader)
- Visual Studio 2022 com .NET Framework 3.5

## Estrutura

```
src/        código do mod (C#, .NET 3.5)
docs/       plano e mapas de FSM
unity/      projeto Unity 5.0.0f4 para AssetBundles (futuro)
```

## Créditos

- [MSCLoader](https://github.com/piotrulos/MSCModLoader), GPL v3
- [MSCMP](https://github.com/RootKiller/MSCMP-OLD), GPL v3, usado como referência

## Licença

[GPL v3](LICENSE)
