using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Oxide.Game.Rust.Libraries
{
    public class Player : Library
    {
        #region Initialization

        private static readonly string ipPattern = @":{1}[0-9]{1}\d*";

        internal readonly Permission permission = Interface.Oxide.GetLibrary<Permission>();

        #endregion Initialization

        #region Information

        /// <summary>
        /// Gets the player's language
        /// </summary>
        public CultureInfo Language(BasePlayer player) => CultureInfo.GetCultureInfo(player.net.connection.info.GetString("global.language") ?? "en");

        /// <summary>
        /// Gets the player's IP address
        /// </summary>
        public string Address(Network.Connection connection) => Regex.Replace(connection.ipaddress, ipPattern, "");

        /// <summary>
        /// Gets the player's IP address
        /// </summary>
        public string Address(BasePlayer player) => player?.net?.connection != null ? Address(player.net.connection) : null;

        /// <summary>
        /// Gets the player's average network ping
        /// </summary>
        public int Ping(Network.Connection connection) => Network.Net.sv.GetAveragePing(connection);

        /// <summary>
        /// Gets the player's average network ping
        /// </summary>
        public int Ping(BasePlayer player) => Ping(player.net.connection);

        /// <summary>
        /// Returns if the player is admin
        /// </summary>
        public bool IsAdmin(ulong id) => ServerUsers.Is(id, ServerUsers.UserGroup.Owner);

        /// <summary>
        /// Returns if the player is admin
        /// </summary>
        public bool IsAdmin(string id) => IsAdmin(Convert.ToUInt64(id));

        /// <summary>
        /// Returns if the player is admin
        /// </summary>
        public bool IsAdmin(BasePlayer player) => IsAdmin(player.userID);

        /// <summary>
        /// Gets if the player is banned
        /// </summary>
        public bool IsBanned(ulong id) => ServerUsers.Is(id, ServerUsers.UserGroup.Banned);

        /// <summary>
        /// Gets if the player is banned
        /// </summary>
        public bool IsBanned(string id) => IsBanned(Convert.ToUInt64(id));

        /// <summary>
        /// Gets if the player is banned
        /// </summary>
        public bool IsBanned(BasePlayer player) => IsBanned(player.userID);

        /// <summary>
        /// Gets if the player is connected
        /// </summary>
        public bool IsConnected(BasePlayer player) => player.IsConnected;

        /// <summary>
        /// Returns if the player is sleeping
        /// </summary>
        public bool IsSleeping(ulong id) => BasePlayer.FindSleeping(id);

        /// <summary>
        /// Returns if the player is sleeping
        /// </summary>
        public bool IsSleeping(string id) => IsSleeping(Convert.ToUInt64(id));

        /// <summary>
        /// Returns if the player is sleeping
        /// </summary>
        public bool IsSleeping(BasePlayer player) => IsSleeping(player.userID);

        #endregion Information

        #region Administration

        /// <summary>
        /// Bans the player from the server based on user ID
        /// </summary>
        /// <param name="id"></param>
        /// <param name="reason"></param>
        public void Ban(ulong id, string reason = "")
        {
            if (IsBanned(id))
            {
                return;
            }

            BasePlayer player = FindById(id);
            ServerUsers.Set(id, ServerUsers.UserGroup.Banned, player?.displayName ?? "Unknown", reason);
            ServerUsers.Save();
            if (player != null && IsConnected(player))
            {
                Kick(player, reason);
            }
        }

        /// <summary>
        /// Bans the player from the server based on user ID
        /// </summary>
        /// <param name="id"></param>
        /// <param name="reason"></param>
        public void Ban(string id, string reason = "") => Ban(Convert.ToUInt64(id), reason);

        /// <summary>
        /// Bans the player from the server
        /// </summary>
        /// <param name="player"></param>
        /// <param name="reason"></param>
        public void Ban(BasePlayer player, string reason = "") => Ban(player.UserIDString, reason);

        /// <summary>
        /// Heals the player by specified amount
        /// </summary>
        /// <param name="player"></param>
        /// <param name="amount"></param>
        public void Heal(BasePlayer player, float amount) => player.Heal(amount);

        /// <summary>
        /// Damages the player by specified amount
        /// </summary>
        /// <param name="player"></param>
        /// <param name="amount"></param>
        public void Hurt(BasePlayer player, float amount) => player.Hurt(amount);

        /// <summary>
        /// Kicks the player from the server
        /// </summary>
        /// <param name="player"></param>
        /// <param name="reason"></param>
        public void Kick(BasePlayer player, string reason = "") => player.Kick(reason);

        /// <summary>
        /// Causes the player to die
        /// </summary>
        /// <param name="player"></param>
        public void Kill(BasePlayer player) => player.Die();

        /// <summary>
        /// Renames the player to specified name
        /// <param name="session"></param>
        /// <param name="name"></param>
        /// </summary>
        public void Rename(BasePlayer player, string name)
        {
            name = string.IsNullOrEmpty(name.Trim()) ? player.displayName : name;

            player.net.connection.username = name;
            player.displayName = name;
            player._name = name;
            player.SendNetworkUpdateImmediate();

            player.IPlayer.Name = name;
            permission.UpdateNickname(player.UserIDString, name);

            Teleport(player, player.transform.position);
        }

        /// <summary>
        /// Teleports the player to the specified position
        /// </summary>
        /// <param name="player"></param>
        /// <param name="destination"></param>
        public void Teleport(BasePlayer player, Vector3 destination)
        {
            if (player.IsAlive() && !player.IsSpectating())
            {
                try
                {
                    // Dismount and remove parent, if applicable
                    player.EnsureDismounted();
                    player.SetParent(null, true, true);

                    // Prevent player from getting hurt
                    //player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
                    //player.UpdatePlayerCollider(true);
                    //player.UpdatePlayerRigidbody(false);
                    player.SetServerFall(true);

                    // Teleport the player to position
                    player.MovePosition(destination);
                    player.ClientRPCPlayer(null, player, "ForcePositionTo", destination);

                    // Update network group if outside current group
                    /*if (!player.net.sv.visibility.IsInside(player.net.group, destination))
                    {
                        player.UpdateNetworkGroup();
                        player.SendNetworkUpdateImmediate();
                        player.ClearEntityQueue();
                        player.SendFullSnapshot();
                    }*/
                }
                finally
                {
                    // Restore player behavior
                    //player.UpdatePlayerCollider(true);
                    //player.UpdatePlayerRigidbody(true);
                    player.SetServerFall(false);
                }
            }
        }

        /// <summary>
        /// Teleports the player to the target player
        /// </summary>
        /// <param name="player"></param>
        /// <param name="target"></param>
        public void Teleport(BasePlayer player, BasePlayer target) => Teleport(player, Position(target));

        /// <summary>
        /// Teleports the player to the specified position
        /// </summary>
        /// <param name="player"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        public void Teleport(BasePlayer player, float x, float y, float z) => Teleport(player, new Vector3(x, y, z));

        /// <summary>
        /// Unbans the player by user ID
        /// </summary>
        public void Unban(ulong id)
        {
            if (!IsBanned(id))
            {
                return;
            }

            ServerUsers.Remove(id);
            ServerUsers.Save();
        }

        /// <summary>
        /// Unbans the player by user ID
        /// </summary>
        public void Unban(string id) => Unban(Convert.ToUInt64(id));

        /// <summary>
        /// Unbans the player
        /// </summary>
        public void Unban(BasePlayer player) => Unban(player.userID);

        #endregion Administration

        #region Location

        /// <summary>
        /// Returns the position of player as Vector3
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public Vector3 Position(BasePlayer player) => player.transform.position;

        #endregion Location

        #region Player Finding

        /// <summary>
        /// Gets the player object using a name, Steam ID, or IP address
        /// </summary>
        /// <param name="nameOrIdOrIp"></param>
        /// <returns></returns>
        public BasePlayer Find(string nameOrIdOrIp)
        {
            foreach (BasePlayer player in Players)
            {
                if (!nameOrIdOrIp.Equals(player.displayName, StringComparison.OrdinalIgnoreCase) &&
                    !nameOrIdOrIp.Equals(player.UserIDString) && !nameOrIdOrIp.Equals(player.net.connection.ipaddress))
                {
                    continue;
                }

                return player;
            }
            return null;
        }

        /// <summary>
        /// Gets the player object using a Steam ID
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public BasePlayer FindById(string id)
        {
            foreach (BasePlayer player in Players)
            {
                if (!id.Equals(player.UserIDString))
                {
                    continue;
                }

                return player;
            }
            return null;
        }

        /// <summary>
        /// Gets the player object using a Steam ID
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public BasePlayer FindById(ulong id)
        {
            foreach (BasePlayer player in Players)
            {
                if (!id.Equals(player.userID))
                {
                    continue;
                }

                return player;
            }
            return null;
        }

        /// <summary>
        /// Returns all connected players
        /// </summary>
        public ListHashSet<BasePlayer> Players => BasePlayer.activePlayerList;

        /// <summary>
        /// Returns all sleeping players
        /// </summary>
        public ListHashSet<BasePlayer> Sleepers => BasePlayer.sleepingPlayerList;

        #endregion Player Finding

        #region Chat and Commands

        /// <summary>
        /// Sends the specified message and prefix to the player
        /// </summary>
        /// <param name="player"></param>
        /// <param name="message"></param>
        /// <param name="prefix"></param>
        /// <param name="args"></param>
        public void Message(BasePlayer player, string message, string prefix, ulong userId = 0, params object[] args)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            message = args.Length > 0 ? string.Format(Formatter.ToUnity(message), args) : Formatter.ToUnity(message);
            string formatted = prefix != null ? $"{prefix} {message}" : message;
            player.SendConsoleCommand("chat.add", 2, userId, formatted);
        }

        /// <summary>
        /// Sends the specified message to the player
        /// </summary>
        /// <param name="player"></param>
        /// <param name="message"></param>
        /// <param name="userId"></param>
        public void Message(BasePlayer player, string message, ulong userId = 0) => Message(player, message, null, userId);

        /// <summary>
        /// Replies to the player with the specified message and prefix
        /// </summary>
        /// <param name="player"></param>
        /// <param name="message"></param>
        /// <param name="prefix"></param>
        /// <param name="args"></param>
        public void Reply(BasePlayer player, string message, string prefix, ulong userId = 0, params object[] args)
        {
            Message(player, message, prefix, userId, args);
        }

        /// <summary>
        /// Replies to the player with the specified message
        /// </summary>
        /// <param name="player"></param>
        /// <param name="message"></param>
        /// <param name="userId"></param>
        public void Reply(BasePlayer player, string message, ulong userId = 0) => Message(player, message, null, userId);

        /// <summary>
        /// Runs the specified player command
        /// </summary>
        /// <param name="player"></param>
        /// <param name="command"></param>
        /// <param name="args"></param>
        public void Command(BasePlayer player, string command, params object[] args) => player.SendConsoleCommand(command, args);

        #endregion Chat and Commands

        #region Item Handling

        /// <summary>
        /// Drops item by item ID from player's inventory
        /// </summary>
        /// <param name="player"></param>
        /// <param name="itemId"></param>
        public void DropItem(BasePlayer player, int itemId)
        {
            Vector3 position = player.transform.position;
            PlayerInventory inventory = Inventory(player);
            for (int s = 0; s < inventory.containerMain.capacity; s++)
            {
                global::Item i = inventory.containerMain.GetSlot(s);
                if (i.info.itemid == itemId)
                {
                    i.Drop(position + new Vector3(0f, 1f, 0f) + position / 2f, (position + new Vector3(0f, 0.2f, 0f)) * 8f);
                }
            }
            for (int s = 0; s < inventory.containerBelt.capacity; s++)
            {
                global::Item i = inventory.containerBelt.GetSlot(s);
                if (i.info.itemid == itemId)
                {
                    i.Drop(position + new Vector3(0f, 1f, 0f) + position / 2f, (position + new Vector3(0f, 0.2f, 0f)) * 8f);
                }
            }
            for (int s = 0; s < inventory.containerWear.capacity; s++)
            {
                global::Item i = inventory.containerWear.GetSlot(s);
                if (i.info.itemid == itemId)
                {
                    i.Drop(position + new Vector3(0f, 1f, 0f) + position / 2f, (position + new Vector3(0f, 0.2f, 0f)) * 8f);
                }
            }
        }

        /// <summary>
        /// Drops item from the player's inventory
        /// </summary>
        /// <param name="player"></param>
        /// <param name="item"></param>
        public void DropItem(BasePlayer player, global::Item item)
        {
            Vector3 position = player.transform.position;
            PlayerInventory inventory = Inventory(player);
            for (int s = 0; s < inventory.containerMain.capacity; s++)
            {
                global::Item i = inventory.containerMain.GetSlot(s);
                if (i == item)
                {
                    i.Drop(position + new Vector3(0f, 1f, 0f) + position / 2f, (position + new Vector3(0f, 0.2f, 0f)) * 8f);
                }
            }
            for (int s = 0; s < inventory.containerBelt.capacity; s++)
            {
                global::Item i = inventory.containerBelt.GetSlot(s);
                if (i == item)
                {
                    i.Drop(position + new Vector3(0f, 1f, 0f) + position / 2f, (position + new Vector3(0f, 0.2f, 0f)) * 8f);
                }
            }
            for (int s = 0; s < inventory.containerWear.capacity; s++)
            {
                global::Item i = inventory.containerWear.GetSlot(s);
                if (i == item)
                {
                    i.Drop(position + new Vector3(0f, 1f, 0f) + position / 2f, (position + new Vector3(0f, 0.2f, 0f)) * 8f);
                }
            }
        }

        /// <summary>
        /// Gives quantity of an item to the player
        /// </summary>
        /// <param name="player"></param>
        /// <param name="itemId"></param>
        /// <param name="quantity"></param>
        public void GiveItem(BasePlayer player, int itemId, int quantity = 1) => GiveItem(player, Item.GetItem(itemId), quantity);

        /// <summary>
        /// Gives quantity of an item to the player
        /// </summary>
        /// <param name="player"></param>
        /// <param name="item"></param>
        /// <param name="quantity"></param>
        public void GiveItem(BasePlayer player, global::Item item, int quantity = 1) => player.inventory.GiveItem(ItemManager.CreateByItemID(item.info.itemid, quantity));

        #endregion Item Handling

        #region Inventory Handling

        /// <summary>
        /// Gets the inventory of the player
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public PlayerInventory Inventory(BasePlayer player) => player.inventory;

        /// <summary>
        /// Clears the inventory of the player
        /// </summary>
        /// <param name="player"></param>
        public void ClearInventory(BasePlayer player) => Inventory(player)?.Strip();

        /// <summary>
        /// Resets the inventory of the player
        /// </summary>
        /// <param name="player"></param>
        public void ResetInventory(BasePlayer player)
        {
            PlayerInventory inventory = Inventory(player);
            if (inventory != null)
            {
                inventory.DoDestroy();
                inventory.ServerInit(player);
            }
        }

        #endregion Inventory Handling
    }
}
