using Oxide.Core;
using Oxide.Core.Extensions;
using Oxide.Plugins;
using System;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

namespace Oxide.Game.Rust
{
    /// <summary>
    /// The extension class that represents this extension
    /// </summary>
    public class RustExtension : Extension
    {
        private const string OxideRustReleaseListUrl = "https://api.github.com/repos/OxideMod/Oxide.Rust/releases";
        internal static Assembly Assembly = Assembly.GetExecutingAssembly();
        internal static AssemblyName AssemblyName = Assembly.GetName();
        internal static VersionNumber AssemblyVersion = new VersionNumber(AssemblyName.Version.Major, AssemblyName.Version.Minor, AssemblyName.Version.Build);
        internal static string AssemblyAuthors = ((AssemblyCompanyAttribute)Attribute.GetCustomAttribute(Assembly, typeof(AssemblyCompanyAttribute), false)).Company;
        internal static string AssemblyBranch;

        private static readonly WebClient WebClient = new WebClient();
        private static VersionNumber LatestExtVersion = AssemblyVersion;

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
        /// Default game-specific references for use in plugins
        /// </summary>
        public override string[] DefaultReferences => new[]
        {
            "ApexAI", "ApexShared", "Facepunch.Network", "Facepunch.Steamworks.Posix64", "Facepunch.System", "Facepunch.UnityEngine", "Facepunch.Steamworks.Win64", "Rust.Data",
            "Rust.FileSystem", "Rust.Clans", "Rust.Clans.Local", "Rust.Global", "Rust.Localization", "Rust.Platform", "Rust.Platform.Common", "Rust.Platform.Steam", "Rust.Workshop",
            "Rust.World", "System.Drawing", "UnityEngine.AIModule", "UnityEngine.AssetBundleModule", "UnityEngine.CoreModule", "UnityEngine.GridModule", "UnityEngine.ImageConversionModule",
            "UnityEngine.Networking", "UnityEngine.PhysicsModule", "UnityEngine.TerrainModule", "UnityEngine.TerrainPhysicsModule", "UnityEngine.UI", "UnityEngine.UIModule",
            "UnityEngine.UIElementsModule", "UnityEngine.UnityWebRequestAudioModule","UnityEngine.UnityWebRequestModule", "UnityEngine.UnityWebRequestTextureModule",
            "UnityEngine.UnityWebRequestWWWModule", "UnityEngine.VehiclesModule", "UnityEngine.WebModule", "netstandard"
        };

        /// <summary>
        /// List of assemblies allowed for use in plugins
        /// </summary>
        public override string[] WhitelistAssemblies => new[]
        {
            "Assembly-CSharp", "Assembly-CSharp-firstpass", "DestMath", "Facepunch.Network", "Facepunch.System", "Facepunch.UnityEngine", "mscorlib",  "Oxide.Core", "Oxide.Rust",
            "RustBuild", "Rust.Data", "Rust.FileSystem", "Rust.Global", "Rust.Localization","Rust.Localization", "Rust.Platform.Common", "Rust.Platform.Steam", "System", "System.Core", "UnityEngine"
        };

        /// <summary>
        /// List of namespaces allowed for use in plugins
        /// </summary>
        public override string[] WhitelistNamespaces => new[]
        {
            "ConVar", "Dest", "Facepunch", "Network", "Oxide.Game.Rust.Cui", "ProtoBuf", "PVT", "Rust", "Steamworks", "System.Collections",
            "System.Security.Cryptography", "System.Text", "System.Threading.Monitor", "UnityEngine"
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
            AssemblyBranch = Branch ?? "master";
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

            WebClient.Headers["User-Agent"] = $"Oxide.Rust {Version}";
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
        }

        /// <summary>
        /// Gets latest official Oxide.Rust build version
        /// </summary>
        /// <param name="callback">Callback to execute.<br/>
        /// First argument is the version (latest, if request was successful, current otherwise)<br/>
        /// Second argument is the exception indicating fail reason. Null if request was successful.
        /// </param>
        public void GetLatestVersion(Action<VersionNumber, Exception> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback), "Callback cannot be null");
            }

            if (LatestExtVersion > AssemblyVersion)
            {
                callback(LatestExtVersion, null);
            }
            else
            {
                GetLatestExtensionVersion().ContinueWith(
                    task => {
                        if (task.Exception == null)
                        {
                            LatestExtVersion = task.Result;
                        }

                        callback(LatestExtVersion, task.Exception?.InnerException);
                    }
                );
            }
        }

        private async Task<VersionNumber> GetLatestExtensionVersion()
        {
            string json = await WebClient.DownloadStringTaskAsync(OxideRustReleaseListUrl);
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new Exception("Could not retrieve latest Oxide.Rust version from GitHub API");
            }

            JSON.Array releaseArray = JSON.Array.Parse(json);
            JSON.Object latest = releaseArray[0].Obj;

            string tag = latest.GetString("tag_name");

            if (string.IsNullOrWhiteSpace(tag))
            {
                throw new Exception("Tag name is undefined");
            }

            VersionNumber tagVersion = ParseVersionNumber(tag);

            return tagVersion;
        }

        private VersionNumber ParseVersionNumber(string versionString) // definitely needs a unification inside the VersionNumber itself
        {
            string[] array = versionString.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            int major = int.Parse(array[0]);
            int minor = int.Parse(array[1]);
            int patch = int.Parse(array[2]);

            return new VersionNumber(major, minor, patch);
        }
    }
}
