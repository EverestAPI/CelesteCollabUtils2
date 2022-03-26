local drawableNinePatch = require("structs.drawable_nine_patch")
local drawableSprite = require("structs.drawable_sprite")

local silverBlock = {}

silverBlock.name = "CollabUtils2/SilverBlock"
silverBlock.depth = -10000
silverBlock.minimumSize = {16, 16}
silverBlock.placements = {
    name = "default",
    data = {
        width = 16,
        height = 16
    }
}

local ninePatchOptions = {
    mode = "fill",
    borderMode = "repeat",
    fillMode = "repeat"
}

local blockTexture = "CollabUtils2/silverblock"
local middleTexture = "CollabUtils2/silverBerry/idle00"

function silverBlock.sprite(room, entity)
    local x, y = entity.x or 0, entity.y or 0
    local width, height = entity.width or 24, entity.height or 24

    local ninePatch = drawableNinePatch.fromTexture(blockTexture, ninePatchOptions, x, y, width, height)
    local middleSprite = drawableSprite.fromTexture(middleTexture, entity)
    local sprites = ninePatch:getDrawableSprite()

    middleSprite:addPosition(math.floor(width / 2), math.floor(height / 2))
    table.insert(sprites, middleSprite)

    return sprites
end

return silverBlock
