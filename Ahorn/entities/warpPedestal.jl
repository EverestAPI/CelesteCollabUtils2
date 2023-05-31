module CollabUtils2WarpPedestal

using ..Ahorn, Maple

@mapdef Entity "CollabUtils2/WarpPedestal" WarpPedestal(x::Integer, y::Integer, map::String="Celeste/1-ForsakenCity", sprite::String="CollabUtils2_placeholderOrb",
    returnToLobbyMode::String="SetReturnToHere", allowSaving::Bool=true, fillSoundEffect::String="", bubbleOffsetY::Integer=16)

const placements = Ahorn.PlacementDict(
    "Warp Pedestal (Collab Utils 2)" => Ahorn.EntityPlacement(
        WarpPedestal
    )
)

Ahorn.editingOptions(entity::WarpPedestal) = Dict{String, Any}(
    "returnToLobbyMode" => String["SetReturnToHere", "RemoveReturn", "DoNotChangeReturn"]
)

function Ahorn.selection(entity::WarpPedestal)
    x, y = Ahorn.position(entity)

    return Ahorn.getSpriteRectangle("CollabUtils2/placeholderorb/placeholderorb00", x, y, jx=0.5, jy=0.92)
end

function Ahorn.render(ctx::Ahorn.Cairo.CairoContext, entity::WarpPedestal)
    Ahorn.drawSprite(ctx, "CollabUtils2/placeholderorb/placeholderorb00", 0, 0, jx=0.5, jy=0.92)
end

end
