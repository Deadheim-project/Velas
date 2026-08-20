# Velas

Mod BepInEx para Valheim que permite personalizar a vela de qualquer navio: um
conjunto de velas genéricas embutidas no mod, mais velas customizadas (algumas
exclusivas de um clã) buscadas de um repositório GitHub configurável.

## Requisitos de build

Mesmo padrão do `NpcValheim`: `dotnet build Velas.sln -c Release`, com
`VALHEIM_PATH` apontando para a instalação do jogo se não estiver no caminho padrão
do Steam. O build já copia a DLL e os assets para
`BepInEx/plugins/Velas` para teste local (`DevTools=true`).

## Configuração

O Velas 0.2.1 incorpora o [ServerSync da Blaxxun](https://github.com/blaxxun-boop/ServerSync).
Ao entrar em um servidor, as configurações abaixo são substituídas pelos valores do
servidor. Com `ServerSync.LockConfiguration=true` (padrão), jogadores não podem alterar
esses valores localmente. Apenas `OpenSailSelectorKey` e `DebugMode` continuam sendo
preferências locais.

| chave | padrão | descrição |
|---|---|---|
| `LockConfiguration` | `true` | bloqueia no cliente as opções controladas pelo servidor |
| `Enabled` | `true` | liga/desliga o mod inteiro |
| `OpenSailSelectorKey` | `G` | tecla que abre o seletor de velas |
| `MaxInteractionDistance` | `10` | distância máxima (m) até o navio para o seletor abrir |
| `SailsRepositoryUrl` | `https://github.com/Deadheim-project/repositorio-das-velas` | repositório das velas customizadas |
| `EnableRemoteSails` | `true` | busca velas do repositório |
| `EnableSailCache` | `true` | cacheia manifesto/imagens em disco |
| `CacheRefreshMinutes` | `30` | tempo antes de re-buscar o manifesto |
| `EnableClanSails` | `true` | aplica a restrição de clã nas velas que declaram uma |
| `EnableAutomaticClanSail` | `true` | aplica a vela do clã automaticamente em navios recém-construídos |
| `DebugMode` | `false` | logs `[Sails] ...` detalhados + habilita os comandos de dev |

Veja [`docs/manifest-format.md`](docs/manifest-format.md) para o formato do
repositório remoto.

## Comandos de desenvolvimento

Todos ficam isolados em `Velas/Debug/` e não afetam o funcionamento normal:

- `dhs_spawnTestShip [prefab]` — spawna um navio de teste em água segura perto do
  jogador (padrão: `Raft`).
- `dhs_sails_status` — lista todas as velas conhecidas e o estado do repositório remoto.
- `dhs_sails_refresh` — força nova busca do manifesto.
- `dhs_clan_whoami` — mostra o clã do jogador local segundo o `ClanProvider`.
- `dhs_clan_simulate <clã|clear>` — força um clã fictício para testes sem o Guilds.
- `dhs_sail_why <sailId>` — explica se o jogador pode usar a vela e, se não, por quê.
- `dhs_sail_auto <clã>` — mostra a vela automática configurada para um clã.
- `dhs_sail_apply <sailId>` — aplica a vela diretamente no navio mais próximo (mesmo caminho de permissão/RPC do seletor, sem abrir a UI).

## Arquitetura

```
SailManager        -- registro de velas conhecidas (genéricas + remotas) e suas texturas
SailRepository      -- busca manifest.json e imagens no GitHub
SailCache           -- cache em disco (BepInEx/config/Velas/cache)
SailTextureLoader    -- bytes -> Texture2D, com validação (tamanho/dimensão/decodificação)
SailPermissionService -- decide se um jogador pode usar uma vela
ClanProvider / IClanProvider / GuildsClanProvider -- abstração Player -> Clan -> permissão
ShipSailController  -- Ship -> Sail Renderer -> Sail Texture
ShipSailComponent   -- estado por navio (ZDO), RPCs, vela automática do clã
SailSelectorUI / SailInputController -- interface (IMGUI) e o bind que a abre
SailConfig          -- toda a configuração do mod
SailDebugTools       -- comandos de desenvolvimento (isolados)
```
