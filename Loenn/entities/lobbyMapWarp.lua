local drawableSprite = require("structs.drawable_sprite")
local celesteEnums = require("consts.celeste_enums")

local lobbyMapWarp = {}
lobbyMapWarp.name = "CollabUtils2/LobbyMapWarp"
lobbyMapWarp.fieldInformation = {
    playerFacing = {
        options = celesteEnums.spawn_facing_trigger_facings,
        editable = false,
    },
    interactOffsetY = {
        fieldType = "integer",
    },
    wipeType = {
        options = celesteEnums.wipe_names,
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
            playerFacing = "Right",
            interactOffsetY = -16,
            depth = 2000,
            wipeType = celesteEnums.wipe_names.Mountain,
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
