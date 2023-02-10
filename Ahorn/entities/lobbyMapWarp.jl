module CollabUtils2LobbyMapWarp

using ..Ahorn, Maple

const default_sprite = "decals/1-forsakencity/bench_concrete"

@mapdef Entity "CollabUtils2/LobbyMapWarp" LobbyMapWarp(
    x::Integer, y::Integer,
    warpId::String="", icon::String="", dialogKey::String="",
    warpSpritePath::String=default_sprite, warpSpriteFlipX::Bool=false,
    playActivateSprite::Bool=true, activateSpriteFlipX::Bool=false,
    playerFacing::String="Right", interactOffsetY::Integer=-16, depth::Integer=2000,
    wipeType::String="Mountain", warpRequiresActivation::Bool=false
)

const placements = Ahorn.PlacementDict(
    "Lobby Map Warp (Collab Utils 2)" => Ahorn.EntityPlacement(
        LobbyMapWarp
    )
)

Ahorn.editingOptions(entity::LobbyMapWarp) = Dict{String, Any}(
    "playerFacing" => sort(Maple.spawn_facing_trigger_facings),
    "wipeType" => Maple.wipe_names
)

function Ahorn.selection(entity::LobbyMapWarp)
    x, y = Ahorn.position(entity)
    sprite = get(entity.data, "warpSpritePath", default_sprite)
    flipX = get(entity.data, "warpSpriteFlipX", false)
    return Ahorn.getSpriteRectangle(sprite, x, y, jx=0.5, jy=1.0)
end

function Ahorn.render(ctx::Ahorn.Cairo.CairoContext, entity::LobbyMapWarp, room::Maple.Room)
    x, y = Ahorn.position(entity)
    sprite = get(entity.data, "warpSpritePath", default_sprite)
    flipX = get(entity.data, "warpSpriteFlipX", false)
    Ahorn.drawSprite(ctx, sprite, 0, 0, sx=flipX ? -1 : 1, jx=0.5, jy=1.0)
end

end