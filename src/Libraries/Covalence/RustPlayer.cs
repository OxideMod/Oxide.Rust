using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Globalization;
using UnityEngine;

namespace Oxide.Game.Rust.Libraries.Covalence
{
    /// <summary>
    /// Represents a player, either connected or not
    /// </summary>
    public class RustPlayer : IPlayer, IEquatable<IPlayer>
    {
        #region Initialization

        internal readonly Player Player = new Player();

        private static Permission libPerms;

        private readonly BasePlayer player;
        private readonly ulong steamId;

        internal RustPlayer(ulong id, string name)
        {
            if (libPerms == null)
            {
                libPerms = Interface.Oxide.GetLibrary<Permission>();
            }

            steamId = id;
            Name = name.Sanitize();
            Id = id.ToString();
        }

        internal RustPlayer(BasePlayer player) : this(player.userID, player.displayName)
        {
            this.player = player;
        }

        #endregion Initialization

        #region Objects

        /// <summary>
        /// Gets the object that backs the player
        /// </summary>
        public object Object => player;

        /// <summary>
        /// Gets the player's last command type
        /// </summary>
        public CommandType LastCommand { get; set; }

        #endregion Objects

        #region Information

        /// <summary>
        /// Gets/sets the name for the player
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets the ID for the player (unique within the current game)
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the player's language
        /// </summary>
        public CultureInfo Language => Player.Language(player);

        /// <summary>
        /// Gets the player's IP address
        /// </summary>
        public string Address => Player.Address(player);

        /// <summary>
        /// Gets the player's average network ping
        /// </summary>
        public int Ping => Player.Ping(player);

        /// <summary>
        /// Returns if the player is admin
        /// </summary>
        public bool IsAdmin => Player.IsAdmin(steamId);

        /// <summary>
        /// Gets if the player is banned
        /// </summary>
        public bool IsBanned => Player.IsBanned(steamId);

        /// <summary>
        /// Returns if the player is connected
        /// </summary>
        public bool IsConnected => Player.IsConnected(player);

        /// <summary>
        /// Returns if the player is sleeping
        /// </summary>
        public bool IsSleeping => Player.IsSleeping(player);

        /// <summary>
        /// Returns if the player is the server
        /// </summary>
        public bool IsServer => false;

        #endregion Information

        #region Administration

        /// <summary>
        /// Bans the player for the specified reason and duration
        /// </summary>
        /// <param name="reason"></param>
        /// <param name="duration"></param>
        public void Ban(string reason, TimeSpan duration = default(TimeSpan)) => Player.Ban(steamId, reason);

        /// <summary>
        /// Gets the amount of time remaining on the player's ban
        /// </summary>
        public TimeSpan BanTimeRemaining => IsBanned ? TimeSpan.MaxValue : TimeSpan.Zero;

        /// <summary>
        /// Heals the player's character by specified amount
        /// </summary>
        /// <param name="amount"></param>
        public void Heal(float amount) => Player.Heal(player, amount);

        /// <summary>
        /// Gets/sets the player's health
        /// </summary>
        public float Health
        {
            get => player.health;
            set => player.health = value;
        }

        /// <summary>
        /// Damages the player's character by specified amount
        /// </summary>
        /// <param name="amount"></param>
        public void Hurt(float amount) => Player.Hurt(player, amount);

        /// <summary>
        /// Kicks the player from the game
        /// </summary>
        /// <param name="reason"></param>
        public void Kick(string reason) => Player.Kick(player, reason);

        /// <summary>
        /// Causes the player's character to die
        /// </summary>
        public void Kill() => Player.Kill(player);

        /// <summary>
        /// Gets/sets the player's maximum health
        /// </summary>
        public float MaxHealth
        {
            get => player.MaxHealth();
            set => player._maxHealth = value;
        }

        /// <summary>
        /// Renames the player to specified name
        /// <param name="name"></param>
        /// </summary>
        public void Rename(string name) => Player.Rename(player, name);

        /// <summary>
        /// Teleports the player's character to the specified position
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        public void Teleport(float x, float y, float z) => Player.Teleport(player, x, y, z);

