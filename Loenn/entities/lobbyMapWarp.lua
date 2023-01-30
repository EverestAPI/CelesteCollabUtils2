local lobbyMapWarp = {}
lobbyMapWarp.name = "CollabUtils2/LobbyMapWarp"
lobbyMapWarp.depth = -100
lobbyMapWarp.placements = {
    {
        name = "default",
        data = {
            warpId = "",
            icon = "",
            dialogKey = "",
            spritePath = "CollabUtils2/lobbyMap/warpSprite",
            spriteFlipX = false,
            activateSpritePath = "",
            activateSpriteFlipX = false,
            playerFacing = 1,
            interactOffsetY = -16,
        }
    }
}

lobbyMapWarp.texture = "CollabUtils2/rainbowBerry/rberry0030"

return lobbyMapWarp
