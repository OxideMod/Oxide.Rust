using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Libraries;
using System;
using System.Reflection;
using UnityEngine;

namespace Oxide.Plugins
{
    public abstract class RustPlugin : CSharpPlugin
    {
        protected Command cmd = Interface.Oxide.GetLibrary<Command>();
        protected Game.Rust.Libraries.Rust rust = Interface.Oxide.GetLibrary<Game.Rust.Libraries.Rust>();
        protected Game.Rust.Libraries.Item Item = Interface.Oxide.GetLibrary<Game.Rust.Libraries.Item>();
        protected Player Player = Interface.Oxide.GetLibrary<Player>();
        protected Server Server = Interface.Oxide.GetLibrary<Server>();

        public override void HandleAddedToManager(PluginManager manager)
        {
            foreach (var field in GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var attributes = field.GetCustomAttributes(typeof(OnlinePlayersAttribute), true);
                if (attributes.Length > 0)
                {
                    var pluginField = new PluginFieldInfo(this, field);
                    if (pluginField.GenericArguments.Length != 2 || pluginField.GenericArguments[0] != typeof(BasePlayer))
                    {
                        Puts($"The {field.Name} field is not a Hash with a BasePlayer key! (online players will not be tracked)");
                        continue;
                    }
                    if (!pluginField.LookupMethod("Add", pluginField.GenericArguments))
                    {
                        Puts($"The {field.Name} field does not support adding BasePlayer keys! (online players will not be tracked)");
                        continue;
                    }
                    if (!pluginField.LookupMethod("Remove", typeof(BasePlayer)))
                    {
                        Puts($"The {field.Name} field does not support removing BasePlayer keys! (online players will not be tracked)");
                        continue;
                    }
                    if (pluginField.GenericArguments[1].GetField("Player") == null)
                    {
                        Puts($"The {pluginField.GenericArguments[1].Name} class does not have a public Player field! (online players will not be tracked)");
                        continue;
                    }
                    if (!pluginField.HasValidConstructor(typeof(BasePlayer)))
                    {
                        Puts($"The {field.Name} field is using a class which contains no valid constructor (online players will not be tracked)");
                        continue;
                    }
                    onlinePlayerFields.Add(pluginField);
                }
            }

            foreach (var method in GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var attributes = method.GetCustomAttributes(typeof(ConsoleCommandAttribute), true);
                if (attributes.Length > 0)
                {
                    var attribute = attributes[0] as ConsoleCommandAttribute;
                    if (attribute != null) cmd.AddConsoleCommand(attribute.Command, this, method.Name);
                    continue;
                }

                attributes = method.GetCustomAttributes(typeof(ChatCommandAttribute), true);
                if (attributes.Length > 0)
                {
                    var attribute = attributes[0] as ChatCommandAttribute;
                    if (attribute != null) cmd.AddChatCommand(attribute.Command, this, method.Name);
                }
            }

            if (onlinePlayerFields.Count > 0) foreach (var player in BasePlayer.activePlayerList) AddOnlinePlayer(player);

            base.HandleAddedToManager(manager);
        }

        [HookMethod("OnPlayerInit")]
        private void base_OnPlayerInit(BasePlayer player) => AddOnlinePlayer(player);

        [HookMethod("OnPlayerDisconnected")]
        private void base_OnPlayerDisconnected(BasePlayer player)
        {
            // Delay removing player until OnPlayerDisconnect has fired in plugin
            NextTick(() =>
            {
                foreach (var pluginField in onlinePlayerFields) pluginField.Call("Remove", player);
            });
        }

        private void AddOnlinePlayer(BasePlayer player)
        {
            foreach (var pluginField in onlinePlayerFields)
            {
                var type = pluginField.GenericArguments[1];
                var onlinePlayer = type.GetConstructor(new[] { typeof(BasePlayer) }) == null ? Activator.CreateInstance(type) : Activator.CreateInstance(type, (object)player);
                type.GetField("Player").SetValue(onlinePlayer, player);
                pluginField.Call("Add", player, onlinePlayer);
            }
        }

