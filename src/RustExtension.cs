using Oxide.Core;
using Oxide.Core.Extensions;
using Oxide.Core.Unity;
using Oxide.Plugins;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Oxide.Game.Rust
{
    /// <summary>
    /// The extension class that represents this extension
    /// </summary>
    public class RustExtension : Extension
    {
        internal static Assembly Assembly = Assembly.GetExecutingAssembly();
        internal static AssemblyName AssemblyName = Assembly.GetName();
        internal static VersionNumber AssemblyVersion = new VersionNumber(AssemblyName.Version.Major, AssemblyName.Version.Minor, AssemblyName.Version.Build);
        internal static string AssemblyAuthors = ((AssemblyCompanyAttribute)Attribute.GetCustomAttribute(Assembly, typeof(AssemblyCompanyAttribute), false)).Company;

        /// <summary>
        /// Gets whether this extension is for a specific game
        /// </summary>
        public override bool IsGameExtension => true;

        /// <summary>
        /// Gets the name of this extension
        /// </summary>
        public override string Name => "Rust";

        /// <summary>
        /// Gets the author of this extension
        /// </summary>
        public override string Author => AssemblyAuthors;

        /// <summary>
        /// Gets the version of this extension
        /// </summary>
        public override VersionNumber Version => AssemblyVersion;

        /// <summary>
        /// Gets the branch of this extension
        /// </summary>
        public override string Branch => "public"; // TODO: Handle this programmatically

        /// <summary>
        /// Default game-specific references for use in plugins
        /// </summary>
        public override string[] DefaultReferences => new[]
        {
            "ApexAI", "ApexShared", "Facepunch.Network", "Facepunch.Steamworks.Posix64", "Facepunch.Steamworks.Win64", "Facepunch.System", "Facepunch.UnityEngine", "NewAssembly",
            "Rust.Data", "Rust.Global", "Rust.Localization", "Rust.Workshop", "Rust.World", "System.Drawing", "UnityEngine.AIModule", "UnityEngine.AssetBundleModule",
            "UnityEngine.CoreModule", "UnityEngine.GridModule", "UnityEngine.ImageConversionModule", "UnityEngine.Networking", "UnityEngine.PhysicsModule", "UnityEngine.TerrainModule",
            "UnityEngine.TerrainPhysicsModule", "UnityEngine.UI", "UnityEngine.UIModule", "UnityEngine.UIElementsModule", "UnityEngine.UnityWebRequestAudioModule",
            "UnityEngine.UnityWebRequestModule", "UnityEngine.UnityWebRequestTextureModule", "UnityEngine.UnityWebRequestWWWModule", "UnityEngine.VehiclesModule",
            "UnityEngine.WebModule"
        };

        /// <summary>
        /// List of assemblies allowed for use in plugins
        /// </summary>
        public override string[] WhitelistAssemblies => new[]
        {
            "Assembly-CSharp", "Assembly-CSharp-firstpass", "DestMath", "Facepunch.Network", "Facepunch.System", "Facepunch.UnityEngine", "mscorlib",
            "Oxide.Core", "Oxide.Rust", /* < Needed for non-C# plugins for some reason */ "RustBuild", "Rust.Data", "Rust.Global", "Rust.Localization",
            "System", "System.Core", "UnityEngine"
        };

        /// <summary>
        /// List of namespaces allowed for use in plugins
        /// </summary>
        public override string[] WhitelistNamespaces => new[]
        {
            "ConVar", "Dest", "Facepunch", "Network", "Oxide.Game.Rust.Cui", "ProtoBuf", "PVT", "Rust", "Steamworks", "System.Collections",
            "System.Security.Cryptography", "System.Text", "UnityEngine"
        };

        /// <summary>
        /// List of filter matches to apply to console output
        /// </summary>
        public static string[] Filter =
        {
            "alphamapResolution is clamped to the range of",
            "AngryAnt Behave version",
            "Floating point textures aren't supported on this device",
            "HDR RenderTexture format is not supported on this platform.",
            "Image Effects are not supported on this platform.",
            "Missing projectileID",
            "Motion vectors not supported on a platform that does not support",
            "The image effect Main Camera",
            "The image effect effect -",
            "Unable to find shaders",
            "Unsupported encoding: 'utf8'",
            "Warning, null renderer for ScaleRenderer!",
            "[AmplifyColor]",
            "[AmplifyOcclusion]",
            "[CoverageQueries] Disabled due to unsupported",
            "[CustomProbe]",
            "[Manifest] URI IS",
            "[SpawnHandler] populationCounts"
        };

        /// <summary>
        /// Initializes a new instance of the RustExtension class
        /// </summary>
        /// <param name="manager"></param>
        public RustExtension(ExtensionManager manager) : base(manager)
        {
        }

        /// <summary>
        /// Loads this extension
        /// </summary>
        public override void Load()
        {
            Manager.RegisterLibrary("Rust", new Libraries.Rust());
            Manager.RegisterLibrary("Command", new Libraries.Command());
            Manager.RegisterLibrary("Item", new Libraries.Item());
            Manager.RegisterLibrary("Player", new Libraries.Player());
            Manager.RegisterLibrary("Server", new Libraries.Server());
            Manager.RegisterPluginLoader(new RustPluginLoader());

            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                Cleanup.Add("Facepunch.Steamworks.Win64.dll"); // TODO: Remove after a few updates
            }
        }

        /// <summary>
        /// Loads plugin watchers used by this extension
        /// </summary>
        /// <param name="directory"></param>
        public override void LoadPluginWatchers(string directory)
        {
        }

        /// <summary>
        /// Called when all other extensions have been loaded
        /// </summary>
        public override void OnModLoad()
        {
            CSharpPluginLoader.PluginReferences.UnionWith(DefaultReferences);
            //Facepunch.Output.OnMessage += HandleLog;
        }

        private static void HandleLog(string message, string stackTrace, LogType logType)
        {
            if (!string.IsNullOrEmpty(message) && !Filter.Any(message.Contains))
            {
                Interface.Oxide.RootLogger.HandleMessage(message, stackTrace, logType.ToLogType());
            }
        }
    }
}
