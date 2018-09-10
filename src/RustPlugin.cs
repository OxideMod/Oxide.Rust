using System;
using System.Reflection;
using uMod.Rust.Libraries;

namespace uMod.Plugins
{
    public abstract class RustPlugin : CSharpPlugin
    {
        protected Command cmd = Interface.uMod.GetLibrary<Command>();

        public override void HandleAddedToManager(PluginManager manager)
        {
            foreach (FieldInfo field in GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                object[] attributes = field.GetCustomAttributes(typeof(OnlinePlayersAttribute), true);
                if (attributes.Length > 0)
                {
                    PluginFieldInfo pluginField = new PluginFieldInfo(this, field);
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

            foreach (MethodInfo method in GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                object[] attributes = method.GetCustomAttributes(typeof(ConsoleCommandAttribute), true);
                if (attributes.Length > 0)
                {
                    ConsoleCommandAttribute attribute = attributes[0] as ConsoleCommandAttribute;
                    if (attribute != null)
                    {
                        cmd.AddConsoleCommand(attribute.Command, this, method.Name);
                    }

                    continue;
                }

                attributes = method.GetCustomAttributes(typeof(ChatCommandAttribute), true);
                if (attributes.Length > 0)
                {
                    ChatCommandAttribute attribute = attributes[0] as ChatCommandAttribute;
                    if (attribute != null)
                    {
                        cmd.AddChatCommand(attribute.Command, this, method.Name);
                    }
                }
            }

            if (onlinePlayerFields.Count > 0)
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    AddOnlinePlayer(player);
                }
            }

            base.HandleAddedToManager(manager);
        }

        [HookMethod("OnPlayerInit")]
        private void base_OnPlayerInit(BasePlayer player) => AddOnlinePlayer(player);

        [HookMethod("OnPlayerDisconnected")]
        private void base_OnPlayerDisconnected(BasePlayer player, string reason)
        {
            // Delay removing player until OnPlayerDisconnected has fired in plugin
            NextTick(() =>
            {
                foreach (PluginFieldInfo pluginField in onlinePlayerFields)
                {
                    pluginField.Call("Remove", player);
                }
            });
        }

        private void AddOnlinePlayer(BasePlayer player)
        {
            foreach (PluginFieldInfo pluginField in onlinePlayerFields)
            {
                Type type = pluginField.GenericArguments[1];
                object onlinePlayer = type.GetConstructor(new[] { typeof(BasePlayer) }) == null ? Activator.CreateInstance(type) : Activator.CreateInstance(type, (object)player);
                type.GetField("Player").SetValue(onlinePlayer, player);
                pluginField.Call("Add", player, onlinePlayer);
            }
        }
    }
}
