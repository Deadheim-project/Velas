# Formato do repositório de velas (`repositorio-das-velas`)

O mod lê tudo a partir de um repositório GitHub configurado em `SailsRepositoryUrl`
(padrão: `https://github.com/Deadheim-project/repositorio-das-velas`). O mod nunca
precisa saber de antemão quais imagens existem — ele descobre tudo a partir de um
único arquivo, `manifest.json`, na raiz do repositório (branch `main`, com fallback
para `master`).

## Layout esperado

```
repositorio-das-velas/
├── manifest.json
└── sails/
    ├── clan_deadheim_01.png
    ├── raven_public.png
    └── ...
```

Os arquivos de imagem podem ficar em qualquer subpasta relativa à raiz do repositório
— o caminho vem do campo `file` de cada entrada do manifesto. `sails/` é só a
convenção sugerida.

## `manifest.json`

```json
{
  "formatVersion": 1,
  "sails": [
    {
      "id": "clan_deadheim_01",
      "name": "Estandarte de Deadheim",
      "file": "sails/clan_deadheim_01.png",
      "clan": "Deadheim",
      "clanDefault": true,
      "sha256": "b1946ac92492d2347c6235b4d2611184"
    },
    {
      "id": "raven_public",
      "name": "Corvo Errante",
      "file": "sails/raven_public.png"
    }
  ]
}
```

### Campos de cada vela

| campo         | obrigatório | descrição |
|---------------|-------------|-----------|
| `id`          | sim         | Identificador estável e único. Só `[A-Za-z0-9_-]`. **Nunca renomeie um `id` já publicado** — é isso que fica salvo nos navios dos jogadores; renomear "orfaniza" a escolha deles. |
| `name`        | não         | Nome exibido no seletor. Se ausente, usa o `id`. |
| `file`        | sim         | Caminho relativo (sem `..`, sem começar com `/`) até a imagem PNG/JPG dentro do repositório. |
| `clan`        | não         | Nome do clã (do mod Guilds) dono da vela. Ausente/vazio = pública, qualquer jogador pode usar. |
| `clanDefault` | não         | `true` = esta é a vela automática daquele clã (aplicada a navios recém-construídos por membros dele). No máximo uma por clã deve ter `true`. |
| `sha256`      | não, mas recomendado | Hash SHA-256 (hex minúsculo) do arquivo de imagem. Se presente, o mod valida o download/cache contra ele e rejeita arquivos corrompidos ou incompletos. |

### Removendo uma vela

Basta apagar a entrada do `manifest.json` (o arquivo de imagem pode continuar no
repositório ou ser removido, tanto faz). Na próxima atualização do manifesto:

- ela some do seletor para novas escolhas;
- navios que **já** usam aquela vela continuam mostrando-a normalmente (o mod não
  troca a aparência de ninguém sozinho só porque o repositório mudou), desde que
  a imagem já tenha sido baixada/cacheada em algum momento por aquele cliente.

### Limites e validação

O mod trata o conteúdo do repositório como não confiável:

- extensão/conteúdo precisa decodificar como imagem válida;
- tamanho máximo: `MaxImageSizeKb` (padrão 2048 KB);
- dimensão máxima: `MaxImageDimension` (padrão 2048 px);
- `file` não pode conter `..` nem começar com `/` (path traversal bloqueado);
- manifesto ou entrada inválidos são ignorados individualmente — um erro não
  derruba as outras velas.
