using ConVar;
using Network;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Core.RemoteConsole;
using Oxide.Game.Rust.Libraries.Covalence;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Oxide.Game.Rust
{
    /// <summary>
    /// Game hooks and wrappers for the core Rust plugin
    /// </summary>
    public partial class RustCore
    {
        internal bool isPlayerTakingDamage;
        internal static string ipPattern = @":{1}[0-9]{1}\d*";

        #region Entity Hooks

        /// <summary>
        /// Called when a BaseCombatEntity takes damage
        /// This is used to call OnEntityTakeDamage for anything other than a BasePlayer
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="hitInfo"></param>
        /// <returns></returns>
        [HookMethod("IOnBaseCombatEntityHurt")]
        private object IOnBaseCombatEntityHurt(BaseCombatEntity entity, HitInfo hitInfo)
        {
            return entity is BasePlayer ? null : Interface.CallHook("OnEntityTakeDamage", entity, hitInfo);
        }

        /// <summary>
        /// Called when an NPC animal tries to target an entity
        /// </summary>
        /// <param name="npc"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        [HookMethod("IOnNpcTarget")]
        private object IOnNpcTarget(BaseNpc npc, BaseEntity target)
        {
            if (Interface.CallHook("OnNpcTarget", npc, target) != null)
            {
                npc.SetFact(BaseNpc.Facts.HasEnemy, 0);
                npc.SetFact(BaseNpc.Facts.EnemyRange, 3);
                npc.SetFact(BaseNpc.Facts.AfraidRange, 1);

                //TODO: Find replacements of those:
                // npc.AiContext.EnemyPlayer = null;
                // npc.AiContext.LastEnemyPlayerScore = 0f;

                npc.playerTargetDecisionStartTime = 0f;
                return 0f;
            }

            return null;
        }

        /// <summary>
        /// Called after a BaseNetworkable has been saved into a ProtoBuf object that is about to
        /// be serialized for a network connection or cache
        /// </summary>
        /// <param name="baseNetworkable"></param>
        /// <param name="saveInfo"></param>
        [HookMethod("IOnEntitySaved")]
        private void IOnEntitySaved(BaseNetworkable baseNetworkable, BaseNetworkable.SaveInfo saveInfo)
        {
            // Only call when saving for the network since we don't expect plugins to want to intercept saving to disk
            if (!serverInitialized || saveInfo.forConnection == null)
            {
                return;
            }

            Interface.CallHook("OnEntitySaved", baseNetworkable, saveInfo);
        }

        #endregion Entity Hooks

        #region Item Hooks

        /// <summary>
        /// Called when an item loses durability
        /// </summary>
        /// <param name="item"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        [HookMethod("IOnLoseCondition")]
        private object IOnLoseCondition(Item item, float amount)
        {
            object[] arguments = { item, amount };
            Interface.CallHook("OnLoseCondition", arguments);
            amount = (float)arguments[1];
            float condition = item.condition;
            item.condition -= amount;
            if (item.condition <= 0f && item.condition < condition)
            {
                item.OnBroken();
            }

            return true;
        }

        #endregion Item Hooks

        #region Player Hooks

        /// <summary>
        /// Called when a player attempts to pickup a DoorCloser entity
        /// </summary>
        /// <param name="basePlayer"></param>
        /// <param name="entity"></param>
        /// <returns></returns>
        [HookMethod("ICanPickupEntity")]
        private object ICanPickupEntity(BasePlayer basePlayer, DoorCloser entity)
        {
            object callHook = Interface.CallHook("CanPickupEntity", basePlayer, entity);
            return callHook is bool result && !result ? (object)true : null;
        }

        /// <summary>
        /// Called when a BasePlayer is attacked
        /// This is used to call OnEntityTakeDamage for a BasePlayer when attacked
        /// </summary>
        /// <param name="basePlayer"></param>
        /// <param name="hitInfo"></param>
        [HookMethod("IOnBasePlayerAttacked")]
        private object IOnBasePlayerAttacked(BasePlayer basePlayer, HitInfo hitInfo)
        {
            if (!serverInitialized || basePlayer == null || hitInfo == null || basePlayer.IsDead() || isPlayerTakingDamage || basePlayer is NPCPlayer)
            {
                return null;
            }

            if (Interface.CallHook("OnEntityTakeDamage", basePlayer, hitInfo) != null)
            {
                return true;
            }

            isPlayerTakingDamage = true;
            try
            {
                basePlayer.OnAttacked(hitInfo);
            }
            finally
            {
                isPlayerTakingDamage = false;
            }
            return true;
        }

        /// <summary>
        /// Called when a BasePlayer is hurt
        /// This is used to call OnEntityTakeDamage when the player was hurt without being attacked
        /// </summary>
        /// <param name="basePlayer"></param>
        /// <param name="hitInfo"></param>
        /// <returns></returns>
        [HookMethod("IOnBasePlayerHurt")]
        private object IOnBasePlayerHurt(BasePlayer basePlayer, HitInfo hitInfo)
        {
            return isPlayerTakingDamage ? null : Interface.CallHook("OnEntityTakeDamage", basePlayer, hitInfo);
        }

        /// <summary>
        /// Called when a server group is set for an ID (i.e. banned)
        /// </summary>
        /// <param name="steamId"></param>
        /// <param name="group"></param>
        /// <param name="playerName"></param>
        /// <param name="reason"></param>
        /// <param name="expiry"></param>
        [HookMethod("OnServerUserSet")]
        private void OnServerUserSet(ulong steamId, ServerUsers.UserGroup group, string playerName, string reason, long expiry)
        {
            if (serverInitialized && group == ServerUsers.UserGroup.Banned)
            {
                string playerId = steamId.ToString();
                IPlayer player = Covalence.PlayerManager.FindPlayerById(playerId);
                Interface.CallHook("OnPlayerBanned", playerName, steamId, player?.Address ?? "0", reason, expiry);
                Interface.CallHook("OnUserBanned", playerName, playerId, player?.Address ?? "0", reason, expiry);
            }
        }

        /// <summary>
        /// Called when a server group is removed for an ID (i.e. unbanned)
        /// </summary>
        /// <param name="steamId"></param>
        [HookMethod("OnServerUserRemove")]
        private void OnServerUserRemove(ulong steamId)
        {
            if (serverInitialized && ServerUsers.users.ContainsKey(steamId) && ServerUsers.users[steamId].group == ServerUsers.UserGroup.Banned)
            {
                string playerId = steamId.ToString();
                IPlayer player = Covalence.PlayerManager.FindPlayerById(playerId);
                Interface.CallHook("OnPlayerUnbanned", player?.Name ?? "Unnamed", steamId, player?.Address ?? "0");
                Interface.CallHook("OnUserUnbanned", player?.Name ?? "Unnamed", playerId, player?.Address ?? "0");
            }
        }

        /// <summary>
        /// Called when the player is attempting to connect
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        [HookMethod("IOnUserApprove")]
        private object IOnUserApprove(Connection connection)
        {
            string playerName = connection.username;
            string connectionId = connection.userid.ToString();
            string connectionIp = Regex.Replace(connection.ipaddress, ipPattern, "");
            uint authLevel = connection.authLevel;

            // Update name and groups with permissions
            if (permission.IsLoaded)
            {
                permission.UpdateNickname(connectionId, playerName);
                OxideConfig.DefaultGroups defaultGroups = Interface.Oxide.Config.Options.DefaultGroups;
                if (!permission.UserHasGroup(connectionId, defaultGroups.Players))
                {
                    permission.AddUserGroup(connectionId, defaultGroups.Players);
                }
                if (authLevel >= 2 && !permission.UserHasGroup(connectionId, defaultGroups.Administrators))
                {
                    permission.AddUserGroup(connectionId, defaultGroups.Administrators);
                }
            }

            // Let covalence know
            Covalence.PlayerManager.PlayerJoin(connection.userid, playerName);

            // Call hooks for plugins
            object loginSpecific = Interface.CallHook("CanClientLogin", connection);
            object loginCovalence = Interface.CallHook("CanUserLogin", playerName, connectionId, connectionIp);
            object canLogin = loginSpecific is null ? loginCovalence : loginSpecific;
            if (canLogin is string || canLogin is bool loginBlocked && !loginBlocked)
            {
                ConnectionAuth.Reject(connection, canLogin is string ? canLogin.ToString() : lang.GetMessage("ConnectionRejected", this, connectionId));
                return true;
            }

            // Call hooks for plugins
            object approvedSpecific = Interface.CallHook("OnUserApprove", connection);
            object approvedCovalence = Interface.CallHook("OnUserApproved", playerName, connectionId, connectionIp);
            return approvedSpecific is null ? approvedCovalence : approvedSpecific;
        }

        /// <summary>
        /// Called when the player has been banned by Publisher/VAC
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="status"></param>
        [HookMethod("IOnPlayerBanned")]
        private void IOnPlayerBanned(Connection connection, AuthResponse status)
        {
            // TODO: Get BasePlayer and pass instead of Connection
            Interface.CallHook("OnPlayerBanned", connection, status.ToString());
        }

        /// <summary>
        /// Called when the player sends a chat message
        /// </summary>
        /// <param name="playerId"></param>
        /// <param name="playerName"></param>
        /// <param name="message"></param>
        /// <param name="channel"></param>
        /// <param name="basePlayer"></param>
        /// <returns></returns>
        [HookMethod("IOnPlayerChat")]
        private object IOnPlayerChat(ulong playerId, string playerName, string message, Chat.ChatChannel channel, BasePlayer basePlayer)
        {
            // Ignore empty and "default" text
            if (string.IsNullOrEmpty(message) || message.Equals("text"))
            {
                return true;
            }

            // Check if chat command
            string chatCommandPrefix = CommandHandler.GetChatCommandPrefix(message);
            if ( chatCommandPrefix != null )
            {
                TryRunPlayerCommand( basePlayer, message, chatCommandPrefix );
                return false;
            }

            message = message.EscapeRichText();

            // Check if using Rust+ app
            if (basePlayer == null || !basePlayer.IsConnected)
            {
                // Call offline chat hook
                return Interface.CallHook("OnPlayerOfflineChat", playerId, playerName, message, channel);
            }

            // Call hooks for plugins
            object chatSpecific = Interface.CallHook("OnPlayerChat", basePlayer, message, channel);
            object chatCovalence = Interface.CallHook("OnUserChat", basePlayer.IPlayer, message);
            return chatSpecific is null ? chatCovalence : chatSpecific;
        }

        /// <summary>
        /// Called when the player sends a chat command
        /// </summary>
        /// <param name="basePlayer"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        private void TryRunPlayerCommand(BasePlayer basePlayer, string message, string commandPrefix)
        {
            if (basePlayer == null)
            {
                return;
            }

            string str = message.Replace("\n", "").Replace("\r", "").Trim();

            // Check if it is a chat command
            if (string.IsNullOrEmpty(str))
            {
                return;
            }

            // Parse the command
            ParseCommand(str.Substring(commandPrefix.Length), out string cmd, out string[] args);
            if (cmd == null)
            {
                return;
            }

            // Check if using Rust+ app
            if (!basePlayer.IsConnected)
            {
                Interface.CallHook("OnApplicationCommand", basePlayer, cmd, args);
                Interface.CallHook("OnApplicationCommand", basePlayer.IPlayer, cmd, args);
                return;
            }

            // Is the command blocked?
            object commandSpecific = Interface.CallHook("OnPlayerCommand", basePlayer, cmd, args);
            object commandCovalence = Interface.CallHook("OnUserCommand", basePlayer.IPlayer, cmd, args);
            object canBlock = commandSpecific is null ? commandCovalence : commandSpecific;
            if (canBlock != null)
            {
                return;
            }

            try
            {
                // Is it a valid chat command?
                if (!Covalence.CommandSystem.HandleChatMessage(basePlayer.IPlayer, str) && !cmdlib.HandleChatCommand(basePlayer, cmd, args))
                {
                    if (Interface.Oxide.Config.Options.Modded)
                    {
                        basePlayer.IPlayer.Reply(string.Format(lang.GetMessage("UnknownCommand", this, basePlayer.IPlayer.Id), cmd));
                    }
                }
            }
            catch (Exception ex)
            {
                Exception innerException = ex;
                string errorMessage = string.Empty;
                string pluginName = string.Empty;
                StringBuilder stackTraceSb = new StringBuilder();
                while (innerException != null)
                {
                    string name = innerException.GetType().Name;
                    errorMessage = $"{name}: {innerException.Message}".TrimEnd(' ', ':');
                    stackTraceSb.AppendLine(innerException.StackTrace);
                    if (innerException.InnerException != null)
                    {
                        stackTraceSb.AppendLine($"Rethrow as {name}");
                    }
                    innerException = innerException.InnerException;
                }

                StackTrace stackTrace = new StackTrace(ex, 0, true);
                for (int i = 0; i < stackTrace.FrameCount; i++)
                {
                    StackFrame frame = stackTrace.GetFrame(i);
                    MethodBase method = frame.GetMethod();
                    if (method is null || method.DeclaringType is null) continue;
                    if (method.DeclaringType.Namespace == "Oxide.Plugins")
                    {
                        pluginName = method.DeclaringType.Name;
                    }
                }

                Interface.Oxide.LogError($"Failed to run command '/{cmd}' on plugin '{pluginName}'. ({errorMessage.Replace(Environment.NewLine, " ")}){Environment.NewLine}{stackTrace}");
            }
        }

        /// <summary>
        /// Called when the player is authenticating
        /// </summary>
        /// <param name="connection"></param>
        [HookMethod("OnClientAuth")]
        private void OnClientAuth(Connection connection)
        {
            connection.username = Regex.Replace(connection.username, @"<[^>]*>", string.Empty);
        }

        /// <summary>
        /// Called when the player has connected
        /// </summary>
        /// <param name="basePlayer"></param>
        [HookMethod("IOnPlayerConnected")]
        private void IOnPlayerConnected(BasePlayer basePlayer)
        {
            // Set language for player
            lang.SetLanguage(basePlayer.net.connection.info.GetString("global.language", "en"), basePlayer.UserIDString);

            // Send CUI to player manually
            basePlayer.SendEntitySnapshot(CommunityEntity.ServerInstance);

            // Let covalence know
            Covalence.PlayerManager.PlayerConnected(basePlayer);
            IPlayer player = Covalence.PlayerManager.FindPlayerById(basePlayer.UserIDString);
            if (player != null)
            {
                basePlayer.IPlayer = player;
                Interface.CallHook("OnUserConnected", player);
            }

            Interface.Oxide.CallHook("OnPlayerConnected", basePlayer);
        }

        /// <summary>
        /// Called when the player has disconnected
        /// </summary>
        /// <param name="basePlayer"></param>
        /// <param name="reason"></param>
        [HookMethod("OnPlayerDisconnected")]
        private void OnPlayerDisconnected(BasePlayer basePlayer, string reason)
        {
            IPlayer player = basePlayer.IPlayer;
            if (player != null)
            {
                Interface.CallHook("OnUserDisconnected", player, reason);
            }

            Covalence.PlayerManager.PlayerDisconnected(basePlayer);
        }

        /// <summary>
        /// Called when setting/changing info values for a player
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="key"></param>
        /// <param name="val"></param>
        [HookMethod("OnPlayerSetInfo")]
        private void OnPlayerSetInfo(Connection connection, string key, string val)
        {
            // Change language for player
            if (key == "global.language")
            {
                lang.SetLanguage(val, connection.userid.ToString());

                BasePlayer basePlayer = connection.player as BasePlayer;
                if (basePlayer != null)
                {
                    Interface.CallHook("OnPlayerLanguageChanged", basePlayer, val);
                    if (basePlayer.IPlayer != null)
                    {
                        Interface.CallHook("OnPlayerLanguageChanged", basePlayer.IPlayer, val);
                    }
                }
            }
        }

        /// <summary>
        /// Called when the player has been kicked
        /// </summary>
        /// <param name="basePlayer"></param>
        /// <param name="reason"></param>
        [HookMethod("OnPlayerKicked")]
        private void OnPlayerKicked(BasePlayer basePlayer, string reason)
        {
            IPlayer player = basePlayer.IPlayer;
            if (player != null)
            {
                Interface.CallHook("OnUserKicked", basePlayer.IPlayer, reason);
            }
        }

        /// <summary>
        /// Called when the player is respawning
        /// </summary>
        /// <param name="basePlayer"></param>
        /// <returns></returns>
        [HookMethod("OnPlayerRespawn")]
        private object OnPlayerRespawn(BasePlayer basePlayer)
        {
            IPlayer player = basePlayer.IPlayer;
            return player != null ? Interface.CallHook("OnUserRespawn", player) : null;
        }

        /// <summary>
        /// Called when the player has respawned
        /// </summary>
        /// <param name="basePlayer"></param>
        [HookMethod("OnPlayerRespawned")]
        private void OnPlayerRespawned(BasePlayer basePlayer)
        {
            IPlayer player = basePlayer.IPlayer;
            if (player != null)
            {
                Interface.CallHook("OnUserRespawned", player);
            }
        }

        #endregion Player Hooks

        #region Server Hooks

        /// <summary>
        /// Called when a remote console command is received
        /// </summary>
        /// <returns></returns>
        /// <param name="ipAddress"></param>
        /// <param name="command"></param>
        [HookMethod("IOnRconMessage")]
        private object IOnRconMessage(IPAddress ipAddress, string command)
        {
            if (ipAddress != null && !string.IsNullOrEmpty(command))
            {
                RemoteMessage message = RemoteMessage.GetMessage(command);

                if (string.IsNullOrEmpty(message?.Message))
                {
                    return null;
                }

                if (Interface.CallHook("OnRconMessage", ipAddress, message) != null)
                {
                    return true;
                }

                string[] fullCommand = CommandLine.Split(message.Message);

                if (fullCommand.Length >= 1)
                {
                    string cmd = fullCommand[0].ToLower();
                    string[] args = fullCommand.Skip(1).ToArray();

                    if (Interface.CallHook("OnRconCommand", ipAddress, cmd, args) != null)
                    {
                        return true;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Called when the remote console is initialized
        /// </summary>
        /// <returns></returns>
        [HookMethod("IOnRconInitialize")]
        private object IOnRconInitialize() => Interface.Oxide.Config.Rcon.Enabled ? (object)true : null;

        /// <summary>
        /// Called when the command-line is ran
        /// </summary>
        /// <returns></returns>
        [HookMethod("IOnRunCommandLine")]
        private object IOnRunCommandLine()
        {
            foreach (KeyValuePair<string, string> pair in Facepunch.CommandLine.GetSwitches())
            {
                string value = pair.Value;
                if (value == "")
                {
                    value = "1";
                }
                string str = pair.Key.Substring(1);
                ConsoleSystem.Option options = ConsoleSystem.Option.Unrestricted;
                options.PrintOutput = false;
                ConsoleSystem.Run(options, str, value);
            }
            return false;
        }

        /// <summary>
        /// Called when a server command was run
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        [HookMethod("IOnServerCommand")]
        private object IOnServerCommand(ConsoleSystem.Arg arg)
        {
            // Ignore invalid connections
            if (arg == null || arg.Connection != null && arg.Player() == null)
            {
                return true;
            }

            // Ignore all chat messages
            if (arg.cmd.FullName == "chat.say" || arg.cmd.FullName == "chat.teamsay" || arg.cmd.FullName == "chat.localsay")
            {
                return null;
            }

            // Is the command blocked?
            object commandSpecific = Interface.CallHook("OnServerCommand", arg);
            object commandCovalence = Interface.CallHook("OnServerCommand", arg.cmd.FullName, RustCommandSystem.ExtractArgs(arg));
            object canBlock = commandSpecific is null ? commandCovalence : commandSpecific;
            if (canBlock != null)
            {
                return true;
            }

            return null;
        }

        /// <summary>
        /// Called when the server has updated Steam information
        /// </summary>
        [HookMethod("OnServerInformationUpdated")]
        private void OnServerInformationUpdated()
        {
            // Add Steam tags for Oxide
            SteamServer.GameTags += ",oxide";
            if (Interface.Oxide.Config.Options.Modded)
            {
                SteamServer.GameTags += ",modded";
            }
        }

        #endregion Server Hooks

        #region Depricated Hooks

        [HookMethod( "OnMapMarkerRemove" )]
        private object OnMapMarkerRemove(BasePlayer player, List<ProtoBuf.MapNote> mapMarker, int index)
        {
            return Interface.Oxide.CallDeprecatedHook("OnMapMarkerRemove", "OnMapMarkerRemove(BasePlayer player, List<MapNote> mapMarker, int index)",
                new DateTime(2023, 12, 31), player, mapMarker[index]);
        }

        #endregion
    }
}
