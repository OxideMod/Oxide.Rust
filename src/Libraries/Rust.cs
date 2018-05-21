using Network;
using Oxide.Core;
using Oxide.Core.Libraries;
using System;
using System.Linq;
using System.Reflection;

namespace Oxide.Game.Rust.Libraries
{
    /// <summary>
    /// A library containing utility shortcut functions for Rust
    /// </summary>
    public class Rust : Library
    {
        internal readonly Player Player = new Player();
        internal readonly Server Server = new Server();

        /// <summary>
        /// Returns if this library should be loaded into the global namespace
        /// </summary>
        /// <returns></returns>
        public override bool IsGlobal => false;

        #region Utility

        /// <summary>
        /// Gets private bindingflag for accessing private methods, fields, and properties
        /// </summary>
        [LibraryFunction("PrivateBindingFlag")]
        public BindingFlags PrivateBindingFlag() => BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;

        /// <summary>
        /// Converts a string into a quote safe string
        /// </summary>
        /// <param name="str"></param>
        [LibraryFunction("QuoteSafe")]
        public string QuoteSafe(string str) => str.Quote();

        #endregion Utility

        #region Chat

        /// <summary>
        /// Broadcasts the specified chat message to all players
        /// </summary>
        /// <param name="name"></param>
        /// <param name="message"></param>
        /// <param name="userId"></param>
        [LibraryFunction("BroadcastChat")]
        public void BroadcastChat(string name, string message = null, string userId = "0")
        {
            Server.Broadcast(message, name, Convert.ToUInt64(userId));
        }

        /// <summary>
        /// Sends a chat message to the player
        /// </summary>
        /// <param name="player"></param>
        /// <param name="name"></param>
        /// <param name="message"></param>
        /// <param name="userId"></param>
        [LibraryFunction("SendChatMessage")]
        public void SendChatMessage(BasePlayer player, string name, string message = null, string userId = "0")
        {
            Player.Message(player, message, name, Convert.ToUInt64(userId));
        }

        #endregion Chat

        #region Commands

        /// <summary>
        /// Runs a client command
        /// </summary>
        /// <param name="player"></param>
        /// <param name="command"></param>
        /// <param name="args"></param>
        [LibraryFunction("RunClientCommand")]
        public void RunClientCommand(BasePlayer player, string command, params object[] args)
        {
            Player.Command(player, command, args);
        }

        /// <summary>
        /// Runs a server command
        /// </summary>
        /// <param name="command"></param>
        /// <param name="args"></param>
        [LibraryFunction("RunServerCommand")]
        public void RunServerCommand(string command, params object[] args)
        {
            Server.Command(command, args);
        }

        #endregion Commands

        /// <summary>
        /// Returns the Steam ID for the specified connection as a string
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        [LibraryFunction("UserIDFromConnection")]
        public string UserIDFromConnection(Connection connection)
        {
            return connection.userid.ToString();
        }

        /// <summary>
        /// Returns the Steam ID for the specified building privilege as an array
        /// </summary>
        /// <param name="priv"></param>
        /// <returns></returns>
        [LibraryFunction("UserIDsFromBuildingPrivilege")]
        public Array UserIDsFromBuildingPrivlidge(BuildingPrivlidge priv)
        {
            return priv.authorizedPlayers.Select(eid => eid.userid.ToString()).ToArray();
        }

        /// <summary>
        /// Returns the Steam ID for the specified player as a string
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        [LibraryFunction("UserIDFromPlayer")]
        public string UserIDFromPlayer(BasePlayer player) => player.UserIDString;

        /// <summary>
        /// Returns the Steam ID for the specified entity as a string
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        [LibraryFunction("OwnerIDFromEntity")]
        public string OwnerIDFromEntity(BaseEntity entity) => entity.OwnerID.ToString();

        /// <summary>
        /// Returns the player for the specified name, id or ip
        /// </summary>
        /// <param name="nameOrIdOrIp"></param>
        /// <returns></returns>
        [LibraryFunction("FindPlayer")]
        public BasePlayer FindPlayer(string nameOrIdOrIp) => Player.Find(nameOrIdOrIp);

        /// <summary>
        /// Returns the player for the specified name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        [LibraryFunction("FindPlayerByName")]
        public BasePlayer FindPlayerByName(string name) => Player.Find(name);

        /// <summary>
        /// Returns the player for the specified id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [LibraryFunction("FindPlayerById")]
        public BasePlayer FindPlayerById(ulong id) => Player.FindById(id);

        /// <summary>
        /// Returns the player for the specified id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [LibraryFunction("FindPlayerByIdString")]
        public BasePlayer FindPlayerByIdString(string id) => Player.FindById(id);

        /// <summary>
        /// Forces player position (teleportation)
        /// </summary>
        /// <param name="player"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        [LibraryFunction("ForcePlayerPosition")]
        public void ForcePlayerPosition(BasePlayer player, float x, float y, float z)
        {
            Player.Teleport(player, x, y, z);
        }
    }
}
