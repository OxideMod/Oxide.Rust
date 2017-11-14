using Oxide.Core.Libraries;
using Oxide.Game.Rust.Libraries.Covalence;

namespace Oxide.Game.Rust.Libraries
{
    public class Item : Library
    {
        // Covalence references
        internal static readonly RustCovalenceProvider Covalence = RustCovalenceProvider.Instance;

        /// <summary>
        /// Gets item based on item ID
        /// </summary>
        /// <param name="itemId"></param>
        public static global::Item GetItem(int itemId) => ItemManager.CreateByItemID(itemId);
    }
}
