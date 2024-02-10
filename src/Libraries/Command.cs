using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Game.Rust.Libraries
{
    public class Command : Library
    {
        private readonly HashSet<string> consoleCommands;
        private readonly HashSet<string> chatCommands;
        private readonly Dictionary<Plugin, Event.Callback<Plugin, PluginManager>> pluginRemovedFromManager;

        public Command()
        {
            consoleCommands = new HashSet<string>();
            chatCommands = new HashSet<string>();
            pluginRemovedFromManager = new Dictionary<Plugin, Event.Callback<Plugin, PluginManager>>();
        }

        [LibraryFunction("AddChatCommand")]
        public void AddChatCommand(string name, Plugin plugin, Action<BasePlayer, string, string[]> callback)
        {
            name = name.ToLowerInvariant();
            if (!CanOverrideCommand(name, "chat"))
            {
                Interface.Oxide.LogError($"{plugin?.Name ?? "An unknown plugin"} tried to register command '{name}', which already exists and cannot be overridden!");
                return;
            }

            if (chatCommands.Contains(name))
            {
                Interface.Oxide.LogWarning($"{plugin?.Name ?? "An unknown plugin"} has replaced the '{name}' chat command previously registered.");
            }

            chatCommands.Add(name);
            pluginRemovedFromManager[plugin] = plugin.OnRemovedFromManager.Add(plugin_OnRemovedFromManager);
        }

        [LibraryFunction("AddConsoleCommand")]
        public void AddConsoleCommand(string command, Plugin plugin, Func<ConsoleSystem.Arg, bool> callback)
        {
            string name = GetCommandName(command);
            if (!CanOverrideCommand(name, "console"))
            {
                Interface.Oxide.LogError($"{plugin?.Name ?? "An unknown plugin"} tried to register command '{name}', which already exists and cannot be overridden!");
                return;
            }

            consoleCommands.Add(name);
            pluginRemovedFromManager[plugin] = plugin.OnRemovedFromManager.Add(plugin_OnRemovedFromManager);
        }

        [LibraryFunction("RemoveChatCommand")]
        public void RemoveChatCommand(string command, Plugin plugin)
        {
            command = command.ToLowerInvariant();
            if (chatCommands.Remove(command))
            {
                pluginRemovedFromManager.Remove(plugin);
            }
        }

        [LibraryFunction("RemoveConsoleCommand")]
        public void RemoveConsoleCommand(string command, Plugin plugin)
        {
            string name = GetCommandName(command);
            if (consoleCommands.Remove(name))
            {
                pluginRemovedFromManager.Remove(plugin);
            }
        }

        private void plugin_OnRemovedFromManager(Plugin sender, PluginManager manager)
        {
            consoleCommands.RemoveWhere(cmd => cmd.Plugin == sender);
            chatCommands.RemoveWhere(cmd => cmd.Plugin == sender);
            pluginRemovedFromManager.Remove(sender);
        }

        private string GetCommandName(string command)
        {
            string[] split = command.Split('.');
            return split.Length >= 2 ? $"{split[0].Trim().ToLowerInvariant()}.{string.Join(".", split.Skip(1).ToArray())}" : split[0].Trim().ToLowerInvariant();
        }

        private bool CanOverrideCommand(string command, string type)
        {
            if (type == "chat")
            {
                return !chatCommands.Contains(command);
            }
            else if (type == "console")
            {
                return !consoleCommands.Contains(command);
            }
            return false;
        }
    }
}

            public ChatCommand(string name, Plugin plugin, Action<BasePlayer, string, string[]> callback)
            {
                Name = name;
                Plugin = plugin;
                _callback = callback;
            }

            public void HandleCommand(BasePlayer sender, string name, string[] args)
            {
                Plugin?.TrackStart();
                _callback?.Invoke(sender, name, args);
                Plugin?.TrackEnd();
            }
        }

        // All console commands that plugins have registered
        internal readonly Dictionary<string, ConsoleCommand> consoleCommands;

        // All chat commands that plugins have registered
        internal readonly Dictionary<string, ChatCommand> chatCommands;

        // A reference to the plugin removed callbacks
        private readonly Dictionary<Plugin, Event.Callback<Plugin, PluginManager>> pluginRemovedFromManager;

        /// <summary>
        /// Initializes a new instance of the Command class
        /// </summary>
        public Command()
        {
            consoleCommands = new Dictionary<string, ConsoleCommand>();
            chatCommands = new Dictionary<string, ChatCommand>();
            pluginRemovedFromManager = new Dictionary<Plugin, Event.Callback<Plugin, PluginManager>>();
        }

        /// <summary>
        /// Adds a chat command
        /// </summary>
        /// <param name="name"></param>
        /// <param name="plugin"></param>
        /// <param name="callback"></param>
        [LibraryFunction("AddChatCommand")]
        public void AddChatCommand(string name, Plugin plugin, string callback)
        {
            AddChatCommand(name, plugin, (player, command, args) => plugin.CallHook(callback, player, command, args));
        }

        /// <summary>
        /// Adds a chat command
        /// </summary>
        /// <param name="command"></param>
        /// <param name="plugin"></param>
        /// <param name="callback"></param>
        public void AddChatCommand(string command, Plugin plugin, Action<BasePlayer, string, string[]> callback)
        {
            string commandName = command.ToLowerInvariant();

            if (!CanOverrideCommand(command, "chat"))
            {
                string pluginName = plugin?.Name ?? "An unknown plugin";
                Interface.Oxide.LogError("{0} tried to register command '{1}', this command already exists and cannot be overridden!", pluginName, commandName);
                return;
            }

            if (chatCommands.TryGetValue(commandName, out ChatCommand cmd))
            {
                string previousPluginName = cmd.Plugin?.Name ?? "an unknown plugin";
                string newPluginName = plugin?.Name ?? "An unknown plugin";
                string message = $"{newPluginName} has replaced the '{commandName}' chat command previously registered by {previousPluginName}";
                Interface.Oxide.LogWarning(message);
            }

            if (RustCore.Covalence.CommandSystem.registeredCommands.TryGetValue(commandName, out RustCommandSystem.RegisteredCommand covalenceCommand))
            {
                string previousPluginName = covalenceCommand.Source?.Name ?? "an unknown plugin";
                string newPluginName = plugin?.Name ?? "An unknown plugin";
                string message = $"{newPluginName} has replaced the '{commandName}' command previously registered by {previousPluginName}";
                Interface.Oxide.LogWarning(message);
                RustCore.Covalence.CommandSystem.UnregisterCommand(commandName, covalenceCommand.Source);
            }

            cmd = new ChatCommand(commandName, plugin, callback);

            // Add the new command to collections
            chatCommands[commandName] = cmd;

            // Hook the unload event
            if (plugin != null && !pluginRemovedFromManager.ContainsKey(plugin))
            {
                pluginRemovedFromManager[plugin] = plugin.OnRemovedFromManager.Add(plugin_OnRemovedFromManager);
            }
        }

        /// <summary>
        /// Adds a console command
        /// </summary>
        /// <param name="command"></param>
        /// <param name="plugin"></param>
        /// <param name="callback"></param>
        [LibraryFunction("AddConsoleCommand")]
        public void AddConsoleCommand(string command, Plugin plugin, string callback)
        {
            AddConsoleCommand(command, plugin, arg => plugin.CallHook(callback, arg) != null);
        }

        /// <summary>
        /// Adds a console command with a delegate callback
        /// </summary>
        /// <param name="command"></param>
        /// <param name="plugin"></param>
        /// <param name="callback"></param>
        public void AddConsoleCommand(string command, Plugin plugin, Func<ConsoleSystem.Arg, bool> callback)
        {
            // Hook the unload event
            if (plugin != null && !pluginRemovedFromManager.ContainsKey(plugin))
            {
                pluginRemovedFromManager[plugin] = plugin.OnRemovedFromManager.Add(plugin_OnRemovedFromManager);
            }

            // Setup console command name
            string[] split = command.Split('.');
            string parent = split.Length >= 2 ? split[0].Trim() : "global";
            string name = split.Length >= 2 ? string.Join(".", split.Skip(1).ToArray()) : split[0].Trim();
            string fullName = $"{parent}.{name}";

            // Setup a new RustPlugin console command
            ConsoleCommand cmd = new ConsoleCommand(fullName);

            // Check if the command can be overridden
            if (!CanOverrideCommand(parent == "global" ? name : fullName, "console"))
            {
                string pluginName = plugin?.Name ?? "An unknown plugin";
                Interface.Oxide.LogError("{0} tried to register command '{1}', this command already exists and cannot be overridden!", pluginName, fullName);
                return;
            }

            // Check if it already exists in a Rust plugin as a console command
            if (consoleCommands.TryGetValue(fullName, out ConsoleCommand consoleCommand))
            {
                if (consoleCommand.OriginalCallback != null)
                {
                    cmd.OriginalCallback = consoleCommand.OriginalCallback;
                }

                string previousPluginName = consoleCommand.Callback.Plugin?.Name ?? "an unknown plugin";
                string newPluginName = plugin?.Name ?? "An unknown plugin";
                string message = $"{newPluginName} has replaced the '{command}' console command previously registered by {previousPluginName}";
                Interface.Oxide.LogWarning(message);

                ConsoleSystem.Index.Server.Dict.Remove(consoleCommand.RustCommand.FullName);
                if (parent == "global")
                {
                    ConsoleSystem.Index.Server.GlobalDict.Remove(consoleCommand.RustCommand.Name);
                }

                ConsoleSystem.Index.All = ConsoleSystem.Index.Server.Dict.Values.ToArray();
            }

            if (RustCore.Covalence.CommandSystem.registeredCommands.TryGetValue(parent == "global" ? name : fullName, out RustCommandSystem.RegisteredCommand covalenceCommand))
            {
                if (covalenceCommand.OriginalCallback != null)
                {
                    cmd.OriginalCallback = covalenceCommand.OriginalCallback;
                }

                string previousPluginName = covalenceCommand.Source?.Name ?? "an unknown plugin";
                string newPluginName = plugin?.Name ?? "An unknown plugin";
                string message = $"{newPluginName} has replaced the '{fullName}' command previously registered by {previousPluginName}";
                Interface.Oxide.LogWarning(message);

                RustCore.Covalence.CommandSystem.UnregisterCommand(parent == "global" ? name : fullName, covalenceCommand.Source);
            }

            // The command either does not already exist or is replacing a previously registered command
            cmd.AddCallback(plugin, callback);

            if (ConsoleSystem.Index.Server.Dict.TryGetValue(fullName, out ConsoleSystem.Command rustCommand))
            {
                // This is a vanilla Rust command which has not yet been hooked by a plugin
                if (rustCommand.Variable)
                {
                    string newPluginName = plugin?.Name ?? "An unknown plugin";
                    Interface.Oxide.LogError($"{newPluginName} tried to register the {name} console variable as a command!");
                    return;
                }
                cmd.OriginalCallback = rustCommand.Call;
            }

            // Register the console command
            ConsoleSystem.Index.Server.Dict[fullName] = cmd.RustCommand;
            if (parent == "global")
            {
                ConsoleSystem.Index.Server.GlobalDict[name] = cmd.RustCommand;
            }

            ConsoleSystem.Index.All = ConsoleSystem.Index.Server.Dict.Values.ToArray();
            consoleCommands[fullName] = cmd;
        }

        /// <summary>
        /// Removes a previously registered chat command
        /// </summary>
        /// <param name="command"></param>
        /// <param name="plugin"></param>
        [LibraryFunction("RemoveChatCommand")]
        public void RemoveChatCommand(string command, Plugin plugin)
        {
            ChatCommand matchedCommand = chatCommands.Values.Where(x => x.Plugin == plugin).FirstOrDefault(x => x.Name == command);
            if (matchedCommand != null)
            {
                RemoveChatCommand(matchedCommand);
            }
        }

        /// <summary>
        /// Removes a previously registered console command
        /// </summary>
        /// <param name="command"></param>
        /// <param name="plugin"></param>
        [LibraryFunction("RemoveConsoleCommand")]
        public void RemoveConsoleCommand(string command, Plugin plugin)
        {
            ConsoleCommand matchedCommand = consoleCommands.Values.Where(x => x.Callback.Plugin == plugin).FirstOrDefault(x => x.Name == command);
            if (matchedCommand != null)
            {
                RemoveConsoleCommand(matchedCommand);
            }
        }

        /// <summary>
        /// Removes a chat command
        /// </summary>
        /// <param name="command"></param>
        private void RemoveChatCommand(ChatCommand command)
        {
            if (chatCommands.ContainsKey(command.Name))
            {
                chatCommands.Remove(command.Name);
            }
        }

        /// <summary>
        /// Removes a console command
        /// </summary>
        /// <param name="command"></param>
        private void RemoveConsoleCommand(ConsoleCommand command)
        {
            if (consoleCommands.ContainsKey(command.Name))
            {
                // This command is no longer registered by any plugins
                consoleCommands.Remove(command.Name);

                // If this was originally a vanilla rust command then restore it, otherwise remove it
                if (command.OriginalCallback != null)
                {
                    ConsoleSystem.Index.Server.Dict[command.RustCommand.FullName].Call = command.OriginalCallback;
                    if (command.RustCommand.FullName.StartsWith("global."))
                    {
                        ConsoleSystem.Index.Server.GlobalDict[command.RustCommand.Name].Call = command.OriginalCallback;
                    }
                }
                else
                {
                    ConsoleSystem.Index.Server.Dict.Remove(command.RustCommand.FullName);
                    if (command.Name.StartsWith("global."))
                    {
                        ConsoleSystem.Index.Server.GlobalDict.Remove(command.RustCommand.Name);
                    }

                    ConsoleSystem.Index.All = ConsoleSystem.Index.Server.Dict.Values.ToArray();
                }
            }
        }

        /// <summary>
        /// Handles the specified chat command
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="name"></param>
        /// <param name="args"></param>
        internal bool HandleChatCommand(BasePlayer sender, string name, string[] args)
        {
            if (chatCommands.TryGetValue(name.ToLowerInvariant(), out ChatCommand cmd))
            {
                cmd.HandleCommand(sender, name, args);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Called when a plugin has been removed from manager
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="manager"></param>
        private void plugin_OnRemovedFromManager(Plugin sender, PluginManager manager)
        {
            // Find all console commands which were registered by the plugin
            ConsoleCommand[] commands = consoleCommands.Values.Where(c => c.Callback.Plugin == sender).ToArray();
            foreach (ConsoleCommand cmd in commands)
            {
                RemoveConsoleCommand(cmd);
            }

            // Remove all chat commands which were registered by the plugin
            foreach (ChatCommand cmd in chatCommands.Values.Where(c => c.Plugin == sender).ToArray())
            {
                RemoveChatCommand(cmd);
            }

            // Unhook the event
            if (pluginRemovedFromManager.TryGetValue(sender, out Event.Callback<Plugin, PluginManager> callback))
            {
                callback.Remove();
                pluginRemovedFromManager.Remove(sender);
            }
        }

        /// <summary>
        /// Checks if a command can be overridden
        /// </summary>
        /// <param name="command"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private bool CanOverrideCommand(string command, string type)
        {
            string[] split = command.Split('.');
            string parent = split.Length >= 2 ? split[0].Trim() : "global";
            string name = split.Length >= 2 ? string.Join(".", split.Skip(1).ToArray()) : split[0].Trim();
            string fullname = $"{parent}.{name}";

            if (RustCore.Covalence.CommandSystem.registeredCommands.TryGetValue(command, out RustCommandSystem.RegisteredCommand cmd))
            {
                if (cmd.Source.IsCorePlugin)
                {
                    return false;
                }
            }

            if (type == "chat")
            {
                if (chatCommands.TryGetValue(command, out ChatCommand chatCommand))
                {
                    if (chatCommand.Plugin.IsCorePlugin)
                    {
                        return false;
                    }
                }
            }
            else if (type == "console")
            {
                if (consoleCommands.TryGetValue(parent == "global" ? name : fullname, out ConsoleCommand consoleCommand))
                {
                    if (consoleCommand.Callback.Plugin.IsCorePlugin)
                    {
                        return false;
                    }
                }
            }

            return !RustCore.RestrictedCommands.Contains(command) && !RustCore.RestrictedCommands.Contains(fullname);
        }
    }
}
