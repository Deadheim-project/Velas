using System;
using System.Linq;
using UnityEngine;
using Velas.Clans;
using Velas.Game;
using Velas.Manager;
using Velas.Permissions;

namespace Velas.Ships
{
    /// <summary>
    /// Attached (via Harmony patch, see ShipAwakePatches) to every Ship's own GameObject.
    /// Owns that ship's sail state: reading/writing the persisted SailId, the RPCs that let a
    /// client request a change, and applying the automatic clan sail the first time a ship is
    /// ever seen.
    ///
    /// Authority model matches MailboxNpc in NpcValheim: RPC_RequestSetSail only *does*
    /// anything when Nview.IsOwner() is true, and permission is (re-)checked there -- a
    /// client can request whatever it wants, but only the owning peer's answer ever changes
    /// the ZDO or the visible texture for everyone else. A modified client cannot grant
    /// itself another clan's sail by skipping its own UI checks.
    /// </summary>
    internal class ShipSailComponent : MonoBehaviour
    {
        private ZNetView _nview;
        private Ship _ship;
        private string _appliedSailId;

        private void Awake()
        {
            _ship = GetComponent<Ship>();
            _nview = GetComponent<ZNetView>();
            if (_nview == null || !_nview.IsValid()) return;

            _nview.Register("RPC_DHS_RequestSetSail", (Action<long, string>)RPC_RequestSetSail);
            _nview.Register("RPC_DHS_SailChanged", (Action<long, string>)RPC_SailChanged);

            if (_nview.IsOwner())
                InitializeIfFreshShip();

            ApplyPersistedSailLocally();
        }

        /// <summary>Freshly-built ships have never had DHS_Init set. This is what
        /// distinguishes "just constructed, apply the clan default" from "loaded from a save
        /// that predates this mod or already has an explicit choice" -- an empty SailId alone
        /// is not enough since "no sail chosen yet" and "explicitly cleared" would look the
        /// same otherwise.</summary>
        private void InitializeIfFreshShip()
        {
            var zdo = _nview.GetZDO();
            if (zdo.GetInt(SailZdoKeys.Initialized, 0) == 1) return;
            zdo.Set(SailZdoKeys.Initialized, 1);

            if (!SailConfig.EnableAutomaticClanSail.Value) return;

            long creatorId = GetCreatorId();
            if (creatorId == 0L) return;

            var provider = ClanProvider.Current;
            if (provider == null || !provider.IsAvailable) return;

            var clan = provider.GetPlayerClan(creatorId);
            if (string.IsNullOrEmpty(clan)) return;

            var autoSail = SailManager.AllSails.FirstOrDefault(s =>
                s.IsClanDefaultSail && string.Equals(s.ClanId, clan, StringComparison.OrdinalIgnoreCase));
            if (autoSail == null) return;

            SailLog.Debug($"Player clan: {clan}");
            SailLog.Debug($"Applying automatic clan sail: {autoSail.Id}");
            zdo.Set(SailZdoKeys.SailId, autoSail.Id);
        }

        private long GetCreatorId()
        {
            var piece = GetComponent<Piece>();
            return piece != null ? piece.GetCreator() : 0L;
        }

        private void ApplyPersistedSailLocally()
        {
            var zdo = _nview?.GetZDO();
            if (zdo == null || !zdo.IsValid()) return;
            var sailId = zdo.GetString(SailZdoKeys.SailId, "");
            if (string.IsNullOrEmpty(sailId)) return;
            ApplyLocally(sailId);
        }

        private void ApplyLocally(string sailId)
        {
            if (_ship == null || string.IsNullOrEmpty(sailId)) return;
            var texture = SailManager.ResolveTexture(sailId);
            if (texture == null) return;
            if (ShipSailController.ApplyTexture(_ship, texture))
                _appliedSailId = sailId;
        }

        /// <summary>Client-side entry point used by the selector UI. Fire-and-forget: the
        /// visible result comes back through RPC_SailChanged (or nothing, if denied).</summary>
        public void RequestSetSail(string sailId)
        {
            if (_nview == null || !_nview.IsValid() || string.IsNullOrEmpty(sailId)) return;
            SailLog.Debug($"Synchronizing sail: requesting '{sailId}' for ship {_ship?.name}");
            _nview.InvokeRPC("RPC_DHS_RequestSetSail", sailId);
        }

        private void RPC_RequestSetSail(long sender, string sailId)
        {
            if (_nview == null || !_nview.IsOwner()) return;

            var def = SailManager.Get(sailId);
            var result = SailPermissionService.CanUse(def, sender);
            if (!result.Allowed)
            {
                SailLog.Info($"Denied sail '{sailId}' to {ValheimApi.GetPlayerName(sender)}: {result.Describe(def)}");
                return;
            }

            SailLog.Debug($"Applying sail: '{sailId}' on ship {_ship?.name} (requested by {ValheimApi.GetPlayerName(sender)})");
            _nview.GetZDO().Set(SailZdoKeys.SailId, sailId);
            ApplyLocally(sailId);

            SailLog.Debug($"Synchronizing sail: broadcasting '{sailId}' for ship {_ship?.name}");
            _nview.InvokeRPC(ZRoutedRpc.Everybody, "RPC_DHS_SailChanged", sailId);
        }

        private void RPC_SailChanged(long sender, string sailId)
        {
            ApplyLocally(sailId);
        }

        public string CurrentSailId => _appliedSailId;
    }
}
