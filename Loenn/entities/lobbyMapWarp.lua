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
            activateSpritePath = "CollabUtils2/characters/sitBench",
            activateSpriteFlipX = false,
            playerFacing = 1,
            interactOffsetY = -16,
        }
    }
}

lobbyMapWarp.texture = "decals/1-forsakencity/bench_concrete"
lobbyMapWarp.justification = {0.5, 1.0}

return lobbyMapWarp
