local trigger = {}
trigger.name = "CollabUtils2/JournalTrigger"
trigger.nodeLimits = {0, 1}
trigger.placements = {
    {
        name = "default",
        data = {
            levelset = "Celeste",
            showOnlyDiscovered = false,
            vanillaJournal = false
        }
    }
}

return trigger
