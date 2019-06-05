using Facepunch;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Game.Rust
{
    /// <summary>
    /// Game commands for the core Rust plugin
    /// </summary>
    public partial class RustCore
    {
        #region Grant Command

        /// <summary>
        /// Called when the "grant" command has been executed
        /// </summary>
        /// <param name="player"></param>
        /// <param name="command"></param>
        /// <param name="args"></param>
        [HookMethod("GrantCommand")]
        private void GrantCommand(IPlayer player, string command, string[] args)
        {
            if (!PermissionsLoaded(player))
            {
                return;
            }

            if (args.Length < 3)
            {
                player.Reply(lang.GetMessage("CommandUsageGrant", this, player.Id));
                return;
            }

            string mode = args[0];
            string name = args[1].Sanitize();
            string perm = args[2];

            if (!permission.PermissionExists(perm))
            {
                player.Reply(string.Format(lang.GetMessage("PermissionNotFound", this, player.Id), perm));
                return;
            }

            if (mode.Equals("group"))
            {
                if (!permission.GroupExists(name))
                {
                    player.Reply(string.Format(lang.GetMessage("GroupNotFound", this, player.Id), name));
                    return;
                }

                if (permission.GroupHasPermission(name, perm))
                {
                    player.Reply(string.Format(lang.GetMessage("GroupAlreadyHasPermission", this, player.Id), name, perm));
                    return;
                }

                permission.GrantGroupPermission(name, perm, null);
                player.Reply(string.Format(lang.GetMessage("GroupPermissionGranted", this, player.Id), name, perm));
            }
            else if (mode.Equals("user"))
            {
                IPlayer[] foundPlayers = Covalence.PlayerManager.FindPlayers(name).ToArray();
                if (foundPlayers.Length > 1)
                {
                    player.Reply(string.Format(lang.GetMessage("PlayersFound", this, player.Id), string.Join(", ", foundPlayers.Select(p => p.Name).ToArray())));
                    return;
                }

                IPlayer target = foundPlayers.Length == 1 ? foundPlayers[0] : null;
                if (target == null && !permission.UserIdValid(name))
                {
                    player.Reply(string.Format(lang.GetMessage("PlayerNotFound", this, player.Id), name));
                    return;
                }

                string userId = name;
                if (target != null)
                {
                    userId = target.Id;
                    name = target.Name;
                    permission.UpdateNickname(userId, name);
                }

                if (permission.UserHasPermission(name, perm))
                {
                    player.Reply(string.Format(lang.GetMessage("PlayerAlreadyHasPermission", this, player.Id), userId, perm));
                    return;
                }

                permission.GrantUserPermission(userId, perm, null);
                player.Reply(string.Format(lang.GetMessage("PlayerPermissionGranted", this, player.Id), $"{name} ({userId})", perm));
            }
            else
            {
                player.Reply(lang.GetMessage("CommandUsageGrant", this, player.Id));
            }
        }

        #endregion Grant Command

        // TODO: GrantAllCommand (grant all permissions from user(s)/group(s))

        #region Group Command

        /// <summary>
        /// Called when the "group" command has been executed
        /// </summary>
        /// <param name="player"></param>
        /// <param name="command"></param>
        /// <param name="args"></param>
        [HookMethod("GroupCommand")]
        private void GroupCommand(IPlayer player, string command, string[] args)
        {
            if (!PermissionsLoaded(player))
            {
                return;
            }

            if (args.Length < 2)
            {
                player.Reply(lang.GetMessage("CommandUsageGroup", this, player.Id));
                player.Reply(lang.GetMessage("CommandUsageGroupParent", this, player.Id));
                player.Reply(lang.GetMessage("CommandUsageGroupRemove", this, player.Id));
                return;
            }

            string mode = args[0];
            string group = args[1];
            string title = args.Length >= 3 ? args[2] : "";
            int rank = args.Length == 4 ? int.Parse(args[3]) : 0;

            if (mode.Equals("add"))
            {
                if (permission.GroupExists(group))
                {
                    player.Reply(string.Format(lang.GetMessage("GroupAlreadyExists", this, player.Id), group));
                    return;
                }

                permission.CreateGroup(group, title, rank);
                player.Reply(string.Format(lang.GetMessage("GroupCreated", this, player.Id), group));
            }
            else if (mode.Equals("remove"))
            {
                if (!permission.GroupExists(group))
                {
                    player.Reply(string.Format(lang.GetMessage("GroupNotFound", this, player.Id), group));
                    return;
                }

                permission.RemoveGroup(group);
                player.Reply(string.Format(lang.GetMessage("GroupDeleted", this, player.Id), group));
            }
            else if (mode.Equals("set"))
            {
                if (!permission.GroupExists(group))
                {
                    player.Reply(string.Format(lang.GetMessage("GroupNotFound", this, player.Id), group));
                    return;
                }

                permission.SetGroupTitle(group, title);
                permission.SetGroupRank(group, rank);
                player.Reply(string.Format(lang.GetMessage("GroupChanged", this, player.Id), group));
            }
            else if (mode.Equals("parent"))
            {
                if (args.Length <= 2)
                {
                    player.Reply(lang.GetMessage("CommandUsageGroupParent", this, player.Id));
                    return;
                }

                if (!permission.GroupExists(group))
                {
                    player.Reply(string.Format(lang.GetMessage("GroupNotFound", this, player.Id), group));
                    return;
                }

                string parent = args[2];
                if (!string.IsNullOrEmpty(parent) && !permission.GroupExists(parent))
                {
                    player.Reply(string.Format(lang.GetMessage("GroupParentNotFound", this, player.Id), parent));
                    return;
                }

                if (permission.SetGroupParent(group, parent))
                {
                    player.Reply(string.Format(lang.GetMessage("GroupParentChanged", this, player.Id), group, parent));
                }
                else
                {
                    player.Reply(string.Format(lang.GetMessage("GroupParentNotChanged", this, player.Id), group));
                }
            }
            else
            {
                player.Reply(lang.GetMessage("CommandUsageGroup", this, player.Id));
                player.Reply(lang.GetMessage("CommandUsageGroupParent", this, player.Id));
                player.Reply(lang.GetMessage("CommandUsageGroupRemove", this, player.Id));
            }
        }

        #endregion Group Command

        #region Lang Command

        /// <summary>
        /// Called when the "lang" command has been executed
        /// </summary>
        /// <param name="player"></param>
        /// <param name="command"></param>
        /// <param name="args"></param>
        [HookMethod("LangCommand")]
        private void LangCommand(IPlayer player, string command, string[] args)
        {
            if (args.Length < 1)
            {
                player.Reply(lang.GetMessage("CommandUsageLang", this, player.Id));
                return;
            }

            if (player.IsServer)
            {
                // TODO: Check if language exists before setting, warn if not
                lang.SetServerLanguage(args[0]);
                player.Reply(string.Format(lang.GetMessage("ServerLanguage", this, player.Id), lang.GetServerLanguage()));
            }
            else
            {
                // TODO: Check if language exists before setting, warn if not
                string[] languages = lang.GetLanguages();
                if (languages.Contains(args[0]))
                {
                    lang.SetLanguage(args[0], player.Id);
                }

                player.Reply(string.Format(lang.GetMessage("PlayerLanguage", this, player.Id), args[0]));
            }
        }

        #endregion Lang Command

        #region Load Command

        /// <summary>
        /// Called when the "load" command has been executed
        /// </summary>
        /// <param name="player"></param>
        /// <param name="command"></param>
        /// <param name="args"></param>
        [HookMethod("LoadCommand")]
        private void LoadCommand(IPlayer player, string command, string[] args)
        {
            if (args.Length < 1)
            {
                player.Reply(lang.GetMessage("CommandUsageLoad", this, player.Id));
                return;
            }

            if (args[0].Equals("*") || args[0].Equals("all"))
            {
                Interface.Oxide.LoadAllPlugins();
                return;
            }

            foreach (string name in args)
            {
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                Interface.Oxide.LoadPlugin(name);
                pluginManager.GetPlugin(name);
            }
        }

        #endregion Load Command

        #region Plugins Command

        /// <summary>
        /// Called when the "plugins" command has been executed
        /// </summary>
        /// <param name="player"></param>
        /// <param name="command"></param>
        /// <param name="args"></param>
        [HookMethod("PluginsCommand")]
        private void PluginsCommand(IPlayer player, string command, string[] args)
        {
            Plugin[] loadedPlugins = pluginManager.GetPlugins().Where(pl => !pl.IsCorePlugin).ToArray();
            HashSet<string> loadedPluginNames = new HashSet<string>(loadedPlugins.Select(pl => pl.Name));
            Dictionary<string, string> unloadedPluginErrors = new Dictionary<string, string>();
            foreach (PluginLoader loader in Interface.Oxide.GetPluginLoaders())
            {
                foreach (string name in loader.ScanDirectory(Interface.Oxide.PluginDirectory).Except(loadedPluginNames))
                {
                    string msg;
                    unloadedPluginErrors[name] = loader.PluginErrors.TryGetValue(name, out msg) ? msg : "Unloaded"; // TODO: Localization
                }
            }

            int totalPluginCount = loadedPlugins.Length + unloadedPluginErrors.Count;
            if (totalPluginCount < 1)
            {
                player.Reply(lang.GetMessage("NoPluginsFound", this, player.Id));
                return;
            }

            string output = $"Listing {loadedPlugins.Length + unloadedPluginErrors.Count} plugins:"; // TODO: Localization
            int number = 1;
            foreach (Plugin plugin in loadedPlugins.Where(p => p.Filename != null))
            {
                output += $"\n  {number++:00} \"{plugin.Title}\" ({plugin.Version}) by {plugin.Author} ({plugin.TotalHookTime:0.00}s) - {plugin.Filename.Basename()}";
            }

            foreach (string pluginName in unloadedPluginErrors.Keys)
            {
                output += $"\n  {number++:00} {pluginName} - {unloadedPluginErrors[pluginName]}";
            }

            player.Reply(output);
        }

        #endregion Plugins Command

        #region Reload Command

        /// <summary>
        /// Called when the "reload" command has been executed
        /// </summary>
        /// <param name="player"></param>
        /// <param name="command"></param>
        /// <param name="args"></param>
        [HookMethod("ReloadCommand")]
        private void ReloadCommand(IPlayer player, string command, string[] args)
        {
            if (args.Length < 1)
            {
                player.Reply(lang.GetMessage("CommandUsageReload", this, player.Id));
                return;
            }

            if (args[0].Equals("*") || args[0].Equals("all"))
            {
                Interface.Oxide.ReloadAllPlugins();
                return;
            }

            foreach (string name in args)
            {
                if (!string.IsNullOrEmpty(name))
                {
                    Interface.Oxide.ReloadPlugin(name);
                }
            }
        }

        #endregion Reload Command

        #region Revoke Command

        /// <summary>
        /// Called when the "revoke" command has been executed
        /// </summary>
        /// <param name="player"></param>
        /// <param name="command"></param>
        /// <param name="args"></param>
        [HookMethod("RevokeCommand")]
        private void RevokeCommand(IPlayer player, string command, string[] args)
        {
            if (!PermissionsLoaded(player))
            {
                return;
            }

            if (args.Length < 3)
            {
                player.Reply(lang.GetMessage("CommandUsageRevoke", this, player.Id));
                return;
            }

            string mode = args[0];
            string name = args[1].Sanitize();
            string perm = args[2];

            if (mode.Equals("group"))
            {
                if (!permission.GroupExists(name))
                {
                    player.Reply(string.Format(lang.GetMessage("GroupNotFound", this, player.Id), name));
                    return;
                }

                if (!permission.GroupHasPermission(name, perm))
                {
                    // TODO: Check if group is inheriting permission, mention
                    player.Reply(string.Format(lang.GetMessage("GroupDoesNotHavePermission", this, player.Id), name, perm));
                    return;
                }

                permission.RevokeGroupPermission(name, perm);
                player.Reply(string.Format(lang.GetMessage("GroupPermissionRevoked", this, player.Id), name, perm));
            }
            else if (mode.Equals("user"))
            {
                IPlayer[] foundPlayers = Covalence.PlayerManager.FindPlayers(name).ToArray();
                if (foundPlayers.Length > 1)
                {
                    player.Reply(string.Format(lang.GetMessage("PlayersFound", this, player.Id), string.Join(", ", foundPlayers.Select(p => p.Name).ToArray())));
                    return;
                }

                IPlayer target = foundPlayers.Length == 1 ? foundPlayers[0] : null;
                if (target == null && !permission.UserIdValid(name))
                {
                    player.Reply(string.Format(lang.GetMessage("PlayerNotFound", this, player.Id), name));
                    return;
                }

                string userId = name;
                if (target != null)
                {
                    userId = target.Id;
                    name = target.Name;
                    permission.UpdateNickname(userId, name);
                }

                if (!permission.UserHasPermission(userId, perm))
                {
                    // TODO: Check if user is inheriting permission, mention
                    player.Reply(string.Format(lang.GetMessage("PlayerDoesNotHavePermission", this, player.Id), name, perm));
                    return;
                }

                permission.RevokeUserPermission(userId, perm);
                player.Reply(string.Format(lang.GetMessage("PlayerPermissionRevoked", this, player.Id), $"{name} ({userId})", perm));
            }
            else
            {
                player.Reply(lang.GetMessage("CommandUsageRevoke", this, player.Id));
            }
        }

        #endregion Revoke Command

        // TODO: RevokeAllCommand (revoke all permissions from user(s)/group(s))

        #region Show Command

        /// <summary>
        /// Called when the "show" command has been executed
        /// </summary>
        /// <param name="player"></param>
        /// <param name="command"></param>
        /// <param name="args"></param>
        [HookMethod("ShowCommand")]
        private void ShowCommand(IPlayer player, string command, string[] args)
        {
            if (!PermissionsLoaded(player))
            {
                return;
            }

            if (args.Length < 1)
            {
                player.Reply(lang.GetMessage("CommandUsageShow", this, player.Id));
                player.Reply(lang.GetMessage("CommandUsageShowName", this, player.Id));
                return;
            }

            string mode = args[0];
            string name = args.Length == 2 ? args[1].Sanitize() : string.Empty;

            if (mode.Equals("perms"))
            {
                player.Reply(string.Format(lang.GetMessage("Permissions", this, player.Id) + ":\n" + string.Join(", ", permission.GetPermissions())));
            }
            else if (mode.Equals("perm"))
            {
                if (args.Length < 2 || string.IsNullOrEmpty(name))
                {
                    player.Reply(lang.GetMessage("CommandUsageShow", this, player.Id));
                    player.Reply(lang.GetMessage("CommandUsageShowName", this, player.Id));
                    return;
                }

                string[] users = permission.GetPermissionUsers(name);
                string[] groups = permission.GetPermissionGroups(name);
                string result = $"{string.Format(lang.GetMessage("PermissionPlayers", this, player.Id), name)}:\n";
                result += users.Length > 0 ? string.Join(", ", users) : lang.GetMessage("NoPermissionPlayers", this, player.Id);
                result += $"\n\n{string.Format(lang.GetMessage("PermissionGroups", this, player.Id), name)}:\n";
                result += groups.Length > 0 ? string.Join(", ", groups) : lang.GetMessage("NoPermissionGroups", this, player.Id);
                player.Reply(result);
            }
            else if (mode.Equals("user"))
            {
                if (args.Length < 2 || string.IsNullOrEmpty(name))
                {
                    player.Reply(lang.GetMessage("CommandUsageShow", this, player.Id));
                    player.Reply(lang.GetMessage("CommandUsageShowName", this, player.Id));
                    return;
                }

                IPlayer[] foundPlayers = Covalence.PlayerManager.FindPlayers(name).ToArray();
                if (foundPlayers.Length > 1)
                {
                    player.Reply(string.Format(lang.GetMessage("PlayersFound", this, player.Id), string.Join(", ", foundPlayers.Select(p => p.Name).ToArray())));
                    return;
                }

                IPlayer target = foundPlayers.Length == 1 ? foundPlayers[0] : null;
                if (target == null && !permission.UserIdValid(name))
                {
                    player.Reply(string.Format(lang.GetMessage("PlayerNotFound", this, player.Id), name));
                    return;
                }

                string userId = name;
                if (target != null)
                {
                    userId = target.Id;
                    name = target.Name;
                    permission.UpdateNickname(userId, name);
                    name += $" ({userId})";
                }

                string[] perms = permission.GetUserPermissions(userId);
                string[] groups = permission.GetUserGroups(userId);
                string result = $"{string.Format(lang.GetMessage("PlayerPermissions", this, player.Id), name)}:\n";
                result += perms.Length > 0 ? string.Join(", ", perms) : lang.GetMessage("NoPlayerPermissions", this, player.Id);
                result += $"\n\n{string.Format(lang.GetMessage("PlayerGroups", this, player.Id), name)}:\n";
                result += groups.Length > 0 ? string.Join(", ", groups) : lang.GetMessage("NoPlayerGroups", this, player.Id);
                player.Reply(result);
            }
            else if (mode.Equals("group"))
            {
                if (args.Length < 2 || string.IsNullOrEmpty(name))
                {
                    player.Reply(lang.GetMessage("CommandUsageShow", this, player.Id));
                    player.Reply(lang.GetMessage("CommandUsageShowName", this, player.Id));
                    return;
                }

                if (!permission.GroupExists(name))
                {
                    player.Reply(string.Format(lang.GetMessage("GroupNotFound", this, player.Id), name));
                    return;
                }

                string[] users = permission.GetUsersInGroup(name);
                string[] perms = permission.GetGroupPermissions(name);
                string result = $"{string.Format(lang.GetMessage("GroupPlayers", this, player.Id), name)}:\n";
                result += users.Length > 0 ? string.Join(", ", users) : lang.GetMessage("NoPlayersInGroup", this, player.Id);
                result += $"\n\n{string.Format(lang.GetMessage("GroupPermissions", this, player.Id), name)}:\n";
                result += perms.Length > 0 ? string.Join(", ", perms) : lang.GetMessage("NoGroupPermissions", this, player.Id);
                string parent = permission.GetGroupParent(name);
                while (permission.GroupExists(parent))
                {
                    result += $"\n{string.Format(lang.GetMessage("ParentGroupPermissions", this, player.Id), parent)}:\n";
                    result += string.Join(", ", permission.GetGroupPermissions(parent));
                    parent = permission.GetGroupParent(parent);
                }
                player.Reply(result);
            }
            else if (mode.Equals("groups"))
            {
                player.Reply(string.Format(lang.GetMessage("Groups", this, player.Id) + ":\n" + string.Join(", ", permission.GetGroups())));
            }
            else
            {
                player.Reply(lang.GetMessage("CommandUsageShow", this, player.Id));
                player.Reply(lang.GetMessage("CommandUsageShowName", this, player.Id));
            }
        }

        #endregion Show Command

        #region Unload Command

        /// <summary>
        /// Called when the "unload" command has been executed
        /// </summary>
        /// <param name="player"></param>
        /// <param name="command"></param>
        /// <param name="args"></param>
        [HookMethod("UnloadCommand")]
        private void UnloadCommand(IPlayer player, string command, string[] args)
        {
            if (args.Length < 1)
            {
                player.Reply(lang.GetMessage("CommandUsageUnload", this, player.Id));
                return;
            }

            if (args[0].Equals("*") || args[0].Equals("all"))
            {
                Interface.Oxide.UnloadAllPlugins();
                return;
            }

            foreach (string name in args)
            {
                if (!string.IsNullOrEmpty(name))
                {
                    Interface.Oxide.UnloadPlugin(name);
                }
            }
        }

        #endregion Unload Command

        #region User Group Command

        /// <summary>
        /// Called when the "usergroup" command has been executed
        /// </summary>
        /// <param name="player"></param>
        /// <param name="command"></param>
        /// <param name="args"></param>
        [HookMethod("UserGroupCommand")]
        private void UserGroupCommand(IPlayer player, string command, string[] args)
        {
            if (!PermissionsLoaded(player))
            {
                return;
            }

            if (args.Length < 3)
            {
                player.Reply(lang.GetMessage("CommandUsageUserGroup", this, player.Id));
                return;
            }

            string mode = args[0];
            string name = args[1].Sanitize();
            string group = args[2];

            IPlayer[] foundPlayers = Covalence.PlayerManager.FindPlayers(name).ToArray();
            if (foundPlayers.Length > 1)
            {
                player.Reply(string.Format(lang.GetMessage("PlayersFound", this, player.Id), string.Join(", ", foundPlayers.Select(p => p.Name).ToArray())));
                return;
            }

            IPlayer target = foundPlayers.Length == 1 ? foundPlayers[0] : null;
            if (target == null && !permission.UserIdValid(name))
            {
                player.Reply(string.Format(lang.GetMessage("PlayerNotFound", this, player.Id), name));
                return;
            }

            string userId = name;
            if (target != null)
            {
                userId = target.Id;
                name = target.Name;
                permission.UpdateNickname(userId, name);
                name += $"({userId})";
            }

            if (!permission.GroupExists(group))
            {
                player.Reply(string.Format(lang.GetMessage("GroupNotFound", this, player.Id), group));
                return;
            }

            if (mode.Equals("add"))
            {
                permission.AddUserGroup(userId, group);
                player.Reply(string.Format(lang.GetMessage("PlayerAddedToGroup", this, player.Id), name, group));
            }
            else if (mode.Equals("remove"))
            {
                permission.RemoveUserGroup(userId, group);
                player.Reply(string.Format(lang.GetMessage("PlayerRemovedFromGroup", this, player.Id), name, group));
            }
            else
            {
                player.Reply(lang.GetMessage("CommandUsageUserGroup", this, player.Id));
            }
        }

        #endregion User Group Command

        // TODO: UserGroupAllCommand (add/remove all users to/from group)

        #region Version Command

        /// <summary>
        /// Called when the "version" command has been executed
        /// </summary>
        /// <param name="player"></param>
        /// <param name="command"></param>
        /// <param name="args"></param>
        [HookMethod("VersionCommand")]
        private void VersionCommand(IPlayer player, string command, string[] args)
        {
            if (player.IsServer)
            {
                player.Reply("Oxide.Rust Version: " + RustExtension.AssemblyVersion);
            }
            else
            {
                string format = Covalence.FormatText(lang.GetMessage("Version", this, player.Id));
                player.Reply(string.Format(format, RustExtension.AssemblyVersion, Covalence.GameName, Server.Version, Server.Protocol));
            }
        }

        #endregion Version Command

        #region Save Command

        [HookMethod("SaveCommand")]
        private void SaveCommand(IPlayer player, string command, string[] args)
        {
            if (PermissionsLoaded(player) && player.IsAdmin)
            {
                Interface.Oxide.OnSave();
                Covalence.PlayerManager.SavePlayerData();
                player.Reply(lang.GetMessage("DataSaved", this, player.Id));
            }
        }

        #endregion Save Command
    }
}
