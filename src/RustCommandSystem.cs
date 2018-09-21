using System;
using System.Collections.Generic;
using System.Linq;
using uMod.Libraries;
using uMod.Libraries.Covalence;
using uMod.Plugins;
using UnityEngine;

namespace uMod.Rust
{
    /// <summary>
    /// Represents a binding to a generic command system
    /// </summary>
    public class RustCommandSystem : ICommandSystem
    {
        #region Initialization

        // The covalence provider
        private readonly RustProvider rustCovalence = RustProvider.Instance;

        // The console player
        private readonly RustConsolePlayer consolePlayer;

        // Command handler
        private readonly CommandHandler commandHandler;

        // All registered commands
        internal IDictionary<string, RegisteredCommand> registeredCommands;

        // All registerd console commands
        internal Dictionary<string, ConsoleCommand> consoleCommands;

        // All registered chat commands
        internal Dictionary<string, ChatCommand> chatCommands;

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
            internal readonly Permission permission = Interface.uMod.GetLibrary<Permission>();

            public ConsoleCommand(string name)
            {
                Name = name;
                string[] splitName = Name.Split('.');
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

        // Registered commands
        internal class RegisteredCommand
        {
            /// <summary>
            /// The plugin that handles the command
            /// </summary>
            public readonly Plugin Source;

            /// <summary>
            /// The name of the command
            /// </summary>
            public readonly string Command;

            /// <summary>
            /// The callback
            /// </summary>
            public readonly CommandCallback Callback;

            /// <summary>
            /// The Rust console command
            /// </summary>
            public ConsoleSystem.Command RustCommand;

            /// <summary>
            /// The orrignal console command when overridden
            /// </summary>
            public ConsoleSystem.Command OriginalRustCommand;

            /// <summary>
            /// The original callback
            /// </summary>
            public Action<ConsoleSystem.Arg> OriginalCallback;

            /// <summary>
            /// Initializes a new instance of the RegisteredCommand class
            /// </summary>
            /// <param name="source"></param>
            /// <param name="command"></param>
            /// <param name="callback"></param>
            public RegisteredCommand(Plugin source, string command, CommandCallback callback)
            {
                Source = source;
                Command = command;
                Callback = callback;
            }
        }

        /// <summary>
        /// Initializes the command system
        /// </summary>
        public RustCommandSystem()
        {
            registeredCommands = new Dictionary<string, RegisteredCommand>();
            commandHandler = new CommandHandler(CommandCallback, registeredCommands.ContainsKey);
            consolePlayer = new RustConsolePlayer();
        }

        private bool CommandCallback(IPlayer caller, string cmd, string[] args)
        {
            RegisteredCommand command;
            return registeredCommands.TryGetValue(cmd, out command) && command.Callback(caller, cmd, args);
        }

        #endregion Initialization

        #region Command Registration

        /// <summary>
        /// Registers the specified command
        /// </summary>
        /// <param name="command"></param>
        /// <param name="plugin"></param>
        /// <param name="callback"></param>
        public void RegisterCommand(string command, Plugin plugin, CommandCallback callback)
        {
            // Convert command to lowercase and remove whitespace
            command = command.ToLowerInvariant().Trim();

            // Setup console command name
            string[] split = command.Split('.');
            string parent = split.Length >= 2 ? split[0].Trim() : "global";
            string name = split.Length >= 2 ? string.Join(".", split.Skip(1).ToArray()) : split[0].Trim();
            string fullName = $"{parent}.{name}";

            if (parent == "global")
            {
                command = name;
            }

            // Setup a new Covalence command
            RegisteredCommand newCommand = new RegisteredCommand(plugin, command, callback);

            // Check if the command can be overridden
            if (!CanOverrideCommand(command))
            {
                throw new CommandAlreadyExistsException(command);
            }

            // Check if command already exists in another Covalence plugin
            RegisteredCommand cmd;
            if (registeredCommands.TryGetValue(command, out cmd))
            {
                if (cmd.OriginalCallback != null)
                {
                    newCommand.OriginalCallback = cmd.OriginalCallback;
                }

                string previousPluginName = cmd.Source?.Name ?? "an unknown plugin";
                string newPluginName = plugin?.Name ?? "An unknown plugin";
                string message = $"{newPluginName} has replaced the '{command}' command previously registered by {previousPluginName}";
                Interface.uMod.LogWarning(message);

                ConsoleSystem.Index.Server.Dict.Remove(fullName);
                if (parent == "global")
                {
                    ConsoleSystem.Index.Server.GlobalDict.Remove(name);
                }

                ConsoleSystem.Index.All = ConsoleSystem.Index.Server.Dict.Values.ToArray();
            }

            // Check if command already exists in a Rust plugin as a chat command
            ChatCommand chatCommand;
            if (chatCommands.TryGetValue(command, out chatCommand))
            {
                string previousPluginName = chatCommand.Plugin?.Name ?? "an unknown plugin";
                string newPluginName = plugin?.Name ?? "An unknown plugin";
                string message = $"{newPluginName} has replaced the '{command}' chat command previously registered by {previousPluginName}";
                Interface.uMod.LogWarning(message);
                chatCommands.Remove(command);
            }

            // Check if command already exists in a Rust plugin as a console command
            ConsoleCommand consoleCommand;
            if (consoleCommands.TryGetValue(fullName, out consoleCommand))
            {
                if (consoleCommand.OriginalCallback != null)
                {
                    newCommand.OriginalCallback = consoleCommand.OriginalCallback;
                }

                string previousPluginName = consoleCommand.Callback.Plugin?.Name ?? "an unknown plugin";
                string newPluginName = plugin?.Name ?? "An unknown plugin";
                string message = $"{newPluginName} has replaced the '{fullName}' console command previously registered by {previousPluginName}";
                Interface.uMod.LogWarning(message);

                ConsoleSystem.Index.Server.Dict.Remove(consoleCommand.RustCommand.FullName);
                if (parent == "global")
                {
                    ConsoleSystem.Index.Server.GlobalDict.Remove(consoleCommand.RustCommand.Name);
                }
                ConsoleSystem.Index.All = ConsoleSystem.Index.Server.Dict.Values.ToArray();
                consoleCommands.Remove(consoleCommand.RustCommand.FullName);
            }

            // Check if command is a vanilla Rust command
            ConsoleSystem.Command rustCommand;
            if (ConsoleSystem.Index.Server.Dict.TryGetValue(fullName, out rustCommand))
            {
                if (rustCommand.Variable)
                {
                    string newPluginName = plugin?.Name ?? "An unknown plugin";
                    Interface.uMod.LogError($"{newPluginName} tried to register the {fullName} console variable as a command!");
                    return;
                }

                newCommand.OriginalCallback = rustCommand.Call;
                newCommand.OriginalRustCommand = rustCommand;
            }

            // Create a new Rust console command
            newCommand.RustCommand = new ConsoleSystem.Command
            {
                Name = name,
                Parent = parent,
                FullName = command,
                ServerUser = true,
                ServerAdmin = true,
                Client = true,
                ClientInfo = false,
                Variable = false,
                Call = arg =>
                {
                    if (arg == null)
                    {
                        return;
                    }

                    BasePlayer player = arg.Player();
                    if (arg.Connection != null && player != null)
                    {
                        RustPlayer iplayer = player.IPlayer as RustPlayer;
                        if (iplayer != null)
                        {
                            iplayer.LastCommand = CommandType.Console;
                            callback(iplayer, command, ExtractArgs(arg));
                        }
                    }
                    else if (arg.Connection == null)
                    {
                        consolePlayer.LastCommand = CommandType.Console;
                        callback(consolePlayer, command, ExtractArgs(arg));
                    }
                }
            };

            // Register the command as a console command
            ConsoleSystem.Index.Server.Dict[fullName] = newCommand.RustCommand;
            if (parent == "global")
            {
                ConsoleSystem.Index.Server.GlobalDict[name] = newCommand.RustCommand;
            }
            ConsoleSystem.Index.All = ConsoleSystem.Index.Server.Dict.Values.ToArray();

            // Register the command as a chat command
            registeredCommands[command] = newCommand;
        }

        #endregion Command Registration

        #region Command Unregistration

        /// <summary>
        /// Unregisters the specified command
        /// </summary>
        /// <param name="command"></param>
        /// <param name="plugin"></param>
        public void UnregisterCommand(string command, Plugin plugin)
        {
            RegisteredCommand cmd;
            if (!registeredCommands.TryGetValue(command, out cmd))
            {
                return;
            }

            // Check if the command belongs to the plugin
            if (plugin != cmd.Source)
            {
                return;
            }

            // Setup console command name
            string[] split = command.Split('.');
            string parent = split.Length >= 2 ? split[0].Trim() : "global";
            string name = split.Length >= 2 ? string.Join(".", split.Skip(1).ToArray()) : split[0].Trim();
            string fullName = $"{parent}.{name}";

            // Remove the chat command
            registeredCommands.Remove(command);

            // If this was originally a vanilla Rust command then restore it, otherwise remove it
            if (cmd.OriginalCallback != null)
            {
                ConsoleSystem.Index.Server.Dict[fullName].Call = cmd.OriginalCallback;
                if (fullName.StartsWith("global."))
                {
                    ConsoleSystem.Index.Server.GlobalDict[name].Call = cmd.OriginalCallback;
                }
                // This part handles Rust commands, above handles overwritten across plugins
                if (cmd.OriginalRustCommand != null)
                {
                    ConsoleSystem.Index.Server.Dict[fullName] = cmd.OriginalRustCommand;
                    if (fullName.StartsWith("global."))
                    {
                        ConsoleSystem.Index.Server.GlobalDict[name] = cmd.OriginalRustCommand;
                    }
                }
            }
            else
            {
                ConsoleSystem.Index.Server.Dict.Remove(fullName);
                if (fullName.StartsWith("global."))
                {
                    ConsoleSystem.Index.Server.GlobalDict.Remove(name);
                }
            }

            // The "find" command uses this, so rebuild it when a command is unregistered (as well as registered)
            ConsoleSystem.Index.All = ConsoleSystem.Index.Server.Dict.Values.ToArray();
        }

        #endregion Command Unregistration

        #region Message Handling

        /// <summary>
        /// Handles a chat message
        /// </summary>
        /// <param name="player"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public bool HandleChatMessage(IPlayer player, string message) => commandHandler.HandleChatMessage(player, message);

        #endregion Message Handling

        #region Command Overriding

        /// <summary>
        /// Checks if a command can be overridden
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        private bool CanOverrideCommand(string command)
        {
            string[] split = command.Split('.');
            string parent = split.Length >= 2 ? split[0].Trim() : "global";
            string name = split.Length >= 2 ? string.Join(".", split.Skip(1).ToArray()) : split[0].Trim();
            string fullName = $"{parent}.{name}";

            RegisteredCommand cmd;
            if (registeredCommands.TryGetValue(command, out cmd))
            {
                if (cmd.Source.IsCorePlugin)
                {
                    return false;
                }
            }

            ChatCommand chatCommand;
            if (chatCommands.TryGetValue(command, out chatCommand))
            {
                if (chatCommand.Plugin.IsCorePlugin)
                {
                    return false;
                }
            }

            ConsoleCommand consoleCommand;
            if (consoleCommands.TryGetValue(fullName, out consoleCommand))
            {
                if (consoleCommand.Callback.Plugin.IsCorePlugin)
                {
                    return false;
                }
            }

            return !Rust.RestrictedCommands.Contains(command) && !Rust.RestrictedCommands.Contains(fullName);
        }

        #endregion Command Overriding

        #region Helpers

        /// <summary>
        /// Extract the arguments from a ConsoleSystem.Arg object
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        private static string[] ExtractArgs(ConsoleSystem.Arg arg)
        {
            if (arg == null)
            {
                return new string[0];
            }

            List<string> argsList = new List<string>();
            int i = 0;
            while (arg.HasArgs(++i))
            {
                argsList.Add(arg.GetString(i - 1));
            }

            return argsList.ToArray();
        }

        #endregion Helpers
    }
}
