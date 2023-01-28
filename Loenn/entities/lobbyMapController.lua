local lobbyMapController = {}
lobbyMapController.name = "CollabUtils2/LobbyMapController"
lobbyMapController.depth = -100
lobbyMapController.placements = {
    {
        name = "default",
        data = {
            mapTexture = "SJ2021/1-Beginner/beginnermap",
            levelSet = "StrawberryJam2021/1-Beginner",
            lobbyIndex = 1,
            totalMaps = 21,
            explorationRadius = 20,
            zoomLevels = "1,2,3",
            defaultZoomLevel = 1,
            customFeatures = "",
            roomWidth = 0,
            roomHeight = 0,
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

lobbyMapController.texture = "CollabUtils2/rainbowBerry/rberry0030"

return lobbyMapController
