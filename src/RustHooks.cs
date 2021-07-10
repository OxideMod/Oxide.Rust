using ConVar;
using Network;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Core.RemoteConsole;
using Oxide.Game.Rust.Libraries.Covalence;
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

        private int GetPlayersSensed(NPCPlayerApex npc, Vector3 position, float distance, BaseEntity[] targetList)
        {
            return BaseEntity.Query.Server.GetInSphere(position, distance, targetList,
                entity =>
                {
                    BasePlayer target = entity as BasePlayer;
                    object callHook = target != null && npc != null && target != npc ? Interface.CallHook("OnNpcTarget", npc, target) : null;
                    if (callHook != null)
                    {
                        foreach (Memory.SeenInfo seenInfo in npc.AiContext.Memory.All)
                        {
                            if (seenInfo.Entity == target)
                            {
                                npc.AiContext.Memory.All.Remove(seenInfo);
                                break;
                            }
                        }

                        foreach (Memory.ExtendedInfo extendedInfo in npc.AiContext.Memory.AllExtended)
                        {
                            if (extendedInfo.Entity == target)
                            {
                                npc.AiContext.Memory.AllExtended.Remove(extendedInfo);
                                break;
                            }
                        }
                    }

                    return target != null && callHook == null && target.isServer && !target.IsSleeping() && !target.IsDead() && target.Family != npc.Family;
                });
        }

        /// <summary>
        /// Called when an Apex NPC player tries to target an entity based on closeness
        /// </summary>
        /// <param name="npc"></param>
        /// <returns></returns>
        [HookMethod("IOnNpcSenseClose")]
        private object IOnNpcSenseClose(NPCPlayerApex npc)
        {
            NPCPlayerApex.EntityQueryResultCount = GetPlayersSensed(npc, npc.ServerPosition, npc.Stats.CloseRange, NPCPlayerApex.EntityQueryResults);
            return true;
        }

        /// <summary>
        /// Called when an Apex NPC player tries to target an entity based on vision
        /// </summary>
        /// <param name="npc"></param>
        /// <returns></returns>
        [HookMethod("IOnNpcSenseVision")]
        private object IOnNpcSenseVision(NPCPlayerApex npc)
        {
            NPCPlayerApex.PlayerQueryResultCount = GetPlayersSensed(npc, npc.ServerPosition, npc.Stats.VisionRange, NPCPlayerApex.PlayerQueryResults);
            return true;
        }

        /// <summary>
        /// Called when an Apex NPC player (i.e. murderer) tries to target an entity
        /// </summary>
        /// <param name="npc"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        [HookMethod("IOnNpcTarget")]
        private object IOnNpcTarget(NPCPlayerApex npc, BaseEntity target)
        {
            if (Interface.CallHook("OnNpcTarget", npc, target) != null)
            {
                return 0f;
            }

            return null;
        }

        /// <summary>
        /// Called when an HTN NPC player (old scientist) tries to target an entity
        /// </summary>
        /// <param name="npc"></param>
        /// <param name="target"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        [HookMethod("IOnNpcTarget")]
        private object IOnNpcTarget(IHTNAgent npc, BasePlayer target, int index)
        {
            if (npc != null && Interface.CallHook("OnNpcTarget", npc.Body, target) != null)
            {
                npc.AiDomain.NpcContext.PlayersInRange.RemoveAt(index);
                npc.AiDomain.NpcContext.BaseMemory.Forget(0f); // Unsure if still needed
                npc.AiDomain.NpcContext.BaseMemory.PrimaryKnownEnemyPlayer.PlayerInfo.Player = null; // Unsure if still needed
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
            if (Interface.CallHook("OnNpcTarget", npc, target) != null)
            {
                npc.SetFact(BaseNpc.Facts.HasEnemy, 0);
                npc.SetFact(BaseNpc.Facts.EnemyRange, 3);
                npc.SetFact(BaseNpc.Facts.AfraidRange, 1);
                npc.AiContext.EnemyPlayer = null;
                npc.AiContext.LastEnemyPlayerScore = 0f;
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
        [HookMethod("IOnServerUsersSet")]
        private void IOnServerUsersSet(ulong steamId, ServerUsers.UserGroup group, string playerName, string reason, long expiry)
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
        [HookMethod("IOnServerUsersRemove")]
        private void IOnServerUsersRemove(ulong steamId)
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
                if (authLevel == 2 && !permission.UserHasGroup(connectionId, defaultGroups.Administrators))
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
        [HookMethod("IOnPlayerCommand")]
        private void IOnPlayerCommand(BasePlayer basePlayer, string message)
        {
            // Check if using Rust+ app
            if (basePlayer == null || !basePlayer.IsConnected)
            {
                return;
            }

            string str = message.Replace("\n", "").Replace("\r", "").Trim();

            // Check if it is a chat command
            if (string.IsNullOrEmpty(str) || str[0] != '/' || str.Length <= 1)
            {
                return;
            }

            // Parse the command
            ParseCommand(str.TrimStart('/'), out string cmd, out string[] args);
            if (cmd == null)
            {
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

            // Is it a valid chat command?
            if (!Covalence.CommandSystem.HandleChatMessage(basePlayer.IPlayer, str) && !cmdlib.HandleChatCommand(basePlayer, cmd, args))
            {
                if (Interface.Oxide.Config.Options.Modded)
                {
                    basePlayer.IPlayer.Reply(string.Format(lang.GetMessage("UnknownCommand", this, basePlayer.IPlayer.Id), cmd));
                }
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
        [HookMethod("IOnRconCommand")]
        private object IOnRconCommand(IPAddress ipAddress, string command)
        {
            if (ipAddress != null && !string.IsNullOrEmpty(command))
            {
                RemoteMessage message = RemoteMessage.GetMessage(command);

                if (string.IsNullOrEmpty(message?.Message))
                {
                    return null;
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
            if (arg.cmd.FullName == "chat.say" || arg.cmd.FullName == "chat.teamsay")
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

        #region Deprecated Hooks

        [HookMethod("OnExperimentStart")]
        private object OnExperimentStart(Workbench workbench, BasePlayer player)
        {
            return Interface.Oxide.CallDeprecatedHook("CanExperiment", "OnExperimentStart(Workbench workbench, BasePlayer player)",
                new System.DateTime(2021, 12, 31), player, workbench);
        }

        [HookMethod("OnPlayerCorpseSpawned")]
        private object OnPlayerCorpseSpawned(BasePlayer player, BaseCorpse corpse)
        {
            return Interface.Oxide.CallDeprecatedHook("OnPlayerCorpse", "OnPlayerCorpseSpawned(BasePlayer player, BaseCorpse corpse)",
                new System.DateTime(2021, 12, 31), player, corpse);
        }

        [HookMethod("OnVehiclePush")]
        private object OnVehiclePush(BaseVehicle vehicle, BasePlayer player)
        {
            if (vehicle is MotorRowboat)
            {
                return Interface.Oxide.CallDeprecatedHook("CanPushBoat", "CanPushVehicle(BaseVehicle vehicle, BasePlayer player)",
                new System.DateTime(2021, 12, 31), player, vehicle as MotorRowboat);
            }

            return null;
        }

        [HookMethod("OnFuelConsume")]
        private void OnFuelConsume(BaseOven oven, Item fuel, ItemModBurnable burnable)
        {
            Interface.Oxide.CallDeprecatedHook("OnConsumeFuel", "OnFuelConsume(BaseOven oven, Item fuel, ItemModBurnable burnable)",
                new System.DateTime(2021, 12, 31), oven, fuel, burnable);
        }

        [HookMethod("OnEntitySaved")]
        private void OnEntitySaved(Elevator elevator, BaseNetworkable.SaveInfo saveInfo)
        {
            Interface.Oxide.CallDeprecatedHook("OnElevatorSaved", "OnEntitySaved(Elevator elevator, BaseNetworkable.SaveInfo saveInfo)",
                new System.DateTime(2021, 12, 31), elevator, saveInfo);
        }

        [HookMethod("OnResearchCostDetermine")]
        private object OnResearchCostDetermine(Item item, ResearchTable table)
        {
            return Interface.Oxide.CallDeprecatedHook("OnItemScrap", "OnResearchCostDetermine(Item item, ResearchTable table)",
                new System.DateTime(2021, 12, 31), table, item);
        }

        #endregion Deprecated Hooks
    }
}
