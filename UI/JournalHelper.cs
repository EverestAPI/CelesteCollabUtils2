using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.CollabUtils2.UI {
    public static class JournalHelper {
        private static Dictionary<string, Action<OuiJournal, string, bool>> journalEditors = new Dictionary<string, Action<OuiJournal, string, bool>>();

        internal static bool VanillaJournal = true; // default to vanilla journal
        internal static bool ShowOnlyDiscovered = false;

        public static void AddJournalEditor(string collabID, Action<OuiJournal, string, bool> editor) {
            if (journalEditors.TryGetValue(collabID, out _))
                journalEditors[collabID] = editor;
            else
                journalEditors.Add(collabID, editor);
        }
        
        public static void RemoveJournalEditor(string collabID) {
            if (journalEditors.TryGetValue(collabID, out _))
                journalEditors.Remove(collabID);
        }
        
        internal static void Load() {
            journalEditors.Clear();
            
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
            if (journalEditors.TryGetValue(LobbyHelper.GetCollabNameForSID(forceArea.SID), out Action<OuiJournal, string, bool> collabJournalPageEditor))
                collabJournalPageEditor(journal, forceArea.LevelSet, ShowOnlyDiscovered);

            // if necessary, redraw the first page to include the stickers
            if (journal.Pages.ElementAtOrDefault(0) is OuiJournalCoverWithStickers coverWithStickers)
                coverWithStickers.Redraw(journal.CurrentPageBuffer);
            
            // reset journal entry data
            VanillaJournal = true;
            ShowOnlyDiscovered = false;
        }
    }
}
