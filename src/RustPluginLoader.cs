using Oxide.Core.Plugins;
using System;

namespace Oxide.Game.Rust
{
    /// <summary>
    /// Responsible for loading Rust core plugins
    /// </summary>
    public class RustPluginLoader : PluginLoader
    {
        public override Type[] CorePlugins => new[] { typeof(RustCore) };
    }
}
