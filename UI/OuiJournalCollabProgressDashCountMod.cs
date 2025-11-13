using System.Runtime.CompilerServices;

namespace Celeste.Mod.CollabUtils2.UI {
    /// <summary>
    /// If Dash Count Mod is installed and enabled, it will simply mod IsDashCountEnabled() to return true instead of false,
    /// in order to enable dash count in the Collab Utils' journals.
    /// (It is simpler to implement them in Collab Utils directly, rather than hooking both journals with IL hooks.)
    /// </summary>
    public static class OuiJournalCollabProgressDashCountMod {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool IsDashCountEnabled() {
            return false;
        }

        // those depend on Dash Count Mod settings / save data, and will be implemented by Dash Count Mod itself.

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool DisplaysTotalDashes() {
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int GetLevelDashesForJournalProgress(AreaStats stats) {
            return stats.BestTotalDashes;
        }
    }
}
