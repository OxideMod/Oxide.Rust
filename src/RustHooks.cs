using ConVar;
using Network;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Rust.Ai;
using Rust.Ai.HTN;
using Steamworks;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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

        #region Server Hooks

        /// <summary>
        /// Called when a remote console command is received
        /// </summary>
        /// <returns></returns>
        /// <param name="ipAddress"></param>
        /// <param name="command"></param>
        [HookMethod("IOnRconCommand")]
        private object IOnRconCommand(IPAddress ipAddress, string command)
        {
            if (ipAddress != null && !string.IsNullOrEmpty(command))
            {
                string[] fullCommand = CommandLine.Split(command);
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
            if (arg == null || arg.Connection != null && arg.Player() == null)
            {
                return true; // Ignore console commands from client during connection
            }

            return arg.cmd.FullName != "chat.say" ? Interface.CallHook("OnServerCommand", arg) : null;
        }

        /// <summary>
        /// Called when the server is updating Steam information
        /// </summary>
        [HookMethod("IOnUpdateServerInformation")]
        private void IOnUpdateServerInformation()
        {
            // Add Steam tags for Oxide
            SteamServer.GameTags += ",oxide";
            if (Interface.Oxide.Config.Options.Modded)
            {
                SteamServer.GameTags += ",modded";
            }
        }

        /// <summary>
        /// Called when the server description is updating
        /// </summary>
        [HookMethod("IOnUpdateServerDescription")]
        private void IOnUpdateServerDescription()
        {
            // Fix for server description not always updating
            SteamServer.SetKey("description_0", string.Empty);
        }

        #endregion Server Hooks

        #region Player Hooks

        /// <summary>
        /// Called when a player attempts to pickup a DoorCloser entity
        /// </summary>
        /// <param name="player"></param>
        /// <param name="entity"></param>
        /// <returns></returns>
        [HookMethod("ICanPickupEntity")]
        private object ICanPickupEntity(BasePlayer player, DoorCloser entity)
        {
            object callHook = Interface.CallHook("CanPickupEntity", player, entity);
            return callHook is bool result && !result ? (object)true : null;
        }

        /// <summary>
        /// Called when a BasePlayer is attacked
        /// This is used to call OnEntityTakeDamage for a BasePlayer when attacked
        /// </summary>
        /// <param name="player"></param>
        /// <param name="info"></param>
        [HookMethod("IOnBasePlayerAttacked")]
        private object IOnBasePlayerAttacked(BasePlayer player, HitInfo info)
        {
            if (!serverInitialized || player == null || info == null || player.IsDead() || isPlayerTakingDamage || player is NPCPlayer)
            {
                return null;
            }

            if (Interface.CallHook("OnEntityTakeDamage", player, info) != null)
            {
                return true;
            }

            isPlayerTakingDamage = true;
            try
            {
                player.OnAttacked(info);
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
        /// <param name="player"></param>
        /// <param name="info"></param>
        /// <returns></returns>
        [HookMethod("IOnBasePlayerHurt")]
        private object IOnBasePlayerHurt(BasePlayer player, HitInfo info)
        {
            return isPlayerTakingDamage ? null : Interface.CallHook("OnEntityTakeDamage", player, info);
        }

        /// <summary>
        /// Called when a server group is set for an ID (i.e. banned)
        /// </summary>
        /// <param name="steamId"></param>
        /// <param name="group"></param>
        /// <param name="name"></param>
        /// <param name="reason"></param>
        [HookMethod("IOnServerUsersSet")]
        private void IOnServerUsersSet(ulong steamId, ServerUsers.UserGroup group, string name, string reason)
        {
            if (serverInitialized)
            {
                string id = steamId.ToString();
                IPlayer iplayer = Covalence.PlayerManager.FindPlayerById(id);
                if (group == ServerUsers.UserGroup.Banned)
                {
                    Interface.CallHook("OnPlayerBanned", name, steamId, iplayer?.Address ?? "0", reason);
                    Interface.CallHook("OnUserBanned", name, id, iplayer?.Address ?? "0", reason);
                }
            }
        }

        /// <summary>
        /// Called when a server group is removed for an ID (i.e. unbanned)
        /// </summary>
        /// <param name="steamId"></param>
        [HookMethod("IOnServerUsersRemove")]
        private void IOnServerUsersRemove(ulong steamId)
        {
            if (serverInitialized)
            {
                string id = steamId.ToString();
                IPlayer iplayer = Covalence.PlayerManager.FindPlayerById(id);
                if (ServerUsers.Is(steamId, ServerUsers.UserGroup.Banned))
                {
                    Interface.CallHook("OnPlayerUnbanned", iplayer?.Name ?? "Unnamed", steamId, iplayer?.Address ?? "0");
                    Interface.CallHook("OnUserUnbanned", iplayer?.Name ?? "Unnamed", id, iplayer?.Address ?? "0");
                }
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
            string name = connection.username;
            string id = connection.userid.ToString();
            string ip = Regex.Replace(connection.ipaddress, ipPattern, "");
            uint authLevel = connection.authLevel;

            // Update player's permissions group and name
            if (permission.IsLoaded)
            {
                permission.UpdateNickname(id, name);

                OxideConfig.DefaultGroups defaultGroups = Interface.Oxide.Config.Options.DefaultGroups;

                if (!permission.UserHasGroup(id, defaultGroups.Players))
                {
                    permission.AddUserGroup(id, defaultGroups.Players);
                }

                if (authLevel == 2 && !permission.UserHasGroup(id, defaultGroups.Administrators))
                {
                    permission.AddUserGroup(id, defaultGroups.Administrators);
                }
            }

            Covalence.PlayerManager.PlayerJoin(connection.userid, name); // TODO: Handle this automatically

            object loginSpecific = Interface.CallHook("CanClientLogin", connection);
            object loginCovalence = Interface.CallHook("CanUserLogin", name, id, ip);
            object canLogin = loginSpecific ?? loginCovalence; // TODO: Fix 'RustCore' hook conflict when both return

            if (canLogin is string || canLogin is bool && !(bool)canLogin)
            {
                ConnectionAuth.Reject(connection, canLogin is string ? canLogin.ToString() : lang.GetMessage("ConnectionRejected", this, id));
                return true;
            }

            // Call game and covalence hooks
            object approvedSpecific = Interface.CallHook("OnUserApprove", connection);
            object approvedCovalence = Interface.CallHook("OnUserApproved", name, id, ip);
            return approvedSpecific ?? approvedCovalence; // TODO: Fix 'RustCore' hook conflict when both return
        }

        /// <summary>
        /// Called when the player has been banned by EAC
        /// </summary>
        /// <param name="connection"></param>
        [HookMethod("IOnPlayerBanned")]
        private void IOnPlayerBanned(Connection connection)
        {
            string ip = Regex.Replace(connection.ipaddress, ipPattern, "") ?? "0";
            string reason = connection.authStatus ?? "Unknown"; // TODO: Localization

            Interface.CallHook("OnPlayerBanned", connection.username, connection.userid, ip, reason);
            Interface.CallHook("OnUserBanned", connection.username, connection.userid.ToString(), ip, reason);
        }

        /// <summary>
        /// Called when the player sends a chat message
        /// </summary>
        /// <param name="arg"></param>
        /// <param name="message"></param>
        /// <param name="channel"></param>
        /// <returns></returns>
        [HookMethod("IOnPlayerChat")]
        private object IOnPlayerChat(ConsoleSystem.Arg arg, string message, Chat.ChatChannel channel)
        {
            // Ignore empty and "default" text
            if (string.IsNullOrEmpty(message) || message.Equals("text"))
            {
                return true;
            }

            // Update arg with escaped message
            arg.Args[0] = message;

            // Get player objects
            BasePlayer player = arg.Connection.player as BasePlayer;
            IPlayer iplayer = player?.IPlayer;
            if (iplayer == null)
            {
                return null;
            }

            // Call game and covalence hooks
            object chatSpecific = Interface.CallHook("OnPlayerChat", arg, channel);
            object chatCovalence = Interface.CallHook("OnUserChat", iplayer, message);
            return chatSpecific ?? chatCovalence; // TODO: Fix 'RustCore' hook conflict when both return
        }

        /// <summary>
        /// Called when the player sends a chat command
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        [HookMethod("IOnPlayerCommand")]
        private void IOnPlayerCommand(ConsoleSystem.Arg arg)
        {
            string str = arg.GetString(0).Replace("\n", "").Replace("\r", "").Trim();

            // Check if it is a chat command
            if (string.IsNullOrEmpty(str) || str[0] != '/' || str.Length <= 1)
            {
                return;
            }

            // Parse it
            string cmd;
            string[] args;
            ParseCommand(str.TrimStart('/'), out cmd, out args);
            if (cmd == null)
            {
                return;
            }

            // Get player objects
            BasePlayer player = arg.Connection.player as BasePlayer;
            IPlayer iplayer = player?.IPlayer;
            if (iplayer == null)
            {
                return;
            }

            // Is the command blocked?
            object commandSpecific = Interface.CallHook("OnPlayerCommand", arg);
            object commandCovalence = Interface.CallHook("OnUserCommand", iplayer, cmd, args);
            if (commandSpecific != null || commandCovalence != null)
            {
                return;
            }

            // Is it a valid chat command?
            if (!Covalence.CommandSystem.HandleChatMessage(iplayer, str) && !cmdlib.HandleChatCommand(player, cmd, args))
            {
                if (Interface.Oxide.Config.Options.Modded)
                {
                    iplayer.Reply(string.Format(lang.GetMessage("UnknownCommand", this, iplayer.Id), cmd));
                }
            }
        }

        /// <summary>
        /// Called when the player is authenticating
        /// </summary>
        /// <param name="connection"></param>
        private void OnClientAuth(Connection connection)
        {
            connection.username = Regex.Replace(connection.username, @"<[^>]*>", string.Empty);
        }

        /// <summary>
        /// Called when the player has disconnected
        /// </summary>
        /// <param name="player"></param>
        /// <param name="reason"></param>
        [HookMethod("OnPlayerDisconnected")]
        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            IPlayer iplayer = player.IPlayer;
            if (iplayer != null)
            {
                Interface.CallHook("OnUserDisconnected", iplayer, reason);
            }

            Covalence.PlayerManager.PlayerDisconnected(player);
        }

        /// <summary>
        /// Called when the player has connected
        /// </summary>
        /// <param name="player"></param>
        [HookMethod("OnPlayerInit")]
        private void OnPlayerInit(BasePlayer player)
        {
            // Set language for player
            lang.SetLanguage(player.net.connection.info.GetString("global.language", "en"), player.UserIDString);

            // Let covalence know
            Covalence.PlayerManager.PlayerConnected(player);
            IPlayer iplayer = Covalence.PlayerManager.FindPlayerById(player.UserIDString);
            if (iplayer != null)
            {
                player.IPlayer = iplayer;
                Interface.CallHook("OnUserConnected", iplayer);
            }
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
            }
        }

        /// <summary>
        /// Called when the player has been kicked
        /// </summary>
        /// <param name="player"></param>
        /// <param name="reason"></param>
        [HookMethod("OnPlayerKicked")]
        private void OnPlayerKicked(BasePlayer player, string reason)
        {
            IPlayer iplayer = player.IPlayer;
            if (iplayer != null)
            {
                Interface.CallHook("OnUserKicked", player.IPlayer, reason);
            }
        }

        /// <summary>
        /// Called when the player is respawning
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        [HookMethod("OnPlayerRespawn")]
        private object OnPlayerRespawn(BasePlayer player)
        {
            IPlayer iplayer = player.IPlayer;
            return iplayer != null ? Interface.CallHook("OnUserRespawn", iplayer) : null;
        }

        /// <summary>
        /// Called when the player has respawned
        /// </summary>
        /// <param name="player"></param>
        [HookMethod("OnPlayerRespawned")]
        private void OnPlayerRespawned(BasePlayer player)
        {
            IPlayer iplayer = player.IPlayer;
            if (iplayer != null)
            {
                Interface.CallHook("OnUserRespawned", iplayer);
            }
        }

        #endregion Player Hooks

        #region Entity Hooks

        /// <summary>
        /// Called when a BaseCombatEntity takes damage
        /// This is used to call OnEntityTakeDamage for anything other than a BasePlayer
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="info"></param>
        /// <returns></returns>
        [HookMethod("IOnBaseCombatEntityHurt")]
        private object IOnBaseCombatEntityHurt(BaseCombatEntity entity, HitInfo info)
        {
            return entity is BasePlayer ? null : Interface.CallHook("OnEntityTakeDamage", entity, info);
        }

        private int GetPlayersSensed(NPCPlayerApex npc, Vector3 position, float distance, BaseEntity[] targetList)
        {
            return BaseEntity.Query.Server.GetInSphere(position, distance, targetList,
                entity =>
                {
                    BasePlayer player = entity as BasePlayer;
                    object callHook = player != null && npc != null && player != npc ? Interface.CallHook("OnNpcPlayerTarget", npc, player) : null;
                    if (callHook != null)
                    {
                        foreach (Memory.SeenInfo seenInfo in npc.AiContext.Memory.All)
                        {
                            if (seenInfo.Entity == player)
                            {
                                npc.AiContext.Memory.All.Remove(seenInfo);
                                break;
                            }
                        }

                        foreach (Memory.ExtendedInfo extendedInfo in npc.AiContext.Memory.AllExtended)
                        {
                            if (extendedInfo.Entity == player)
                            {
                                npc.AiContext.Memory.AllExtended.Remove(extendedInfo);
                                break;
                            }
                        }
                    }

                    return player != null && callHook == null && player.isServer && !player.IsSleeping() && !player.IsDead() && player.Family != npc.Family;
                });
        }

        /// <summary>
        /// Called when an Apex NPC player tries to target an entity based on closeness
        /// </summary>
        /// <param name="npc"></param>
        /// <returns></returns>
        [HookMethod("IOnNpcPlayerSenseClose")]
        private object IOnNpcPlayerSenseClose(NPCPlayerApex npc)
        {
            NPCPlayerApex.EntityQueryResultCount = GetPlayersSensed(npc, npc.ServerPosition, npc.Stats.CloseRange, NPCPlayerApex.EntityQueryResults);
            return true;
        }

        /// <summary>
        /// Called when an Apex NPC player tries to target an entity based on vision
        /// </summary>
        /// <param name="npc"></param>
        /// <returns></returns>
        [HookMethod("IOnNpcPlayerSenseVision")]
        private object IOnNpcPlayerSenseVision(NPCPlayerApex npc)
        {
            NPCPlayerApex.PlayerQueryResultCount = GetPlayersSensed(npc, npc.ServerPosition, npc.Stats.VisionRange, NPCPlayerApex.PlayerQueryResults);
            return true;
        }

        /// <summary>
        /// Called when a Murderer NPC player tries to target an entity
        /// </summary>
        /// <param name="npc"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        [HookMethod("IOnNpcPlayerTarget")]
        private object IOnNpcPlayerTarget(NPCPlayerApex npc, BaseEntity target)
        {
            if (Interface.CallHook("OnNpcPlayerTarget", npc, target) != null)
            {
                return 0f;
            }

            return null;
        }

        /// <summary>
        /// Called when an HTN NPC player tries to target an entity
        /// </summary>
        /// <param name="npc"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        [HookMethod("IOnHtnNpcPlayerTarget")]
        private object IOnHtnNpcPlayerTarget(IHTNAgent npc, BasePlayer target)
        {
            if (npc != null && Interface.CallHook("OnNpcPlayerTarget", npc.Body, target) != null)
            {
                npc.AiDomain.NpcContext.BaseMemory.Forget(0f);
                npc.AiDomain.NpcContext.BaseMemory.PrimaryKnownEnemyPlayer.PlayerInfo.Player = null;
                return true;
            }

            return null;
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
            object callHook = Interface.CallHook("OnNpcTarget", npc, target);
            if (callHook != null)
            {
                npc.SetFact(BaseNpc.Facts.HasEnemy, 0);
                npc.SetFact(BaseNpc.Facts.EnemyRange, 3);
                npc.SetFact(BaseNpc.Facts.AfraidRange, 1);
                return 0f;
            }

            return null;
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

        #region Deprecated Hooks

        [HookMethod("IOnActiveItemChange")]
        private object IOnActiveItemChange(BasePlayer player, Item oldItem, uint newItemId)
        {
            object newHook = Interface.Oxide.CallHook("OnActiveItemChange", player, oldItem, newItemId);
            object oldHook = Interface.Oxide.CallDeprecatedHook("OnActiveItemChange", $"OnActiveItemChange(BasePlayer player, Item oldItem, uint newItemId)",
                new System.DateTime(2020, 1, 1), player, newItemId);
            return newHook ?? oldHook;
        }

        [HookMethod("IOnActiveItemChanged")]
        private void IOnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            Interface.Oxide.CallHook("OnActiveItemChanged", player, oldItem, newItem);
            Interface.Oxide.CallDeprecatedHook("OnPlayerActiveItemChanged", $"OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)",
                new System.DateTime(2020, 1, 1), player, oldItem, newItem);
        }

        #endregion Deprecated Hooks
    }
}
