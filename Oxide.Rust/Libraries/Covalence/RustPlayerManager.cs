﻿extern alias Oxide;

using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide::ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Game.Rust.Libraries.Covalence
{
    /// <summary>
    /// Represents a generic player manager
    /// </summary>
    public class RustPlayerManager : IPlayerManager
    {
        [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
        private struct PlayerRecord
        {
            public string Name;
            public ulong Id;
        }

        private IDictionary<string, PlayerRecord> playerData;
        private IDictionary<string, RustPlayer> allPlayers;
        private IDictionary<string, RustPlayer> connectedPlayers;

        /// <summary>
        /// Initializes player data
        /// </summary>
        internal void Initialize()
        {
            Utility.DatafileToProto<Dictionary<string, PlayerRecord>>("oxide.covalence");
            playerData = ProtoStorage.Load<Dictionary<string, PlayerRecord>>("oxide.covalence") ?? new Dictionary<string, PlayerRecord>();
            allPlayers = new Dictionary<string, RustPlayer>();
            connectedPlayers = new Dictionary<string, RustPlayer>();

            foreach (var pair in playerData) allPlayers.Add(pair.Key, new RustPlayer(pair.Value.Id, pair.Value.Name));
        }

        /// <summary>
        /// When a player joins
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="name"></param>
        internal void PlayerJoin(ulong userId, string name)
        {
            var id = userId.ToString();

            PlayerRecord record;
            if (playerData.TryGetValue(id, out record))
            {
                record.Name = name;
                playerData[id] = record;
                allPlayers.Remove(id);
                allPlayers.Add(id, new RustPlayer(userId, name));
            }
            else
            {
                record = new PlayerRecord { Id = userId, Name = name };
                playerData.Add(id, record);
                allPlayers.Add(id, new RustPlayer(userId, name));
            }
        }

        /// <summary>
        /// When a player connects
        /// </summary>
        /// <param name="player"></param>
        internal void PlayerConnected(BasePlayer player)
        {
            allPlayers[player.UserIDString] = new RustPlayer(player);
            connectedPlayers[player.UserIDString] = new RustPlayer(player);
        }

        /// <summary>
        /// When a player disconnects
        /// </summary>
        /// <param name="player"></param>
        internal void PlayerDisconnected(BasePlayer player) => connectedPlayers.Remove(player.UserIDString);

        /// <summary>
        /// To save the current player data
        /// </summary>
        internal void SavePlayerData() => ProtoStorage.Save(playerData, "oxide.covalence");

        #region Player Finding

        /// <summary>
        /// Gets all players
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IPlayer> All => allPlayers.Values.Cast<IPlayer>();

        /// <summary>
        /// Gets all connected players
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IPlayer> Connected => connectedPlayers.Values.Cast<IPlayer>();

        /// <summary>
        /// Gets all sleeping players
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IPlayer> Sleeping => BasePlayer.sleepingPlayerList.Select(p => p.IPlayer);

        /// <summary>
        /// Finds a single player given unique ID
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public IPlayer FindPlayerById(string id)
        {
            RustPlayer player;
            return allPlayers.TryGetValue(id, out player) ? player : null;
        }

        /// <summary>
        /// Finds a single connected player given game object
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public IPlayer FindPlayerByObj(object obj) => connectedPlayers.Values.FirstOrDefault(p => p.Object == obj);

        /// <summary>
        /// Finds a single player given a partial name or unique ID (case-insensitive, wildcards accepted, multiple matches returns null)
        /// </summary>
        /// <param name="partialNameOrId"></param>
        /// <returns></returns>
        public IPlayer FindPlayer(string partialNameOrId)
        {
            var players = FindPlayers(partialNameOrId).ToArray();
            return players.Length == 1 ? players[0] : null;
        }

        /// <summary>
        /// Finds any number of players given a partial name or unique ID (case-insensitive, wildcards accepted)
        /// </summary>
        /// <param name="partialNameOrId"></param>
        /// <returns></returns>
        public IEnumerable<IPlayer> FindPlayers(string partialNameOrId)
        {
            foreach (var player in allPlayers.Values)
            {
                if (player.Name != null && player.Name.IndexOf(partialNameOrId, StringComparison.OrdinalIgnoreCase) >= 0 || player.Id == partialNameOrId)
                    yield return player;
            }
        }

        #endregion Player Finding
    }
}
