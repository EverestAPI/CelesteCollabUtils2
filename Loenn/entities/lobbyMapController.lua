local lobbyMapController = {}
lobbyMapController.name = "CollabUtils2/LobbyMapController"
lobbyMapController.depth = -100
lobbyMapController.fieldInformation = {
    totalMaps = {
        fieldType = "integer",
    }
}
lobbyMapController.placements = {
    {
        name = "default",
        data = {
            mapTexture = "",
            totalMaps = 10,
            customMarkers = "",
            warpIcon = "CollabUtils2/lobbies/warp",
            rainbowBerryIcon = "CollabUtils2/lobbies/rainbowBerry",
            heartDoorIcon = "CollabUtils2/lobbies/heartgate",
            gymIcon = "CollabUtils2/lobbies/gym",
            mapIcon = "CollabUtils2/lobbies/map",
            journalIcon = "CollabUtils2/lobbies/journal",
            showWarps = true,
            showRainbowBerry = true,
            showHeartDoor = true,
            showGyms = true,
            showMaps = true,
            showJournals = true,
            showHeartCount = true,
        }
    }
}

lobbyMapController.texture = "CollabUtils2/editor_lobbymapmarker"

return lobbyMapController
