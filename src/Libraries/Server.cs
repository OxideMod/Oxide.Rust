using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Game.Rust.Libraries
{
    public class Server : Library
    {
        #region Chat and Commands

        /// <summary>
        /// Broadcasts the specified chat message and prefix to all players
        /// </summary>
        /// <param name="message"></param>
        /// <param name="userId"></param>
        /// <param name="prefix"></param>
        /// <param name="args"></param>
        public void Broadcast(string message, string prefix, ulong userId = 0, params object[] args)
        {
            if (!string.IsNullOrEmpty(message))
            {
                message = args.Length > 0 ? string.Format(Formatter.ToUnity(message), args) : Formatter.ToUnity(message);
                string formatted = prefix != null ? $"{prefix}: {message}" : message;
                ConsoleNetwork.BroadcastToAllClients("chat.add", 2, userId, formatted);
            }
        }

        /// <summary>
        /// Broadcasts the specified chat message to all players
        /// </summary>
        /// <param name="message"></param>
        /// <param name="userId"></param>
        public void Broadcast(string message, ulong userId = 0) => Broadcast(message, null, userId);

        /// <summary>
        /// Runs the specified server command
        /// </summary>
        /// <param name="command"></param>
        /// <param name="args"></param>
        public void Command(string command, params object[] args)
        {
            ConsoleSystem.Run(ConsoleSystem.Option.Server, command, args);
        }

        #endregion Chat and Commands
    }
}
