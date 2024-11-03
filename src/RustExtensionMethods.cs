namespace Oxide.Plugins
{
    public static class RustExtensionMethods
    {
        public static bool IsSteamId(this BasePlayer.EncryptedValue<ulong> userID) => ((ulong)userID).IsSteamId();
    }
}
