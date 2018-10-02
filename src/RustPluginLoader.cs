using System;
using uMod.Plugins;

namespace uMod.Rust
{
    /// <summary>
    /// Responsible for loading the core Rust plugin
    /// </summary>
    public class RustPluginLoader : PluginLoader
    {
        public override Type[] CorePlugins => new[] { typeof(Rust) };
    }
}
