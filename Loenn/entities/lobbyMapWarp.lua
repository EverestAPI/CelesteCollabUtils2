local drawableSprite = require("structs.drawable_sprite")

local lobbyMapWarp = {}
lobbyMapWarp.name = "CollabUtils2/LobbyMapWarp"
lobbyMapWarp.fieldInformation = {
    playerFacing = {
        fieldType = "integer",
        options = {
            { "Left", -1 }, { "Right", 1 }
        }
    },
    interactOffsetY = {
        fieldType = "integer",
    }
}
lobbyMapWarp.placements = {
    {
        name = "default",
        data = {
            warpId = "",
            icon = "",
            dialogKey = "",
            warpSpritePath = "decals/1-forsakencity/bench_concrete",
            warpSpriteFlipX = false,
            playActivateSprite = true,
            activateSpriteFlipX = false,
            playerFacing = 1,
            interactOffsetY = -16,
            depth = 2000,
        }
    }
}

function lobbyMapWarp.sprite(room, entity)
    local spritePath = entity.warpSpritePath or "decals/1-forsakencity/bench_concrete"
    local sprite = drawableSprite.fromTexture(spritePath, entity)
    sprite:setJustification(0.5, 1.0)
    sprite:setScale(entity.spriteFlipX and -1 or 1, 1)
    return sprite
end

function lobbyMapWarp.depth(room, entity)
    return entity.depth
end

return lobbyMapWarp
