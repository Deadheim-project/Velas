using System;
using System.Collections;
using System.Reflection;

namespace Velas.Game
{
    /// <summary>
    /// Small subset of reflection-based Valheim glue (peer id/name resolution, image
    /// decoding). Every BepInEx mod needs some version of this -- it is not domain logic,
    /// so it is duplicated in this mod's own assembly rather than referencing NpcValheim's
    /// copy, which would tie two otherwise-independent mods together. Mirrors the pattern in
    /// NpcValheim/Npc/GameApi.cs.
    /// </summary>
    internal static class ValheimApi
    {
        private const BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        private static FieldInfo _peerCharacterId;
        private static FieldInfo _peerPlayerName;

        /// <summary>Resolves a routed-RPC sender id to the stable character id
        /// (ZDOID.UserID), matching MailboxNpc's convention in NpcValheim. 0 = local/unknown.</summary>
        public static long GetPlayerId(long senderId)
        {
            try
            {
                var online = FindOnlinePlayer(senderId);
                if (online != null) return online.GetPlayerID();

                var peer = FindPeer(senderId);
                if (peer == null)
                    return Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerID() : 0L;

                _peerCharacterId ??= typeof(ZNetPeer).GetField("m_characterID", AnyInstance);
                if (_peerCharacterId?.GetValue(peer) is ZDOID characterId && characterId.UserID != 0L)
                    return characterId.UserID;
            }
            catch (Exception e)
            {
                SailLog.Warn($"could not resolve character id for RPC peer {senderId}: {e.Message}");
            }
            return 0L;
        }

        public static string GetPlayerName(long senderId)
        {
            try
            {
                var online = FindOnlinePlayer(senderId);
                if (online != null) return online.GetPlayerName();

                var peer = FindPeer(senderId);
                if (peer != null)
                {
                    _peerPlayerName ??= typeof(ZNetPeer).GetField("m_playerName", AnyInstance);
                    if (_peerPlayerName?.GetValue(peer) is string name && !string.IsNullOrWhiteSpace(name))
                        return name;
                }
                return Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerName() : "???";
            }
            catch (Exception e)
            {
                SailLog.Warn($"could not resolve player name for {senderId}: {e.Message}");
                return "???";
            }
        }

        /// <summary>Finds the live Player instance behind an RPC sender id, or null if it is
        /// a remote peer whose character is not currently loaded here.</summary>
        public static Player FindOnlinePlayer(long senderId)
        {
            var all = Player.GetAllPlayers();
            if (all == null) return null;

            var peer = FindPeer(senderId);
            string peerName = null;
            if (peer != null)
            {
                _peerPlayerName ??= typeof(ZNetPeer).GetField("m_playerName", AnyInstance);
                peerName = _peerPlayerName?.GetValue(peer) as string;
            }

            foreach (var player in all)
            {
                if (player == null) continue;
                if (player.GetPlayerID() == senderId) return player;
                if (!string.IsNullOrEmpty(peerName) &&
                    string.Equals(player.GetPlayerName(), peerName, StringComparison.OrdinalIgnoreCase))
                    return player;
                try
                {
                    var zdoid = player.GetZDOID();
                    if (zdoid.UserID == senderId) return player;
                }
                catch { /* not spawned yet */ }
            }
            return null;
        }

        private static ZNetPeer FindPeer(long senderId)
        {
            if (senderId == 0L || ZNet.instance == null) return null;
            var peers = GetPeerList();
            if (peers == null) return null;

            _peerCharacterId ??= typeof(ZNetPeer).GetField("m_characterID", AnyInstance);
            foreach (var item in peers)
            {
                if (!(item is ZNetPeer peer) || peer == null) continue;
                if (_peerCharacterId?.GetValue(peer) is ZDOID characterId && characterId.UserID == senderId)
                    return peer;
            }
            return null;
        }

        private static IEnumerable GetPeerList()
        {
            var method = typeof(ZNet).GetMethod("GetPeers", AnyInstance, null, Type.EmptyTypes, null)
                         ?? typeof(ZNet).GetMethod("GetConnectedPeers", AnyInstance, null, Type.EmptyTypes, null);
            if (method != null)
                return method.Invoke(ZNet.instance, Array.Empty<object>()) as IEnumerable;

            var field = typeof(ZNet).GetField("m_peers", AnyInstance);
            return field?.GetValue(ZNet.instance) as IEnumerable;
        }

        /// <summary>PNG/JPG bytes -> Texture2D without a compile-time reference to
        /// UnityEngine.ImageConversionModule (that module targets netstandard2.1, which this
        /// net48 project cannot reference directly). See NpcValheim/Npc/GameApi.TryLoadImage
        /// for the identical rationale.</summary>
        public static bool TryLoadImage(UnityEngine.Texture2D tex, byte[] bytes)
        {
            if (tex == null || bytes == null) return false;
            try
            {
                var type = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule", throwOnError: false);
                var method = type?.GetMethod("LoadImage", new[] { typeof(UnityEngine.Texture2D), typeof(byte[]) });
                if (method == null) return false;
                return (bool)method.Invoke(null, new object[] { tex, bytes });
            }
            catch (Exception e)
            {
                SailLog.Warn($"LoadImage failed: {e.Message}");
                return false;
            }
        }
    }
}
