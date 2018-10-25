extern alias References;

using References::ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using uMod.Libraries.Covalence;

namespace uMod.Rust
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
        private const string dataFileName = "umod";

        internal void Initialize()
        {
            // TODO: Migrate/move from oxide.covalence.data to umod.data if SQLite is not used, else migrate to umod.db with SQLite
            playerData = ProtoStorage.Load<Dictionary<string, PlayerRecord>>(dataFileName) ?? new Dictionary<string, PlayerRecord>();
            allPlayers = new Dictionary<string, RustPlayer>();
            connectedPlayers = new Dictionary<string, RustPlayer>();

            foreach (KeyValuePair<string, PlayerRecord> pair in playerData)
            {
                allPlayers.Add(pair.Key, new RustPlayer(pair.Value.Id, pair.Value.Name));
            }
        }

        internal void PlayerJoin(ulong userId, string name)
        {
            string id = userId.ToString();

            if (playerData.TryGetValue(id, out PlayerRecord record))
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

        internal void PlayerConnected(BasePlayer player)
        {
            allPlayers[player.UserIDString] = new RustPlayer(player);
            connectedPlayers[player.UserIDString] = new RustPlayer(player);
        }

        internal void PlayerDisconnected(BasePlayer player) => connectedPlayers.Remove(player.UserIDString);

        internal void SavePlayerData() => ProtoStorage.Save(playerData, dataFileName);

        #region Player Finding

        /// <summary>
        /// Gets all players
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IPlayer> All => allPlayers.Values;

        /// <summary>
        /// Gets all connected players
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IPlayer> Connected => connectedPlayers.Values;

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
            return allPlayers.TryGetValue(id, out RustPlayer player) ? player : null;
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
            IPlayer[] players = FindPlayers(partialNameOrId).ToArray();
            return players.Length == 1 ? players[0] : null;
        }

        /// <summary>
        /// Finds any number of players given a partial name or unique ID (case-insensitive, wildcards accepted)
        /// </summary>
        /// <param name="partialNameOrId"></param>
        /// <returns></returns>
        public IEnumerable<IPlayer> FindPlayers(string partialNameOrId)
        {
            foreach (RustPlayer player in allPlayers.Values)
            {
                if (player.Name != null && player.Name.IndexOf(partialNameOrId, StringComparison.OrdinalIgnoreCase) >= 0 || player.Id == partialNameOrId)
                {
                    yield return player;
                }
            }
        }

        #endregion Player Finding
    }
}