        /// <summary>
        /// Teleports the player's character to the specified generic position
        /// </summary>
        /// <param name="pos"></param>
        public void Teleport(GenericPosition pos) => Teleport(pos.X, pos.Y, pos.Z);

        /// <summary>
        /// Unbans the player
        /// </summary>
        public void Unban() => Player.Unban(steamId);

        #endregion Administration

        #region Location

        /// <summary>
        /// Gets the position of the player
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        public void Position(out float x, out float y, out float z)
        {
            Vector3 pos = Player.Position(player);
            x = pos.x;
            y = pos.y;
            z = pos.z;
        }

        /// <summary>
        /// Gets the position of the player
        /// </summary>
        /// <returns></returns>
        public GenericPosition Position()
        {
            Vector3 pos = Player.Position(player);
            return new GenericPosition(pos.x, pos.y, pos.z);
        }

        #endregion Location

        #region Chat and Commands

        /// <summary>
        /// Sends the specified message and prefix to the player
        /// </summary>
        /// <param name="message"></param>
        /// <param name="prefix"></param>
        /// <param name="args"></param>
        public void Message(string message, string prefix, params object[] args)
        {
            Player.Message(player, message, prefix, 0, args);
        }

        /// <summary>
        /// Sends the specified message to the player
        /// </summary>
        /// <param name="message"></param>
        public void Message(string message) => Message(message, null);

        /// <summary>
        /// Replies to the player with the specified message and prefix
        /// </summary>
        /// <param name="message"></param>
        /// <param name="prefix"></param>
        /// <param name="args"></param>
        public void Reply(string message, string prefix, params object[] args)
        {
            switch (LastCommand)
            {
                case CommandType.Chat:
                    Message(message, prefix, args);
                    break;

                case CommandType.Console:
                    player.ConsoleMessage(string.Format(Formatter.ToPlaintext(message), args));
                    break;
            }
        }

        /// <summary>
        /// Replies to the player with the specified message
        /// </summary>
        /// <param name="message"></param>
        public void Reply(string message) => Reply(message, null);

        /// <summary>
        /// Runs the specified console command on the player
        /// </summary>
        /// <param name="command"></param>
        /// <param name="args"></param>
        public void Command(string command, params object[] args) => player.SendConsoleCommand(command, args);

        #endregion Chat and Commands

        #region Permissions

        /// <summary>
        /// Gets if the player has the specified permission
        /// </summary>
        /// <param name="perm"></param>
        /// <returns></returns>
        public bool HasPermission(string perm) => libPerms.UserHasPermission(Id, perm);

        /// <summary>
        /// Grants the specified permission on this player
        /// </summary>
        /// <param name="perm"></param>
        public void GrantPermission(string perm) => libPerms.GrantUserPermission(Id, perm, null);

        /// <summary>
        /// Strips the specified permission from this player
        /// </summary>
        /// <param name="perm"></param>
        public void RevokePermission(string perm) => libPerms.RevokeUserPermission(Id, perm);

        /// <summary>
        /// Gets if the player belongs to the specified group
        /// </summary>
        /// <param name="group"></param>
        /// <returns></returns>
        public bool BelongsToGroup(string group) => libPerms.UserHasGroup(Id, group);

        /// <summary>
        /// Adds the player to the specified group
        /// </summary>
        /// <param name="group"></param>
        public void AddToGroup(string group) => libPerms.AddUserGroup(Id, group);

        /// <summary>
        /// Removes the player from the specified group
        /// </summary>
        /// <param name="group"></param>
        public void RemoveFromGroup(string group) => libPerms.RemoveUserGroup(Id, group);

        #endregion Permissions

        #region Operator Overloads

        /// <summary>
        /// Returns if player's unique ID is equal to another player's unique ID
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Equals(IPlayer other) => Id == other?.Id;

        /// <summary>
        /// Returns if player's object is equal to another player's object
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj) => obj is IPlayer && Id == ((IPlayer)obj).Id;

        /// <summary>
        /// Gets the hash code of the player's unique ID
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode() => Id.GetHashCode();

        /// <summary>
        /// Returns a human readable string representation of this IPlayer
        /// </summary>
        /// <returns></returns>
        public override string ToString() => $"Covalence.RustPlayer[{Id}, {Name}]";

        #endregion Operator Overloads
    }
}
