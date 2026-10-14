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

O Velas 0.3.0 usa a interface nativa do Valheim no mesmo padrão visual do NpcValheim:
moldura de madeira, fontes do jogo, caixas rebaixadas, botões com sons e uma grade com
prévias das velas. Também incorpora o
[ServerSync da Blaxxun](https://github.com/blaxxun-boop/ServerSync) e carrega o catálogo
remoto no runtime do Valheim.
Ao entrar em um servidor, as configurações abaixo são substituídas pelos valores do
servidor. Com `ServerSync.LockConfiguration=true` (padrão), jogadores não podem alterar
esses valores localmente. Apenas `OpenSailSelectorKey` e `DebugMode` continuam sendo
preferências locais.

| chave | padrão | descrição |
|---|---|---|
| `LockConfiguration` | `true` | bloqueia no cliente as opções controladas pelo servidor |
| `Enabled` | `true` | liga/desliga o mod inteiro |
| `OpenSailSelectorKey` | `G` | tecla que abre o seletor de velas, mirando no mastro do navio |
| `MaxInteractionDistance` | `10` | distância máxima (m) do jogador até o mastro para o seletor abrir |
| `MastInteractionRadius` | `0.8` | quão longe (m) do eixo do mastro a mira pode passar e ainda contar como "no mastro" |
| `AdminOnlySails` | _(vazio)_ | ids de velas, separados por vírgula, que só admins do servidor (`adminlist.txt`) podem aplicar |
| `VipOnlySails` | _(vazio)_ | ids de velas, separados por vírgula, que só VIPs (lista do `VipList`) podem aplicar; admins também podem (Aesir 27) |
| `ServerIconOnDefaultSails` | `true` | carimba o ícone do servidor na vela padrão (vanilla) dos navios sem vela personalizada |
| `ServerIconSize` | `0.55` | altura do ícone como fração da altura da vela (0.1 a 1) |
| `SailsRepositoryUrl` | `https://github.com/Deadheim-project/repositorio-das-velas` | repositório das velas customizadas |
| `EnableRemoteSails` | `true` | busca velas do repositório |
| `EnableSailCache` | `true` | cacheia manifesto/imagens em disco |
| `CacheRefreshMinutes` | `30` | tempo antes de re-buscar o manifesto |
| `EnableClanSails` | `true` | aplica a restrição de clã nas velas que declaram uma |
| `EnableAutomaticClanSail` | `true` | aplica a vela do clã automaticamente em navios recém-construídos |
| `DebugMode` | `false` | logs `[Sails] ...` detalhados + habilita os comandos de dev |

A tecla só funciona mirando no **mastro** do navio: o HUD mostra `[G] Trocar vela` quando a
mira está nele. No convés, no leme ou longe do navio a tecla não faz nada.

Para reservar uma vela a admins, coloque o id dela (veja `dhs_sails_status`) em
`[Permissions] AdminOnlySails`, por exemplo `AdminOnlySails = generic_skull_banner, clan_deadheim_01`.
A checagem também roda no dono do navio ao receber o pedido, então um cliente modificado não
consegue aplicá-la.

### Vela só para VIP (Aesir 27)

`[Permissions] VipOnlySails` funciona como o `AdminOnlySails` acima, mas a lista de quem pode
usar é a do VIP (`VipList`, servidor por id de plataforma; cliente por
`VipListApi.IsLocalPlayerVip()`) em vez da `adminlist.txt`. Admins do servidor também podem
aplicar essas velas, VIP ou não -- mesma regra do `AdminOnlySails`. O Velas não referencia o
`VipList.dll` em tempo de compilação: a ligação é por reflexão (`Integration/VipProvider.cs`,
mesmo padrão do `GuildsClanProvider`), então o mod continua funcionando (negando a todos, exceto
admins) se o `VipList` não estiver instalado. Como toda vela que dá alguma vantagem a quem
paga, a checagem definitiva roda no dono do navio ao receber o pedido (não no cliente que pede),
então um cliente modificado não ganha a vela de VIP sem estar na lista.

### Vela liberada por jogador (pacote de Halloween, passe de temporada)

Uma entrada do manifesto pode trazer `"requiresCosmetic": "<id>"`: só quem tiver aquele
cosmético desbloqueado no registro do Deadheim pode escolher a vela, mesmo sendo pública ou de
clã. A ligação com o registro de cosméticos do Deadheim (ver o plano, seção "Base comum") ainda
não está pronta: enquanto `Integration/CosmeticUnlockProvider.cs` não apontar para a API real
(ela ainda não tinha nome anotado no plano em 14/10/2026), toda vela com `requiresCosmetic`
fica bloqueada para todo mundo -- é a falha segura, não um bug. Quando a sessão do Deadheim
anotar o nome da API no plano, atualize as três constantes no topo daquele arquivo; o resto
(checagem, mensagem no seletor, `dhs_sail_why`) já está pronto.

### Vela/brasão de guilda sob encomenda

Serviço manual, sem automação: o jogador compra a `FichaBrasao` (Loja Deadcoins) e negocia a
arte com um admin. Quando a imagem estiver pronta, o admin:

1. Confirma o nome exato da guilda com o jogador (é o nome que o mod Guilds usa; peça para o
   jogador rodar `dhs_clan_whoami` no servidor estando no clã -- a checagem é
   case-insensitive, mas evite espaços/acentos diferentes do nome real).
2. Prepara o PNG da vela respeitando os limites em
   [`docs/manifest-format.md`](docs/manifest-format.md) (tamanho, dimensão, formato) e sobe o
   arquivo em `sails/` no repositório `repositorio-das-velas` (branch `main`).
3. Acrescenta a entrada em `manifest.json`: `id` novo e definitivo (nunca reaproveite nem
   renomeie depois -- veja o aviso no formato do manifesto), `name`, `file`, e `clan` com o
   nome exato do passo 1. `clanDefault: true` é opcional, só se a vela também deve ser aplicada
   automaticamente a todo navio novo construído por um membro daquela guilda.
4. Faz commit e push do `manifest.json` e da imagem. Quem já tiver o jogo aberto vê a vela nova
   depois do próximo `CacheRefreshMinutes` (padrão 30 min) ou ao reentrar no servidor.
5. A restrição por clã (`clan` no manifesto) já existe desde antes desta onda --
   `Clans/GuildsClanProvider.cs` fala com o mod Guilds por reflexão e
   `SailPermissionService` nega a vela a quem não é daquele clã, checado no dono do navio, não
   no cliente que pede. Nada precisou mudar no código para isso: só o passo a passo acima.

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
- `dhs_sail_inspect` — lista renderers, materiais, texturas e o retângulo de UV da vela do navio mais próximo (diagnóstico de vela desalinhada).

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
SailSelectorUI / SailValheimUi / SailInputController -- interface uGUI nativa e o bind que a abre
SailConfig          -- toda a configuração do mod
SailDebugTools       -- comandos de desenvolvimento (isolados)
```
