local trigger = {}
trigger.name = "CollabUtils2/ChapterPanelTrigger"
trigger.nodeLimits = {0, 1}
trigger.placements = {
    {
        name = "default",
        data = {
            map = "Celeste/1-ForsakenCity",
            returnToLobbyMode = "SetReturnToHere",
            allowSaving = true
        }
    }
}

trigger.fieldInformation = {
    returnToLobbyMode = {
        options = { "SetReturnToHere", "RemoveReturn", "DoNotChangeReturn" },
        editable = false
    }
}

return trigger
