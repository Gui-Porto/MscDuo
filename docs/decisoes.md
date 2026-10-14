# Decisões de arquitetura

## 2026-10-14: o host guarda o mundo

O save do host contém o mundo inteiro: as duas casas, as duas Satsumas e todos os itens.
O PC do jogador 2 guarda apenas o perfil dele (dinheiro, necessidades, multas, chaves, nome e posição) em `player2.txt`, na pasta do mod.

- Sempre o mesmo jogador hospeda a sessão.
- Antes de qualquer save do jogo, o mod volta para o perfil do jogador 1, para o save do jogo nunca receber os valores do jogador 2.
- Itens podem ter uma etiqueta de dono para regras de jogo, mas ela não decide onde o item é salvo.
- Alternativa descartada por enquanto: cada jogador salvar as próprias coisas e enviá-las ao host ao conectar. Pode voltar como "exportar/importar mundo".
