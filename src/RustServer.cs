using Facepunch;
using Rust;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using uMod.Libraries.Universal;
using uMod.Logging;

namespace uMod.Rust
{
    /// <summary>
    /// Represents the server hosting the game instance
    /// </summary>
    public class RustServer : IServer
    {
        #region Information

        /// <summary>
        /// Gets/sets the public-facing name of the server
        /// </summary>
        public string Name
        {
            get => ConVar.Server.hostname;
            set => ConVar.Server.hostname = value;
        }

        private static IPAddress address;
        private static IPAddress localAddress;

        /// <summary>
        /// Gets the public-facing IP address of the server, if known
        /// </summary>
        public IPAddress Address
        {
            get
            {
                try
                {
                    if (address == null)
                    {
                        if (Utility.ValidateIPv4(ConVar.Server.ip) && !Utility.IsLocalIP(ConVar.Server.ip))
                        {
                            IPAddress.TryParse(ConVar.Server.ip, out address);
                            Interface.uMod.LogInfo($"IP address from command-line: {address}");
                        }
                        else if (Global.SteamServer != null && Global.SteamServer.IsValid && Global.SteamServer.PublicIp != null)
                        {
                            address = Global.SteamServer.PublicIp;
                            Interface.uMod.LogInfo($"IP address from Steam query: {address}");
                        }
                        else
                        {
                            WebClient webClient = new WebClient();
                            IPAddress.TryParse(webClient.DownloadString("http://api.ipify.org"), out address);
                            Interface.uMod.LogInfo($"IP address from external API: {address}");
                        }
                    }

                    return address;
                }
                catch (Exception ex)
                {
                    RemoteLogger.Exception("Couldn't get server's public IP address", ex);
                    return IPAddress.Any;
                }
            }
        }

        /// <summary>
        /// Gets the local IP address of the server, if known
        /// </summary>
        public IPAddress LocalAddress
        {
            get
            {
                try
                {
                    return localAddress ?? (localAddress = Utility.GetLocalIP());
                }
                catch (Exception ex)
                {
                    RemoteLogger.Exception("Couldn't get server's local IP address", ex);
                    return IPAddress.Any;
                }
            }
        }

        /// <summary>
        /// Gets the public-facing network port of the server, if known
        /// </summary>
        public ushort Port => (ushort)ConVar.Server.port;

        /// <summary>
        /// Gets the version or build number of the server
        /// </summary>
        public string Version => BuildInfo.Current.Build.Number;

        /// <summary>
        /// Gets the network protocol version of the server
        /// </summary>
        public string Protocol => global::Rust.Protocol.printable;

        /// <summary>
        /// Gets the language set by the server
        /// </summary>
        public CultureInfo Language => CultureInfo.InstalledUICulture;

        /// <summary>
        /// Gets the total of players currently on the server
        /// </summary>
        public int Players => BasePlayer.activePlayerList.Count;

        /// <summary>
        /// Gets/sets the maximum players allowed on the server
        /// </summary>
        public int MaxPlayers
        {
            get => ConVar.Server.maxplayers;
            set => ConVar.Server.maxplayers = value;
        }

        /// <summary>
        /// Gets/sets the current in-game time on the server
        /// </summary>
        public DateTime Time
        {
            get => TOD_Sky.Instance.Cycle.DateTime;
            set => TOD_Sky.Instance.Cycle.DateTime = value;
        }

        /// <summary>
        /// Gets information on the currently loaded save file
        /// </summary>
        public SaveInfo SaveInfo { get; } = SaveInfo.Create(World.SaveFileName);

        #endregion Information

        #region Administration

        /// <summary>
        /// Bans the player for the specified reason and duration
        /// </summary>
        /// <param name="id"></param>
        /// <param name="reason"></param>
        /// <param name="duration"></param>
        public void Ban(string id, string reason, TimeSpan duration = default(TimeSpan))
        {
            if (!IsBanned(id))
            {
                ServerUsers.Set(ulong.Parse(id), ServerUsers.UserGroup.Banned, Name, reason);
                ServerUsers.Save();

                // TODO: Implement universal ban storage
            }
        }

        /// <summary>
        /// Gets the amount of time remaining on the player's ban
        /// </summary>
        /// <param name="id"></param>
        public TimeSpan BanTimeRemaining(string id) => IsBanned(id) ? TimeSpan.MaxValue : TimeSpan.Zero;

        /// <summary>
        /// Gets if the player is banned
        /// </summary>
        /// <param name="id"></param>
        public bool IsBanned(string id) => ServerUsers.Is(ulong.Parse(id), ServerUsers.UserGroup.Banned);

        /// <summary>
        /// Saves the server and any related information
        /// </summary>
        public void Save()
        {
            ConVar.Server.save(null);
            File.WriteAllText(Path.Combine(ConVar.Server.GetServerFolder("cfg"), "serverauto.cfg"), ConsoleSystem.SaveToConfigString(true));
            ServerUsers.Save();
        }

        /// <summary>
        /// Unbans the player
        /// </summary>
        /// <param name="id"></param>
        public void Unban(string id)
        {
            if (IsBanned(id))
            {
                ServerUsers.Remove(ulong.Parse(id));
                ServerUsers.Save();

                // TODO: Implement universal ban storage
            }
        }

        #endregion Administration

        #region Chat and Commands

        /// <summary>
        /// Broadcasts the specified chat message and prefix to all players
        /// </summary>
        /// <param name="message"></param>
        /// <param name="prefix"></param>
        /// <param name="args"></param>
        public void Broadcast(string message, string prefix, params object[] args)
        {
            if (!string.IsNullOrEmpty(message))
            {
                ulong avatarId = args.Length > 0 && args[0].IsSteamId() ? (ulong)args[0] : 0ul;
                message = args.Length > 0 ? string.Format(Formatter.ToUnity(message), avatarId != 0ul ? args.Skip(1) : args) : Formatter.ToUnity(message);
                string formatted = prefix != null ? $"{prefix}: {message}" : message;
                ConsoleNetwork.BroadcastToAllClients("chat.add", avatarId, formatted, 1.0);
            }
        }

        /// <summary>
        /// Broadcasts the specified chat message to all players
        /// </summary>
        /// <param name="message"></param>
        public void Broadcast(string message) => Broadcast(message, null);

        /// <summary>
        /// Runs the specified server command
        /// </summary>
        /// <param name="command"></param>
        /// <param name="args"></param>
        public void Command(string command, params object[] args)
        {
            ConsoleSystem.Run(ConsoleSystem.Option.Server, command, args);
        }

        #endregion Chat and Commands
    }
}
