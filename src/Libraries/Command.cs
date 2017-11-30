using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Event = Oxide.Core.Event;

namespace Oxide.Game.Rust.Libraries
{
    /// <summary>
    /// A library containing functions for adding console and chat commands
    /// </summary>
    public class Command : Library
    {
        internal struct PluginCallback
        {
            public readonly Plugin Plugin;
            public readonly string Name;
            public Func<ConsoleSystem.Arg, bool> Call;

            public PluginCallback(Plugin plugin, string name)
            {
                Plugin = plugin;
                Name = name;
                Call = null;
            }

            public PluginCallback(Plugin plugin, Func<ConsoleSystem.Arg, bool> callback)
            {
                Plugin = plugin;
                Call = callback;
                Name = null;
            }
        }

        internal class ConsoleCommand
        {
            public readonly string Name;
            public PluginCallback Callback;
            public readonly ConsoleSystem.Command RustCommand;
            public Action<ConsoleSystem.Arg> OriginalCallback;
            internal readonly Permission permission = Interface.Oxide.GetLibrary<Permission>();

            public ConsoleCommand(string name)
            {
                Name = name;
                var splitName = Name.Split('.');
                RustCommand = new ConsoleSystem.Command
                {
                    Name = splitName[1],
                    Parent = splitName[0],
                    FullName = name,
                    ServerUser = true,
                    ServerAdmin = true,
                    Client = true,
                    ClientInfo = false,
                    Variable = false,
                    Call = HandleCommand
                };
            }

            public void AddCallback(Plugin plugin, string name) => Callback = new PluginCallback(plugin, name);

            public void AddCallback(Plugin plugin, Func<ConsoleSystem.Arg, bool> callback) => Callback = new PluginCallback(plugin, callback);

            public void HandleCommand(ConsoleSystem.Arg arg)
            {
                Callback.Plugin?.TrackStart();
                Callback.Call(arg);
                Callback.Plugin?.TrackEnd();
            }
        }

        internal class ChatCommand
        {
            public readonly string Name;
            public readonly Plugin Plugin;
            private readonly Action<BasePlayer, string, string[]> _callback;

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
        public void AddChatCommand(string name, Plugin plugin, string callback) => AddChatCommand(name, plugin, (player, command, args) => plugin.CallHook(callback, player, command, args));

        /// <summary>
        /// Adds a chat command
        /// </summary>
        /// <param name="command"></param>
        /// <param name="plugin"></param>
        /// <param name="callback"></param>
        public void AddChatCommand(string command, Plugin plugin, Action<BasePlayer, string, string[]> callback)
        {
            var commandName = command.ToLowerInvariant();

            if (!CanOverrideCommand(command, "chat"))
            {
                var pluginName = plugin?.Name ?? "An unknown plugin";
                Interface.Oxide.LogError("{0} tried to register command '{1}', this command already exists and cannot be overridden!", pluginName, commandName);
                return;
            }

            ChatCommand cmd;
            if (chatCommands.TryGetValue(commandName, out cmd))
            {
                var previousPluginName = cmd.Plugin?.Name ?? "an unknown plugin";
                var newPluginName = plugin?.Name ?? "An unknown plugin";
                var message = $"{newPluginName} has replaced the '{commandName}' chat command previously registered by {previousPluginName}";
                Interface.Oxide.LogWarning(message);
            }

            RustCommandSystem.RegisteredCommand covalenceCommand;
            if (RustCore.Covalence.CommandSystem.registeredCommands.TryGetValue(commandName, out covalenceCommand))
            {
                var previousPluginName = covalenceCommand.Source?.Name ?? "an unknown plugin";
                var newPluginName = plugin?.Name ?? "An unknown plugin";
                var message = $"{newPluginName} has replaced the '{commandName}' command previously registered by {previousPluginName}";
                Interface.Oxide.LogWarning(message);
                RustCore.Covalence.CommandSystem.UnregisterCommand(commandName, covalenceCommand.Source);
            }

            cmd = new ChatCommand(commandName, plugin, callback);

            // Add the new command to collections
            chatCommands[commandName] = cmd;

            // Hook the unload event
            if (plugin != null && !pluginRemovedFromManager.ContainsKey(plugin))
                pluginRemovedFromManager[plugin] = plugin.OnRemovedFromManager.Add(plugin_OnRemovedFromManager);
        }

        /// <summary>
        /// Adds a console command
        /// </summary>
        /// <param name="command"></param>
        /// <param name="plugin"></param>
        /// <param name="callback"></param>
        [LibraryFunction("AddConsoleCommand")]
        public void AddConsoleCommand(string command, Plugin plugin, string callback) => AddConsoleCommand(command, plugin, arg => plugin.CallHook(callback, arg) != null);

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
                pluginRemovedFromManager[plugin] = plugin.OnRemovedFromManager.Add(plugin_OnRemovedFromManager);

            // Setup console command name
            var split = command.Split('.');
            var parent = split.Length >= 2 ? split[0].Trim() : "global";
            var name = split.Length >= 2 ? string.Join(".", split.Skip(1).ToArray()) : split[0].Trim();
            var fullName = $"{parent}.{name}";

            // Setup a new RustPlugin console command
            var cmd = new ConsoleCommand(fullName);

            // Check if the command can be overridden
            if (!CanOverrideCommand(parent == "global" ? name : fullName, "console"))
            {
                var pluginName = plugin?.Name ?? "An unknown plugin";
                Interface.Oxide.LogError("{0} tried to register command '{1}', this command already exists and cannot be overridden!", pluginName, fullName);
                return;
            }

