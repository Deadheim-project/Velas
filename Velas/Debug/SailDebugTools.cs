using System.Linq;
using Velas.Clans;
using Velas.Game;
using Velas.Manager;
using Velas.Permissions;
using Velas.Ships;

namespace Velas.Debug
{
    /// <summary>
    /// Dev-only console commands (spec sections 11-13), kept isolated from the rest of the
    /// mod: nothing outside this file/namespace depends on these existing, and removing this
    /// file entirely would not change normal gameplay behavior at all. Registered once from
    /// Plugin.Awake via RegisterAll().
    /// </summary>
    internal static class SailDebugTools
    {
        public static void RegisterAll()
        {
            new Terminal.ConsoleCommand("dhs_spawnTestShip", "[prefab] - spawns a test ship on safe nearby water (default: Raft)",
                args =>
                {
                    var player = Player.m_localPlayer;
                    if (player == null) { args.Context.AddString("Sem jogador local."); return; }
                    string prefab = args.Length >= 2 ? args[1] : "Raft";
                    if (Velas.Debug.TestShipSpawner.TrySpawn(player.transform.position, prefab, out var msg))
                        args.Context.AddString(msg);
                    else
                        args.Context.AddString("Falha: " + msg);
                }, isCheat: true);

            new Terminal.ConsoleCommand("dhs_sails_status", "lists every known sail and the repository state",
                args =>
                {
                    args.Context.AddString($"Repositório remoto: {SailManager.RemoteState}");
                    foreach (var s in SailManager.AllSails.OrderBy(s => s.Source).ThenBy(s => s.Id))
                        args.Context.AddString($"  {s.Id} | {s.DisplayName} | {s.Source} | {(s.IsPublic ? "público" : "clã=" + s.ClanId)}{(s.IsClanDefaultSail ? " [auto]" : "")}");
                }, isCheat: true);

            new Terminal.ConsoleCommand("dhs_sails_refresh", "forces a re-fetch of the remote manifest",
                args =>
                {
                    SailManager.RefreshRemoteSails(forceRefresh: true);
                    args.Context.AddString("Atualização do manifesto remoto solicitada -- veja o log com DebugMode=true.");
                }, isCheat: true);

            new Terminal.ConsoleCommand("dhs_clan_whoami", "shows the local player's clan as seen by the ClanProvider",
                args =>
                {
                    var provider = ClanProvider.Current;
                    if (provider == null || !provider.IsAvailable)
                    {
                        args.Context.AddString("Nenhum sistema de clã disponível (Guilds não instalado, ou simulação não ativa).");
                        return;
                    }
                    long id = Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerID() : 0L;
                    var clan = provider.GetPlayerClan(id);
                    args.Context.AddString(string.IsNullOrEmpty(clan) ? "Sem clã." : $"Clã atual: {clan}");
                }, isCheat: true);

            new Terminal.ConsoleCommand("dhs_clan_simulate", "<clanName|clear> - forces the local player's clan for testing, without needing Guilds installed",
                args =>
                {
                    if (args.Length < 2) { args.Context.AddString("Uso: dhs_clan_simulate <nomeDoClã|clear>"); return; }
                    if (args[1] == "clear")
                    {
                        ClanProvider.ResetToDefault();
                        args.Context.AddString("Simulação de clã removida -- voltando ao provider real (Guilds).");
                        return;
                    }
                    ClanProvider.SetProvider(new FakeClanProvider(args[1]));
                    args.Context.AddString($"Simulando clã '{args[1]}' para o jogador local.");
                }, isCheat: true);

            new Terminal.ConsoleCommand("dhs_sail_why", "<sailId> - explains whether the local player can use a sail, and why not",
                args =>
                {
                    if (args.Length < 2) { args.Context.AddString("Uso: dhs_sail_why <sailId>"); return; }
                    var def = SailManager.Get(args[1]);
                    if (def == null) { args.Context.AddString($"Vela '{args[1]}' desconhecida."); return; }
                    var result = SailPermissionService.CanUseLocal(def);
                    args.Context.AddString($"{args[1]}: {(result.Allowed ? "permitido" : "bloqueado")} -- {result.Describe(def)}");
                }, isCheat: true);

            new Terminal.ConsoleCommand("dhs_sail_auto", "<clanName> - shows which sail (if any) is configured as that clan's automatic sail",
                args =>
                {
                    if (args.Length < 2) { args.Context.AddString("Uso: dhs_sail_auto <nomeDoClã>"); return; }
                    var auto = SailManager.AllSails.FirstOrDefault(s =>
                        s.IsClanDefaultSail && string.Equals(s.ClanId, args[1], System.StringComparison.OrdinalIgnoreCase));
                    args.Context.AddString(auto == null
                        ? $"Clã '{args[1]}' não tem vela automática configurada."
                        : $"Vela automática do clã '{args[1]}': {auto.Id} ({auto.DisplayName})");
                }, isCheat: true);

            new Terminal.ConsoleCommand("dhs_sail_apply", "<sailId> - applies a sail directly to the ship the player is standing on (bypasses the UI, still goes through the real permission/RPC path)",
                args =>
                {
                    var player = Player.m_localPlayer;
                    if (player == null || args.Length < 2) { args.Context.AddString("Uso: dhs_sail_apply <sailId> (fique em cima do navio)"); return; }

                    var ship = ShipFinder.FindNearest(player.transform.position, SailConfig.MaxInteractionDistance.Value);
                    if (ship == null) { args.Context.AddString("Nenhum navio próximo."); return; }

                    var component = ship.GetComponent<ShipSailComponent>();
                    if (component == null) { args.Context.AddString("Navio sem ShipSailComponent."); return; }

                    component.RequestSetSail(args[1]);
                    args.Context.AddString($"Pedido de troca para '{args[1]}' enviado.");
                }, isCheat: true);

            SailLog.Debug("Dev commands registered (dhs_spawnTestShip, dhs_sails_status, dhs_sails_refresh, dhs_clan_whoami, dhs_clan_simulate, dhs_sail_why, dhs_sail_auto, dhs_sail_apply)");
        }
    }
}
