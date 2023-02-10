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
            heartGateIcon = "CollabUtils2/lobbies/heartgate",
            gymIcon = "CollabUtils2/lobbies/gym",
            mapIcon = "CollabUtils2/lobbies/map",
            journalIcon = "CollabUtils2/lobbies/journal",
            heartSideIcon = "CollabUtils2/lobbies/heartside",
            showWarps = true,
            showRainbowBerry = true,
            showHeartGate = true,
            showGyms = true,
            showMaps = true,
            showHeartSide = true,
            showJournals = true,
            showHeartCount = true,
            revealWhenAllMarkersFound = false,
        }
    }
}

lobbyMapController.texture = "CollabUtils2/editor_lobbymapmarker"

return lobbyMapController