            // Check if it already exists in a Rust plugin as a console command
            ConsoleCommand consoleCommand;
            if (consoleCommands.TryGetValue(fullName, out consoleCommand))
            {
                if (consoleCommand.OriginalCallback != null) cmd.OriginalCallback = consoleCommand.OriginalCallback;

                var previousPluginName = consoleCommand.Callback.Plugin?.Name ?? "an unknown plugin";
                var newPluginName = plugin?.Name ?? "An unknown plugin";
                var message = $"{newPluginName} has replaced the '{command}' console command previously registered by {previousPluginName}";
                Interface.Oxide.LogWarning(message);

                ConsoleSystem.Index.Server.Dict.Remove(consoleCommand.RustCommand.FullName);
                if (parent == "global") ConsoleSystem.Index.Server.GlobalDict.Remove(consoleCommand.RustCommand.Name);
                ConsoleSystem.Index.All = ConsoleSystem.Index.Server.Dict.Values.ToArray();
            }

            RustCommandSystem.RegisteredCommand covalenceCommand;
            if (RustCore.Covalence.CommandSystem.registeredCommands.TryGetValue(parent == "global" ? name : fullName, out covalenceCommand))
            {
                if (covalenceCommand.OriginalCallback != null) cmd.OriginalCallback = covalenceCommand.OriginalCallback;

                var previousPluginName = covalenceCommand.Source?.Name ?? "an unknown plugin";
                var newPluginName = plugin?.Name ?? "An unknown plugin";
                var message = $"{newPluginName} has replaced the '{fullName}' command previously registered by {previousPluginName}";
                Interface.Oxide.LogWarning(message);

                RustCore.Covalence.CommandSystem.UnregisterCommand(parent == "global" ? name : fullName, covalenceCommand.Source);
            }

            // The command either does not already exist or is replacing a previously registered command
            cmd.AddCallback(plugin, callback);

            ConsoleSystem.Command rustCommand;
            if (ConsoleSystem.Index.Server.Dict.TryGetValue(fullName, out rustCommand))
            {
                // This is a vanilla Rust command which has not yet been hooked by a plugin
                if (rustCommand.Variable)
                {
                    var newPluginName = plugin?.Name ?? "An unknown plugin";
                    Interface.Oxide.LogError($"{newPluginName} tried to register the {name} console variable as a command!");
                    return;
                }
                cmd.OriginalCallback = rustCommand.Call;
            }

            // Register the console command
            ConsoleSystem.Index.Server.Dict[fullName] = cmd.RustCommand;
            if (parent == "global") ConsoleSystem.Index.Server.GlobalDict[name] = cmd.RustCommand;
            ConsoleSystem.Index.All = ConsoleSystem.Index.Server.Dict.Values.ToArray();
            consoleCommands[fullName] = cmd;
        }

        /// <summary>
        /// Handles the specified chat command
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="name"></param>
        /// <param name="args"></param>
        internal bool HandleChatCommand(BasePlayer sender, string name, string[] args)
        {
            ChatCommand cmd;
            if (!chatCommands.TryGetValue(name.ToLowerInvariant(), out cmd)) return false;
            cmd.HandleCommand(sender, name, args);
            return true;
        }

        /// <summary>
        /// Called when a plugin has been removed from manager
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="manager"></param>
        private void plugin_OnRemovedFromManager(Plugin sender, PluginManager manager)
        {
            // Find all console commands which were registered by the plugin
            var commands = consoleCommands.Values.Where(c => c.Callback.Plugin == sender).ToArray();
            foreach (var cmd in commands)
            {
                // This command is no longer registered by any plugins
                consoleCommands.Remove(cmd.Name);

                // If this was originally a vanilla rust command then restore it, otherwise remove it
                if (cmd.OriginalCallback != null)
                {
                    ConsoleSystem.Index.Server.Dict[cmd.RustCommand.FullName].Call = cmd.OriginalCallback;
                    if (cmd.RustCommand.FullName.StartsWith("global."))
                        ConsoleSystem.Index.Server.GlobalDict[cmd.RustCommand.Name].Call = cmd.OriginalCallback;
                }
                else
                {
                    ConsoleSystem.Index.Server.Dict.Remove(cmd.RustCommand.FullName);
                    if (cmd.Name.StartsWith("global.")) ConsoleSystem.Index.Server.GlobalDict.Remove(cmd.RustCommand.Name);
                    ConsoleSystem.Index.All = ConsoleSystem.Index.Server.Dict.Values.ToArray();
                }
            }

            // Remove all chat commands which were registered by the plugin
            foreach (var cmd in chatCommands.Values.Where(c => c.Plugin == sender).ToArray())
                chatCommands.Remove(cmd.Name);

            // Unhook the event
            Event.Callback<Plugin, PluginManager> callback;
            if (pluginRemovedFromManager.TryGetValue(sender, out callback))
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
            var split = command.Split('.');
            var parent = split.Length >= 2 ? split[0].Trim() : "global";
            var name = split.Length >= 2 ? string.Join(".", split.Skip(1).ToArray()) : split[0].Trim();
            var fullname = $"{parent}.{name}";

            RustCommandSystem.RegisteredCommand cmd;
            if (RustCore.Covalence.CommandSystem.registeredCommands.TryGetValue(command, out cmd))
                if (cmd.Source.IsCorePlugin)
                    return false;

            if (type == "chat")
            {
                ChatCommand chatCommand;
                if (chatCommands.TryGetValue(command, out chatCommand))
                    if (chatCommand.Plugin.IsCorePlugin)
                        return false;
            }
            else if (type == "console")
            {
                ConsoleCommand consoleCommand;
                if (consoleCommands.TryGetValue(parent == "global" ? name : fullname, out consoleCommand))
                    if (consoleCommand.Callback.Plugin.IsCorePlugin)
                        return false;
            }

            return !RustCore.RestrictedCommands.Contains(command) && !RustCore.RestrictedCommands.Contains(fullname);
        }
    }
}
