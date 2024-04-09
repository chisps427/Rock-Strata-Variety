CONCEPT:
Precompute rock strata noise distributions in the column, sum them up, and then distribute each rock group's allocated thickness (per Geologic Province) among each rock type proportionally, according to their percentage of the total noise distribution sum.
Generate rock groups in order: volcanic, sedimentary, metamorphic, and then igneous. Igneous typically dominates all other rock groups in vanilla generation, as it has a max thickness of 255 in every Geologic Province, so this allows the other rock groups to shine through. This is good for tall world heights, though.

Vanilla noise maps are unchanged. This only generates what should already be there, by preventing rock strata from interfering with each other and edging out strata that come later in the variants list in Vintagestory/assets/survival/worldgen/rockstrata.json.
        
This means that /wgen pos rockstrata should be more accurate to the actual rocks present in a column. It shows exactly what can generate given noise distributions, but is often wrong about what is there in vanilla. There may still be missing rock types, because strata less than 2 blocks thick are culled before generation. If a rock type doesn't have a high enough percentage of the noise sum to generate >= 2 blocks thick, it will be left out. This is vanilla behavior, and I left it in, as it prevents things from getting too "stripey".

NOTES:
- Geologicprovinces.json may still need tweaking to adjust the dominance of igneous rock types and allow thicker metamorphic layers (they are very thin & rare by default).
- Rockstrata.json will likely need reordering to form realistic & logical strata orders. The orders that strata generate in are deterministic --  i.e. if claystone and sandstone both generate,
  the former will always be above the latter. This ensures smooth, continuous rock layers, but some consideration should be made to their order.
- Bauxite shouldn't be metamorphic, it's sedimentary.
- Never seen Phyllite? It's honestly not that rare in my testing, it's just always buried underneath all the igneous layers because it generates Bottom-up (from the mantle),
  and it generates before every igneous rock. So, it's guaranteed to be stuck at the very bottom, 100% of the time. Worth digging for, if you like the novelty!

None of the above issues are within the scope of this mod, though. I wanted it to be as lightweight and focused as possible. I may release other json patches to address them.
        
COMPATIBILITY:
        - This patch should be fully compatible with mods that add new rock types, ores, etc. (e.g. small_fern's Geology Additions), and should greatly improve the likelihood of new rocks generating.
        - This patch *WILL* be *FULLY INCOMPATIBLE* with any mod that overwrites or modifies GenRockStrataNew.genBlockColumn(). None yet do so, as far as I know, but beware.

Apologies for messy code, I am not a professional, and this is my first time programming in C#. Let me know if I've done anything stupid, please!
