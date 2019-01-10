using System;
using UnityEngine;

namespace uMod.Rust
{
    [Obsolete("Use Rust instead")]
    public class RustCore : Rust
    {
        [Obsolete("Use IPlayer.FindPlayer instead")]
        public static BasePlayer FindPlayer(string playerNameOrIdOrIp)
        {
            BasePlayer basePlayer = Universal.PlayerManager.FindPlayer(playerNameOrIdOrIp)?.Object as BasePlayer;
            if (basePlayer?.net?.connection != null && basePlayer.net.connection.ipaddress.Equals(playerNameOrIdOrIp))
            {
                return basePlayer;
            }

            return null;
        }

        [Obsolete("Use IPlayer.FindPlayerById instead")]
        public static BasePlayer FindPlayerById(ulong playerId)
        {
            return FindPlayerByIdString(playerId.ToString());
        }

        [Obsolete("Use IPlayer.FindPlayerById instead")]
        public static BasePlayer FindPlayerByIdString(string playerId)
        {
            return Universal.PlayerManager.FindPlayerById(playerId)?.Object as BasePlayer;
        }

        [Obsolete("Use IPlayer.FindPlayer instead")]
        public static BasePlayer FindPlayerByName(string playerName)
        {
            return FindPlayer(playerName);
        }

        [Obsolete("Use IPlayer.Message instead")]
        protected void PrintToConsole(BasePlayer basePlayer, string message, params object[] args)
        {
            if (basePlayer?.net != null)
            {
                basePlayer.SendConsoleCommand("echo " + (args.Length > 0 ? string.Format(message, args) : message));
            }
        }

        [Obsolete("Use IPlayer.Message instead")]
        protected void PrintToConsole(string message, params object[] args)
        {
            if (BasePlayer.activePlayerList.Count >= 1)
            {
                ConsoleNetwork.BroadcastToAllClients("echo " + (args.Length > 0 ? string.Format(message, args) : message));
            }
        }

        [Obsolete("Use IPlayer.Message instead")]
        protected void PrintToChat(BasePlayer basePlayer, string message, params object[] args)
        {
            if (basePlayer?.net != null)
            {
                basePlayer.SendConsoleCommand("chat.add", 0, args.Length > 0 ? string.Format(message, args) : message, 1f);
            }
        }

        [Obsolete("Use server.Broadcast instead")]
        protected void PrintToChat(string message, params object[] args)
        {
            if (BasePlayer.activePlayerList.Count >= 1)
            {
                ConsoleNetwork.BroadcastToAllClients("chat.add", 0, args.Length > 0 ? string.Format(message, args) : message, 1f);
            }
        }

        [Obsolete("Use IPlayer.Reply instead")]
        protected void SendReply(ConsoleSystem.Arg arg, string message, params object[] args)
        {
            BasePlayer basePlayer = arg.Connection?.player as BasePlayer;
            string formatted = args.Length > 0 ? string.Format(message, args) : message;

            if (basePlayer?.net != null)
            {
                basePlayer.SendConsoleCommand("echo " + formatted);
                return;
            }

            Interface.uMod.LogInfo(formatted);
        }

        [Obsolete("Use IPlayer.Reply instead")]
        protected void SendReply(BasePlayer basePlayer, string message, params object[] args)
        {
            PrintToChat(basePlayer, message, args);
        }

        [Obsolete("Use IPlayer.Message instead and/or LogWarning")]
        protected void SendWarning(ConsoleSystem.Arg arg, string message, params object[] args)
        {
            BasePlayer basePlayer = arg.Connection?.player as BasePlayer;
            string formatted = args.Length > 0 ? string.Format(message, args) : message;

            if (basePlayer?.net != null)
            {
                basePlayer.SendConsoleCommand("echo " + formatted);
                return;
            }

            Interface.uMod.LogWarning(formatted);
        }

        [Obsolete("Use IPlayer.Message instead and/or LogError")]
        protected void SendError(ConsoleSystem.Arg arg, string message, params object[] args)
        {
            BasePlayer basePlayer = arg.Connection?.player as BasePlayer;
            string formatted = args.Length > 0 ? string.Format(message, args) : message;

            if (basePlayer?.net != null)
            {
                basePlayer.SendConsoleCommand("echo " + formatted);
                return;
            }

            Interface.uMod.LogError(formatted);
        }

        [Obsolete("Use IPlayer.Teleport instead")]
        protected void ForcePlayerPosition(BasePlayer basePlayer, Vector3 destination)
        {
            basePlayer.IPlayer.Teleport(destination.x, destination.y, destination.z);
        }
    }
}
