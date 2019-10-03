using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Libraries;
using Oxide.Game.Rust.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Oxide.Game.Rust
{
    /// <summary>
    /// The core Rust plugin
    /// </summary>
    public partial class RustCore : CSPlugin
    {
        #region Initialization

        /// <summary>
        /// Initializes a new instance of the RustCore class
        /// </summary>
        public RustCore()
        {
            // Set plugin info attributes
            Title = "Rust";
            Author = RustExtension.AssemblyAuthors;
            Version = RustExtension.AssemblyVersion;
        }

        // Libraries
        internal readonly Command cmdlib = Interface.Oxide.GetLibrary<Command>();
        internal readonly Lang lang = Interface.Oxide.GetLibrary<Lang>();
        internal readonly Permission permission = Interface.Oxide.GetLibrary<Permission>();
        internal readonly Player Player = Interface.Oxide.GetLibrary<Player>();

        // Instances
        internal static readonly RustCovalenceProvider Covalence = RustCovalenceProvider.Instance;
        internal readonly PluginManager pluginManager = Interface.Oxide.RootPluginManager;
        internal readonly IServer Server = Covalence.CreateServer();

        // Commands that a plugin can't override
        internal static IEnumerable<string> RestrictedCommands => new[]
        {
            "ownerid", "moderatorid", "removeowner", "removemoderator"
        };

        internal bool serverInitialized;

        /// <summary>
        /// Checks if the permission system has loaded, shows an error if it failed to load
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        private bool PermissionsLoaded(IPlayer player)
        {
            if (!permission.IsLoaded)
            {
                player.Reply(string.Format(lang.GetMessage("PermissionsNotLoaded", this, player.Id), permission.LastException.Message));
                return false;
            }

            return true;
        }

        #endregion Initialization

        #region Core Hooks

        /// <summary>
        /// Called when the plugin is initializing
        /// </summary>
        [HookMethod("Init")]
        private void Init()
        {
            // Configure remote error logging
            RemoteLogger.SetTag("game", Title.ToLower());
            RemoteLogger.SetTag("game version", Server.Version);

            // Add core plugin commands
            AddCovalenceCommand(new[] { "oxide.plugins", "o.plugins", "plugins" }, "PluginsCommand", "oxide.plugins");
            AddCovalenceCommand(new[] { "oxide.load", "o.load", "plugin.load" }, "LoadCommand", "oxide.load");
            AddCovalenceCommand(new[] { "oxide.reload", "o.reload", "plugin.reload" }, "ReloadCommand", "oxide.reload");
            AddCovalenceCommand(new[] { "oxide.unload", "o.unload", "plugin.unload" }, "UnloadCommand", "oxide.unload");

            // Add core permission commands
            AddCovalenceCommand(new[] { "oxide.grant", "o.grant", "perm.grant" }, "GrantCommand", "oxide.grant");
            AddCovalenceCommand(new[] { "oxide.group", "o.group", "perm.group" }, "GroupCommand", "oxide.group");
            AddCovalenceCommand(new[] { "oxide.revoke", "o.revoke", "perm.revoke" }, "RevokeCommand", "oxide.revoke");
            AddCovalenceCommand(new[] { "oxide.show", "o.show", "perm.show" }, "ShowCommand", "oxide.show");
            AddCovalenceCommand(new[] { "oxide.usergroup", "o.usergroup", "perm.usergroup" }, "UserGroupCommand", "oxide.usergroup");

            // Add core misc commands
            AddCovalenceCommand(new[] { "oxide.lang", "o.lang", "lang" }, "LangCommand");
            AddCovalenceCommand(new[] { "oxide.save", "o.save" }, "SaveCommand");
            AddCovalenceCommand(new[] { "oxide.version", "o.version" }, "VersionCommand");

            // Register messages for localization
            foreach (KeyValuePair<string, Dictionary<string, string>> language in Localization.languages)
            {
                lang.RegisterMessages(language.Value, this, language.Key);
            }

            // Setup default permission groups
            if (permission.IsLoaded)
            {
                int rank = 0;
                foreach (string defaultGroup in Interface.Oxide.Config.Options.DefaultGroups)
                {
                    if (!permission.GroupExists(defaultGroup))
                    {
                        permission.CreateGroup(defaultGroup, defaultGroup, rank++);
                    }
                }

                permission.RegisterValidate(s =>
                {
                    ulong temp;
                    if (!ulong.TryParse(s, out temp))
                    {
                        return false;
                    }

                    int digits = temp == 0 ? 1 : (int)Math.Floor(Math.Log10(temp) + 1);
                    return digits >= 17;
                });

                permission.CleanUp();
            }
        }

        /// <summary>
        /// Called when another plugin has been loaded
        /// </summary>
        /// <param name="plugin"></param>
        [HookMethod("OnPluginLoaded")]
        private void OnPluginLoaded(Plugin plugin)
        {
            if (serverInitialized)
            {
                // Call OnServerInitialized for hotloaded plugins
                plugin.CallHook("OnServerInitialized", false);
            }
        }

        /// <summary>
        /// Called when the server is first initialized
        /// </summary>
        [HookMethod("IOnServerInitialized")]
        private void IOnServerInitialized()
        {
            if (!serverInitialized)
            {
                Analytics.Collect();

                if (!Interface.Oxide.Config.Options.Modded)
                {
                    Interface.Oxide.LogWarning("The server is currently listed under Community. Please be aware that Facepunch only allows admin tools" +
                        " (that do not affect gameplay) under the Community section");
                }

                serverInitialized = true;

                // Let plugins know server startup is complete
                Interface.CallHook("OnServerInitialized", serverInitialized);
            }
        }

        /// <summary>
        /// Called when the server is saved
        /// </summary>
        [HookMethod("OnServerSave")]
        private void OnServerSave()
        {
            Interface.Oxide.OnSave();
            Covalence.PlayerManager.SavePlayerData();
        }

        /// <summary>
        /// Called when the server is shutting down
        /// </summary>
        [HookMethod("OnServerShutdown")]
        private void OnServerShutdown()
        {
            Interface.Oxide.OnShutdown();
            Covalence.PlayerManager.SavePlayerData();
        }

        #endregion Core Hooks

        #region Command Handling

        /// <summary>
        /// Parses the specified command
        /// </summary>
        /// <param name="argstr"></param>
        /// <param name="command"></param>
        /// <param name="args"></param>
        private void ParseCommand(string argstr, out string command, out string[] args)
        {
            List<string> arglist = new List<string>();
            StringBuilder sb = new StringBuilder();
            bool inlongarg = false;

            foreach (char c in argstr)
            {
                if (c == '"')
                {
                    if (inlongarg)
                    {
                        string arg = sb.ToString().Trim();
                        if (!string.IsNullOrEmpty(arg))
                        {
                            arglist.Add(arg);
                        }

                        sb.Clear();
                        inlongarg = false;
                    }
                    else
                    {
                        inlongarg = true;
                    }
                }
                else if (char.IsWhiteSpace(c) && !inlongarg)
                {
                    string arg = sb.ToString().Trim();
                    if (!string.IsNullOrEmpty(arg))
                    {
                        arglist.Add(arg);
                    }

                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }

            if (sb.Length > 0)
            {
                string arg = sb.ToString().Trim();
                if (!string.IsNullOrEmpty(arg))
                {
                    arglist.Add(arg);
                }
            }

            if (arglist.Count == 0)
            {
                command = null;
                args = null;
                return;
            }

            command = arglist[0];
            arglist.RemoveAt(0);
            args = arglist.ToArray();
        }

        #endregion Command Handling

        #region Helpers

        /// <summary>
        /// Returns the BasePlayer for the specified name, ID, or IP address string
        /// </summary>
        /// <param name="nameOrIdOrIp"></param>
        /// <returns></returns>
        public static BasePlayer FindPlayer(string nameOrIdOrIp)
        {
            BasePlayer player = null;
            foreach (BasePlayer activePlayer in BasePlayer.activePlayerList)
            {
                if (string.IsNullOrEmpty(activePlayer.UserIDString))
                {
                    continue;
                }

                if (activePlayer.UserIDString.Equals(nameOrIdOrIp))
                {
                    return activePlayer;
                }

                if (string.IsNullOrEmpty(activePlayer.displayName))
                {
                    continue;
                }

                if (activePlayer.displayName.Equals(nameOrIdOrIp, StringComparison.OrdinalIgnoreCase))
                {
                    return activePlayer;
                }

                if (activePlayer.displayName.Contains(nameOrIdOrIp, CompareOptions.OrdinalIgnoreCase))
                {
                    player = activePlayer;
                }

                if (activePlayer.net?.connection != null && activePlayer.net.connection.ipaddress.Equals(nameOrIdOrIp))
                {
                    return activePlayer;
                }
            }
            foreach (BasePlayer sleepingPlayer in BasePlayer.sleepingPlayerList)
            {
                if (string.IsNullOrEmpty(sleepingPlayer.UserIDString))
                {
                    continue;
                }

                if (sleepingPlayer.UserIDString.Equals(nameOrIdOrIp))
                {
                    return sleepingPlayer;
                }

                if (string.IsNullOrEmpty(sleepingPlayer.displayName))
                {
                    continue;
                }

                if (sleepingPlayer.displayName.Equals(nameOrIdOrIp, StringComparison.OrdinalIgnoreCase))
                {
                    return sleepingPlayer;
                }

                if (sleepingPlayer.displayName.Contains(nameOrIdOrIp, CompareOptions.OrdinalIgnoreCase))
                {
                    player = sleepingPlayer;
                }
            }
            return player;
        }

        /// <summary>
        /// Returns the BasePlayer for the specified name string
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static BasePlayer FindPlayerByName(string name)
        {
            BasePlayer player = null;
            foreach (BasePlayer activePlayer in BasePlayer.activePlayerList)
            {
                if (string.IsNullOrEmpty(activePlayer.displayName))
                {
                    continue;
                }

                if (activePlayer.displayName.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return activePlayer;
                }

                if (activePlayer.displayName.Contains(name, CompareOptions.OrdinalIgnoreCase))
                {
                    player = activePlayer;
                }
            }
            foreach (BasePlayer sleepingPlayer in BasePlayer.sleepingPlayerList)
            {
                if (string.IsNullOrEmpty(sleepingPlayer.displayName))
                {
                    continue;
                }

                if (sleepingPlayer.displayName.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return sleepingPlayer;
                }

                if (sleepingPlayer.displayName.Contains(name, CompareOptions.OrdinalIgnoreCase))
                {
                    player = sleepingPlayer;
                }
            }
            return player;
        }

        /// <summary>
        /// Returns the BasePlayer for the specified ID ulong
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static BasePlayer FindPlayerById(ulong id)
        {
            foreach (BasePlayer activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.userID == id)
                {
                    return activePlayer;
                }
            }
            foreach (BasePlayer sleepingPlayer in BasePlayer.sleepingPlayerList)
            {
                if (sleepingPlayer.userID == id)
                {
                    return sleepingPlayer;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns the BasePlayer for the specified ID string
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static BasePlayer FindPlayerByIdString(string id)
        {
            foreach (BasePlayer activePlayer in BasePlayer.activePlayerList)
            {
                if (string.IsNullOrEmpty(activePlayer.UserIDString))
                {
                    continue;
                }

                if (activePlayer.UserIDString.Equals(id))
                {
                    return activePlayer;
                }
            }
            foreach (BasePlayer sleepingPlayer in BasePlayer.sleepingPlayerList)
            {
                if (string.IsNullOrEmpty(sleepingPlayer.UserIDString))
                {
                    continue;
                }

                if (sleepingPlayer.UserIDString.Equals(id))
                {
                    return sleepingPlayer;
                }
            }
            return null;
        }

        #endregion Helpers
    }
}
