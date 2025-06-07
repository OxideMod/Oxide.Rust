using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ConVar;

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

            string languageName = args[0];
            try
            {
                languageName = new CultureInfo(languageName)?.TwoLetterISOLanguageName;
            }
            catch (CultureNotFoundException)
            {
                player.Reply(lang.GetMessage("InvalidLanguageName", this, player.Id), languageName);
                return;
            }

            if (player.IsServer)
            {
                lang.SetServerLanguage(languageName);
                player.Reply(string.Format(lang.GetMessage("ServerLanguage", this, player.Id), lang.GetServerLanguage()));
            }
            else
            {
                lang.SetLanguage(languageName, player.Id);
                player.Reply(string.Format(lang.GetMessage("PlayerLanguage", this, player.Id), languageName));
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
        private void PluginsCommand(IPlayer player)
        {
            var plugins = pluginManager.GetPlugins();

            var output = Facepunch.Pool.Get<StringBuilder>();

            output.Append("Listing ") // Plugin count appended later on
                .Append(" plugins:"); // TODO: Localization

            int pluginCount = 0;

            foreach (var plugin in plugins)
            {
                if (plugin.Filename == null || plugin.IsCorePlugin) continue;

                output.AppendLine().Append("  ").Append(pluginCount++.ToString("00")).Append(" \"").Append(plugin.Title)
                    .Append("\"").Append(" (").Append(plugin.Version).Append(") by ").Append(plugin.Author).Append(" (")
                    .Append(plugin.TotalHookTime.ToString("0.00")).Append("s) - ").Append("(")
                    .Append(FormatBytes(plugin.TotalHookMemory)).Append(" - ").Append(plugin.Filename.Basename());
            }

            if (pluginCount == 0)
            {
                player.Reply(lang.GetMessage("NoPluginsFound", this, player.Id));
                Facepunch.Pool.FreeUnmanaged(ref output);
                return;
            }

            foreach (var loader in Interface.Oxide.GetPluginLoaders())
            {
                var unloadedNames = loader.ScanDirectory(Interface.Oxide.PluginDirectory)
                    .Except(plugins.Select(pl => pl.Name));

                foreach (var name in unloadedNames)
                {
                    output.AppendLine().Append("  ").Append(pluginCount++.ToString("00")).Append(" ").Append(name)
                        .Append(" - ").Append(loader.PluginErrors.TryGetValue(name, out var msg) ? msg : "Unloaded");
                }
            }

            output.Insert(8, pluginCount - 1);

            player.Reply(output.ToString());

            Facepunch.Pool.FreeUnmanaged(ref output);
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes:0} B";
            if (bytes < 1048576) return $"{bytes / 1024:0} KB";
            if (bytes < 1073741824) return $"{bytes / 1048576:0} MB";
            return $"{bytes / 1073741824:0} GB";
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
        private void VersionCommand(IPlayer player)
        {
            if (player.IsServer)
            {
                string format = "Oxide.Rust Version: {0}\nOxide.Rust Branch: {1}";
                player.Reply(string.Format(format, RustExtension.AssemblyVersion, Extension.Branch));
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
        private void SaveCommand(IPlayer player)
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
