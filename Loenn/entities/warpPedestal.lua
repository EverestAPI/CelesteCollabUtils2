local pedestal = {}

pedestal.name = "CollabUtils2/WarpPedestal"
pedestal.depth = 1000
pedestal.texture = "CollabUtils2/placeholderorb/placeholderorb00"
pedestal.justification = { 0.5, 0.95 }

pedestal.placements = {
    name = "pedestal",
    data = {
        map = "Celeste/1-ForsakenCity",
        sprite = "CollabUtils2_placeholderOrb",
        returnToLobbyMode = "SetReturnToHere",
        allowSaving = true,
        fillSoundEffect = "",
        bubbleOffsetY = 16
    }
}

pedestal.fieldInformation = {
    bubbleOffsetY = {
        fieldType = "integer"
    },
    returnToLobbyMode = {
        options = { "SetReturnToHere", "RemoveReturn", "DoNotChangeReturn" },
        editable = false
    }
}

return pedestal