        /// <summary>
        /// Print a message to the players console log
        /// </summary>
        /// <param name="player"></param>
        /// <param name="format"></param>
        /// <param name="args"></param>
        protected void PrintToConsole(BasePlayer player, string format, params object[] args)
        {
            if (player?.net != null) player.SendConsoleCommand("echo " + (args.Length > 0 ? string.Format(format, args) : format));
        }

        /// <summary>
        /// Print a message to every players console log
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        protected void PrintToConsole(string format, params object[] args)
        {
            if (BasePlayer.activePlayerList.Count < 1) return;
            ConsoleNetwork.BroadcastToAllClients("echo " + (args.Length > 0 ? string.Format(format, args) : format));
        }

        /// <summary>
        /// Print a message to the players chat log
        /// </summary>
        /// <param name="player"></param>
        /// <param name="format"></param>
        /// <param name="args"></param>
        protected void PrintToChat(BasePlayer player, string format, params object[] args)
        {
            if (player?.net != null) player.SendConsoleCommand("chat.add", 0, args.Length > 0 ? string.Format(format, args) : format, 1f);
        }

        /// <summary>
        /// Print a message to every players chat log
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        protected void PrintToChat(string format, params object[] args)
        {
            if (BasePlayer.activePlayerList.Count < 1) return;
            ConsoleNetwork.BroadcastToAllClients("chat.add", 0, args.Length > 0 ? string.Format(format, args) : format, 1f);
        }

        /// <summary>
        /// Send a reply message in response to a console command
        /// </summary>
        /// <param name="arg"></param>
        /// <param name="format"></param>
        /// <param name="args"></param>
        protected void SendReply(ConsoleSystem.Arg arg, string format, params object[] args)
        {
            var message = args.Length > 0 ? string.Format(format, args) : format;
            var player = arg.Connection?.player as BasePlayer;
            if (player?.net != null)
            {
                player.SendConsoleCommand("echo " + message);
                return;
            }
            Puts(message);
        }

        /// <summary>
        /// Send a reply message in response to a chat command
        /// </summary>
        /// <param name="player"></param>
        /// <param name="format"></param>
        /// <param name="args"></param>
        protected void SendReply(BasePlayer player, string format, params object[] args) => PrintToChat(player, format, args);

        /// <summary>
        /// Send a warning message in response to a console command
        /// </summary>
        /// <param name="arg"></param>
        /// <param name="format"></param>
        /// <param name="args"></param>
        protected void SendWarning(ConsoleSystem.Arg arg, string format, params object[] args)
        {
            var message = args.Length > 0 ? string.Format(format, args) : format;
            var player = arg.Connection?.player as BasePlayer;
            if (player?.net != null)
            {
                player.SendConsoleCommand("echo " + message);
                return;
            }
            Debug.LogWarning(message);
        }

        /// <summary>
        /// Send an error message in response to a console command
        /// </summary>
        /// <param name="arg"></param>
        /// <param name="format"></param>
        /// <param name="args"></param>
        protected void SendError(ConsoleSystem.Arg arg, string format, params object[] args)
        {
            var message = args.Length > 0 ? string.Format(format, args) : format;
            var player = arg.Connection?.player as BasePlayer;
            if (player?.net != null)
            {
                player.SendConsoleCommand("echo " + message);
                return;
            }
            Debug.LogError(message);
        }

        /// <summary>
        /// Forces the player to a specific position
        /// </summary>
        /// <param name="player"></param>
        /// <param name="destination"></param>
        protected void ForcePlayerPosition(BasePlayer player, Vector3 destination)
        {
            player.MovePosition(destination);
            if (!player.IsSpectating() || Vector3.Distance(player.transform.position, destination) > 25.0)
                player.ClientRPCPlayer(null, player, "ForcePositionTo", destination);
            else
                player.SendNetworkUpdate(BasePlayer.NetworkQueue.UpdateDistance);
        }
    }
}
