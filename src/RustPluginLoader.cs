using System;
using uMod.Plugins;

namespace uMod.Rust
{
    /// <summary>
    /// Responsible for loading Rust core plugins
    /// </summary>
    public class RustPluginLoader : PluginLoader
    {
        public override Type[] CorePlugins => new[] { typeof(RustCore) };
    }
}
