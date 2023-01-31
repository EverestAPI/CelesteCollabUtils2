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
            spritePath = "decals/1-forsakencity/bench_concrete",
            spriteFlipX = false,
            activateSpritePath = "CollabUtils2/characters/benchSit",
            activateSpriteFlipX = false,
            playerFacing = 1,
            interactOffsetY = -16,
        }
    }
}

lobbyMapWarp.texture = "decals/1-forsakencity/bench_concrete"

return lobbyMapWarp
