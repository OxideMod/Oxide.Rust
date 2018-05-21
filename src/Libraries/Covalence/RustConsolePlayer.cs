using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Globalization;

namespace Oxide.Game.Rust.Libraries.Covalence
{
    /// <summary>
    /// The player object that represents the server console
    /// </summary>
    public class RustConsolePlayer : IPlayer
    {
        #region Objects

        /// <summary>
        /// Gets the object that backs the player
        /// </summary>
        public object Object => null;

        /// <summary>
        /// Gets the player's last command type
        /// </summary>
        public CommandType LastCommand { get => CommandType.Console; set { } }

        #endregion Objects

        #region Information

        /// <summary>
        /// Gets/sets the name for the player
        /// </summary>
        public string Name { get => "Server Console"; set { } }

        /// <summary>
        /// Gets the ID for the player (unique within the current game)
        /// </summary>
        public string Id => "server_console";

        /// <summary>
        /// Gets the player's language
        /// </summary>
        public CultureInfo Language => CultureInfo.InstalledUICulture;

        /// <summary>
        /// Gets the player's IP address
        /// </summary>
        public string Address => "127.0.0.1";

        /// <summary>
        /// Gets the player's average network ping
        /// </summary>
        public int Ping => 0;

        /// <summary>
        /// Returns if the player is admin
        /// </summary>
        public bool IsAdmin => true;

        /// <summary>
        /// Gets if the player is banned
        /// </summary>
        public bool IsBanned => false;

        /// <summary>
        /// Returns if the player is connected
        /// </summary>
        public bool IsConnected => true;

        /// <summary>
        /// Returns if the player is sleeping
        /// </summary>
        public bool IsSleeping => false;

        /// <summary>
        /// Returns if the player is the server
        /// </summary>
        public bool IsServer => true;

        #endregion Information

        #region Administration

        /// <summary>
        /// Bans the player for the specified reason and duration
        /// </summary>
        /// <param name="reason"></param>
        /// <param name="duration"></param>
        public void Ban(string reason, TimeSpan duration)
        {
        }

        /// <summary>
        /// Gets the amount of time remaining on the player's ban
        /// </summary>
        public TimeSpan BanTimeRemaining => TimeSpan.Zero;

        /// <summary>
        /// Heals the player's character by specified amount
        /// </summary>
        /// <param name="amount"></param>
        public void Heal(float amount)
        {
        }

        /// <summary>
        /// Gets/sets the player's health
        /// </summary>
        public float Health { get; set; }

        /// <summary>
        /// Damages the player's character by specified amount
        /// </summary>
        /// <param name="amount"></param>
        public void Hurt(float amount)
        {
        }

        /// <summary>
        /// Kicks the player from the game
        /// </summary>
        /// <param name="reason"></param>
        public void Kick(string reason)
        {
        }

        /// <summary>
        /// Causes the player's character to die
        /// </summary>
        public void Kill()
        {
        }

        /// <summary>
        /// Gets/sets the player's maximum health
        /// </summary>
        public float MaxHealth { get; set; }

        /// <summary>
        /// Renames the player to specified name
        /// <param name="name"></param>
        /// </summary>
        public void Rename(string name)
        {
        }

        /// <summary>
        /// Teleports the player's character to the specified position
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        public void Teleport(float x, float y, float z)
        {
        }

        /// <summary>
        /// Teleports the player's character to the specified generic position
        /// </summary>
        /// <param name="pos"></param>
        public void Teleport(GenericPosition pos) => Teleport(pos.X, pos.Y, pos.Z);

        /// <summary>
        /// Unbans the player
        /// </summary>
        public void Unban()
        {
        }

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
            x = 0;
            y = 0;
            z = 0;
        }

        /// <summary>
        /// Gets the position of the player
        /// </summary>
        /// <returns></returns>
        public GenericPosition Position() => new GenericPosition(0, 0, 0);

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
            message = args.Length > 0 ? string.Format(Formatter.ToPlaintext(message), args) : Formatter.ToPlaintext(message);
            string formatted = prefix != null ? $"{prefix} {message}" : message;
            Interface.Oxide.LogInfo(formatted);
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
            Message(message, prefix, args);
        }

        /// <summary>
        /// Replies to the player with the specified message
        /// </summary>
        /// <param name="message"></param>
        public void Reply(string message) => Message(message, null);

        /// <summary>
        /// Runs the specified console command on the player
        /// </summary>
        /// <param name="command"></param>
        /// <param name="args"></param>
        public void Command(string command, params object[] args)
        {
            ConsoleSystem.Run(ConsoleSystem.Option.Server, command, args);
        }

        #endregion Chat and Commands

        #region Permissions

        /// <summary>
        /// Gets if the player has the specified permission
        /// </summary>
        /// <param name="perm"></param>
        /// <returns></returns>
        public bool HasPermission(string perm) => true;

        /// <summary>
        /// Grants the specified permission on this player
        /// </summary>
        /// <param name="perm"></param>
        public void GrantPermission(string perm)
        {
        }

        /// <summary>
        /// Strips the specified permission from this player
        /// </summary>
        /// <param name="perm"></param>
        public void RevokePermission(string perm)
        {
        }

        /// <summary>
        /// Gets if the player belongs to the specified group
        /// </summary>
        /// <param name="group"></param>
        /// <returns></returns>
        public bool BelongsToGroup(string group) => false;

        /// <summary>
        /// Adds the player to the specified group
        /// </summary>
        /// <param name="group"></param>
        public void AddToGroup(string group)
        {
        }

        /// <summary>
        /// Removes the player from the specified group
        /// </summary>
        /// <param name="group"></param>
        public void RemoveFromGroup(string group)
        {
        }

        #endregion Permissions
    }
}
