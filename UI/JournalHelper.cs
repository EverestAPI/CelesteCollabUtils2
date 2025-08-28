using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.CollabUtils2.UI {
    public static class JournalHelper {
        private static Dictionary<string, Action<bool, List<OuiJournalPage>>> journalPageEditors = new Dictionary<string, Action<bool, List<OuiJournalPage>>>();

        internal static bool VanillaJournal = true; // default to vanilla journal
        internal static bool ShowOnlyDiscovered = false;

        public static void AddJournalPageEditor(string collabID, Action<bool, List<OuiJournalPage>> editor) {
            if (journalPageEditors.TryGetValue(collabID, out _))
                journalPageEditors[collabID] = editor;
            else
                journalPageEditors.Add(collabID, editor);
        }
        
        public static void RemoveJournalPageEditor(string collabID) {
            if (journalPageEditors.TryGetValue(collabID, out _))
                journalPageEditors.Remove(collabID);
        }
        
        internal static void Load() {
            journalPageEditors.Clear();
            
            Everest.Events.Journal.OnEnter += OnJournalEnter;
        }

        internal static void Unload() {
            Everest.Events.Journal.OnEnter -= OnJournalEnter;
        }

        private static void OnJournalEnter(OuiJournal journal, Oui from) {
            // if using the vanilla journal, we just don't have anything to do, since vanilla already did everything for us!
            if (VanillaJournal)
                return;

            // get current area
            AreaData forceArea = new DynData<Overworld>(journal.Overworld).Get<AreaData>("collabInGameForcedArea");
            if (forceArea == null)
                return;
            
            // custom journal: throw away all pages.
            journal.Pages.Clear();

            // add the cover with stickers.
            journal.Pages.Add(new OuiJournalCoverWithStickers(journal));

            // then, fill in the journal with our custom pages.
            journal.Pages.AddRange(OuiJournalCollabProgressInLobby.GeneratePages(journal, forceArea.LevelSet, ShowOnlyDiscovered));

            // and add the map if we have it as well.
            if (MTN.Journal.Has("collabLobbyMaps/" + forceArea.LevelSet))
                journal.Pages.Add(new OuiJournalLobbyMap(journal, MTN.Journal["collabLobbyMaps/" + forceArea.LevelSet]));

            // apply custom page editing
            if (journalPageEditors.TryGetValue(LobbyHelper.GetCollabNameForSID(forceArea.SID), out Action<bool, List<OuiJournalPage>> collabJournalPageEditor))
                collabJournalPageEditor(ShowOnlyDiscovered, journal.Pages);

            // if necessary, redraw the first page to include the stickers
            if (journal.Pages.ElementAtOrDefault(0) is OuiJournalCoverWithStickers coverWithStickers)
                coverWithStickers.Redraw(journal.CurrentPageBuffer);
            
            // reset journal entry data
            VanillaJournal = true;
            ShowOnlyDiscovered = false;
        }
    }
}
