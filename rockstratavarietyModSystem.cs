/*
*    CONCEPT:
*        Precompute rock strata noise distributions in the column, sum them up, and then distribute each rock group's allocated thickness (per Geologic Province)
*        among each rock type proportionally, according to their percentage of the total noise distribution sum.
*
*        Generate rock groups in order: volcanic, sedimentary, metamorphic, and then igneous. Igneous typically dominates all other rock groups in vanilla generation,
*        as it has a max thickness of 255 in every Geologic Province, so this allows the other rock groups to shine through. This is good for tall world heights, though.
*
*        Vanilla noise maps are unchanged. This only generates what should already be there, by preventing rock strata from interfering with each other
*        and edging out strata that come later in the variants list in Vintagestory/assets/survival/worldgen/rockstrata.json.
*        
*        This means that /wgen pos rockstrata should be more accurate to the actual rocks present in a column. It shows exactly what can generate given noise distributions,
*        but is often wrong about what is there in vanilla. There may still be missing rock types, because strata less than 2 blocks thick are culled before generation.
*        If a rock type doesn't have a high enough percentage of the noise sum to generate >= 2 blocks thick, it will be left out. 
*        This is vanilla behavior, and I left it in, as it prevents things from getting too "stripey".
*
*    NOTES:
*        - Geologicprovinces.json may still need tweaking to adjust the dominance of igneous rock types and allow thicker metamorphic layers (they are very thin & rare by default).
*        - Rockstrata.json will likely need reordering to form realistic & logical strata orders. The orders that strata generate in are deterministic --  i.e. if claystone and sandstone both generate,
*            the former will always be above the latter. This ensures smooth, continuous rock layers, but some consideration should be made to their order.
*        - Bauxite shouldn't be metamorphic, it's sedimentary.
*        - Never seen Phyllite? It's honestly not that rare in my testing, it's just always buried underneath all the igneous layers because it generates Bottom-up (from the mantle),
*            and it generates before every igneous rock. So, it's guaranteed to be stuck at the very bottom, 100% of the time. Worth digging for, if you like the novelty!
*
*        None of the above issues are within the scope of this mod, though. I wanted it to be as lightweight and focused as possible. I may release other json patches to address them.
*        
*    COMPATIBILITY:
*        - This patch should be fully compatible with mods that add new rock types, ores, etc. (e.g. small_fern's Geology Additions), and should greatly improve the likelihood of new rocks generating.
*        - This patch *WILL* be *FULLY INCOMPATIBLE* with any mod that overwrites or modifies GenRockStrataNew.genBlockColumn(). None yet do so, as far as I know, but beware.
*
*        Apologies for messy code, I am not a professional, and this is my first time programming in C#. Let me know if I've done anything stupid, please!
*
*    By: chisps
*/

using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;
using System;
using HarmonyLib;
using Vintagestory.ServerMods;
using Vintagestory.ServerMods.NoObf;


namespace rockstratavariety
{

    [HarmonyPatch]
    public class rockstratavarietyModSystem : ModSystem
    {
        public static ICoreAPI api;
        public Harmony harmony;

        // keep track of which ID's are which rock group
        internal static System.Collections.Generic.List<int>[] rockIdsByGroup = null;

        public override double ExecuteOrder()
        {
            return 0.5; // load after block and item loader, so that all rock block types have been registered by the time the rock type directory is built
        }

        private static void buildRockTypeDirectory(RockStrataConfig strata)
        {
            //rock strata by group (sed = 0, met = 1, ig = 2, volc = 3)
            rockIdsByGroup = new System.Collections.Generic.List<int>[4];
            rockIdsByGroup[0] = new System.Collections.Generic.List<int>();
            rockIdsByGroup[1] = new System.Collections.Generic.List<int>();
            rockIdsByGroup[2] = new System.Collections.Generic.List<int>();
            rockIdsByGroup[3] = new System.Collections.Generic.List<int>();

            RockStratum rockStratum = null;

            for (int strataId = 0; strataId < strata.Variants.Length; strataId++)
            {
                rockStratum = strata.Variants[strataId];
                int rockGroup = (int)rockStratum.RockGroup;
                rockIdsByGroup[rockGroup].Add(strataId);
            }

            api.Logger.Event("[RockStrataVariety] Built rock directory: {0} variants", strata.Variants.Length);
        }

        // Called on server and client
        // Useful for registering block/entity classes on both sides
        public override void Start(ICoreAPI api)
        {
            api.Logger.Notification("Attempting rockstrata generation patch for: " + api.Side);

            rockstratavarietyModSystem.api = api;
            // The mod is started once for the server and once for the client.
            // Prevent the patches from being applied by both in the same process.
            if (!Harmony.HasAnyPatches(Mod.Info.ModID))
            {
                harmony = new Harmony(Mod.Info.ModID);
                api.Logger.Event("Applying rockstrata generation patch.");
                harmony.PatchAll(); // Applies all harmony patches
            }
            else api.Logger.Event("Rockstrata generation patch not applied for this side (this is expected for Client side!)");
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GenRockStrataNew), "genBlockColumn")]
        public static bool blockColumnChanged(GenRockStrataNew __instance, IServerChunk[] chunks, int chunkX, int chunkZ, int lx, int lz
            // fields
            // for writeable, use ref keyword
            ,
            ushort[] ___heightMap,
            ref float[] ___rockGroupMaxThickness,
            ref float[] ___rockGroupCurrentThickness,
            GeologicProvinces ___provinces,
            ref LerpedWeightedIndex2DMap ___map,
            float ___chunkInRegionX,
            float ___lerpMapInv,
            float ___chunkInRegionZ,
            SimplexNoise ___distort2dx,
            SimplexNoise ___distort2dz,
            RockStrataConfig ___strata,
            IMapChunk ___mapChunk,
            int ___regionChunkSize,
            int ___rdx,
            int ___rdz

            )
        { // For methods, use __instance to obtain the caller object

            if (rockIdsByGroup == null)
            {
                // build the Rock directory on the first run
                buildRockTypeDirectory(___strata);
            }

            // this stuff is unchanged from vanilla implementation
            int num = (int)___heightMap[lz * 32 + lx];
            int ylower = 1;
            int yupper = num;
            int rockBlockId = __instance.rockBlockId;
            ___rockGroupMaxThickness[0] = (___rockGroupMaxThickness[1] = (___rockGroupMaxThickness[2] = (___rockGroupMaxThickness[3] = 0f)));
            ___rockGroupCurrentThickness[0] = (___rockGroupCurrentThickness[1] = (___rockGroupCurrentThickness[2] = (___rockGroupCurrentThickness[3] = 0)));
            float[] indices = new float[___provinces.Variants.Length];
            ___map.WeightsAt(___chunkInRegionX + (float)lx * ___lerpMapInv, ___chunkInRegionZ + (float)lz * ___lerpMapInv, indices);
            for (int i = 0; i < indices.Length; i++)
            {
                float w = indices[i];
                if (w != 0f)
                {
                    GeologicProvinceRockStrata[] localstrata = ___provinces.Variants[i].RockStrataIndexed;
                    ___rockGroupMaxThickness[0] += localstrata[0].ScaledMaxThickness * w;
                    ___rockGroupMaxThickness[1] += localstrata[1].ScaledMaxThickness * w;
                    ___rockGroupMaxThickness[2] += localstrata[2].ScaledMaxThickness * w;
                    ___rockGroupMaxThickness[3] += localstrata[3].ScaledMaxThickness * w;
                }
            }
            float distx = (float)___distort2dx.Noise((double)(chunkX * 32 + lx), (double)(chunkZ * 32 + lz));
            float distz = (float)___distort2dz.Noise((double)(chunkX * 32 + lx), (double)(chunkZ * 32 + lz));
            float thicknessDistort = GameMath.Clamp((distx + distz) / 30f, 0.9f, 1.1f);

            // generate strataThicknesses for each rock stratum, normalized to the rockGroupMaxThickness for that stratum.
            // this way, a particular stratum cannot dominate the whole layer just because it comes first in order in variants.
            // two or more strata with a high perlin noise value at a given chunk will be forced to share the rockGroup layer proportionally.

            // 1. rock strata by group (sed = 0, met = 1, ig = 2, volc = 3)
            // 2. sum up total noise thicknesses of each rock type per group
            RockStratum rockStratum = null;
            float[] rockGroupNoiseThickness = new float[4];
            rockGroupNoiseThickness[0] = (rockGroupNoiseThickness[1] = (rockGroupNoiseThickness[2] = (rockGroupNoiseThickness[3] = 0)));

            float[] strataWeightedThickness = new float[___strata.Variants.Length];

            for (int strataId = 0; strataId < ___strata.Variants.Length; strataId++)
            {
                IntDataMap2D wrockMap = ___mapChunk.MapRegion.RockStrata[strataId];
                float wstep = (float)wrockMap.InnerSize / (float)___regionChunkSize;
                var nx = (float)___rdx * wstep + wstep * ((float)lx + distx) / 32f;
                var nz = (float)___rdx * wstep + wstep * ((float)lz + distx) / 32f;

                // 1.20 value clamping for chunk borders (idk if this is necessary but adding it for consistency)
                //nx = Math.Max(nx, -1.499f);
                //nz = Math.Max(nz, -1.499f);

                float noiseThickness = wrockMap.GetIntLerpedCorrectly(nx, nz);
                strataWeightedThickness[strataId] = noiseThickness; // this strataId's genned thickness, per noise map

                rockStratum = ___strata.Variants[strataId];
                int rockGroup = (int)rockStratum.RockGroup;

                rockGroupNoiseThickness[rockGroup] += noiseThickness; // total noise genned thickness for this rockGroup
            }

            // 2.5. if rockGroupNoiseThickness doesn't equal or exceed rockGroupMaxThickness*thicknessDistort, (i.e. this is a basin geoprovince, but perlin noise only lets like 15 blocks of sediment generate)
            //      need to adjust so maximum *allowed* thickness doesn't always generate.

            float[] rockGroupAdjMaxThickness = new float[4];

            for (int i = 0; i < 4; i++)
            {
                rockGroupAdjMaxThickness[i] = Math.Min(rockGroupNoiseThickness[i], ___rockGroupMaxThickness[i] * thicknessDistort);
            }

            // adjust maximum thickness for igneous to be ~= remaining height after sed, met, and volc generate, so igneous can have variety too
            int totalColumnHeight = yupper - ylower;
            float remainingHeight = (float)totalColumnHeight - (rockGroupAdjMaxThickness[0] + rockGroupAdjMaxThickness[1] + rockGroupAdjMaxThickness[3]);
            remainingHeight *= 1.2f; // 20% more, because remaining height is not an exact measure of the room remaining, as <2 thick rock types are culled during generation. otherwise leaves a strip of granite (non-generated rock) in the middle
            rockGroupAdjMaxThickness[2] = Math.Min(remainingHeight, rockGroupAdjMaxThickness[2]);

            // 3. rock type thickness / rockGroupNoiseThicness = rockTypePercentageOfGroup
            // 4. weightedThickness = rockTypePercentageOfGroup * rockGroupAdjMaxThickness[group]

            for (int strataId = 0; strataId < ___strata.Variants.Length; strataId++)
            {
                rockStratum = ___strata.Variants[strataId];
                int rockGroup = (int)rockStratum.RockGroup;

                float rockTypePercentageOfGroup = strataWeightedThickness[strataId] / rockGroupNoiseThickness[rockGroup];

                float weightedThickness = rockTypePercentageOfGroup * rockGroupAdjMaxThickness[rockGroup];

                if (float.IsNaN(weightedThickness))
                {
                    strataWeightedThickness[strataId] = 0;
                }
                else strataWeightedThickness[strataId] = weightedThickness;
            }

            // generate rock groups in order. First do volcanic, then sedimentary, then metamorphic, and then let igneous fill in the remaining gaps.
            // use each strata's strataWeightedThickness to generate. Should already be scaled to rockGroupMaxThickness, so no need to check against that at every step,
            // or to subtract rockGroupCurrentThickness. Will still do that though just to deal with floating point stuff.

            // 0. Volcanic, 1. Sedimentary, 2. Metamorphic, 3. Igneous
            // rock strata by group: sed = 0, met = 1, ig = 2, volc = 3
            // so do 3 first, then 0,1,2

            int[] genOrder = new int[4];
            genOrder[0] = 3;    // volcanic
            genOrder[1] = 0;    // sedimentary
            genOrder[2] = 1;    // metamorphic
            genOrder[3] = 2;    // igneous

            int rockStrataId = -1;
            RockStratum stratum = null;
            int grp = 0;
            float strataThickness = 0f;

            bool finishedColumn = false;

            for (int genIndex = 0; genIndex < 4; genIndex++)
            {
                grp = genOrder[genIndex];

                for (int groupIndex = 0; groupIndex < rockIdsByGroup[grp].Count; groupIndex++)
                {
                    if (ylower > yupper)
                    {
                        finishedColumn = true;
                        break;
                    }

                    rockStrataId = rockIdsByGroup[grp][groupIndex];

                    stratum = ___strata.Variants[rockStrataId];
                    strataThickness = Math.Min(___rockGroupMaxThickness[grp] * thicknessDistort - (float)___rockGroupCurrentThickness[grp], strataWeightedThickness[rockStrataId]);

                    if (stratum.RockGroup == EnumRockGroup.Sedimentary)
                    {
                        strataThickness -= (float)Math.Max(0, yupper - TerraGenConfig.seaLevel) * 0.5f;
                    }

                    if (strataThickness < 2f)
                    {
                        strataThickness = -1f;
                        continue; // skip this one, it's too thin
                    }

                    // now gen this Id up to strataThickness, but stop if ylower > yupper
                    for (int i = 0; i < (int)strataThickness; i++)
                    {
                        if (ylower > yupper)
                        {
                            finishedColumn = true;
                            break;
                        }

                        ___rockGroupCurrentThickness[grp]++;

                        if (stratum.GenDir == EnumStratumGenDir.BottomUp)
                        {
                            int chunkY = ylower / 32;
                            int lY = ylower - chunkY * 32;
                            int localIndex3D = (32 * lY + lz) * 32 + lx;
                            IChunkBlocks chunkBlockData = chunks[chunkY].Data;
                            if (chunkBlockData.GetBlockIdUnsafe(localIndex3D) == rockBlockId)
                            {
                                chunkBlockData.SetBlockUnsafe(localIndex3D, stratum.BlockId);
                            }
                            ylower++;
                        }
                        else
                        {
                            int chunkY2 = yupper / 32;
                            int lY2 = yupper - chunkY2 * 32;
                            int localIndex3D2 = (32 * lY2 + lz) * 32 + lx;
                            IChunkBlocks chunkBlockData2 = chunks[chunkY2].Data;
                            if (chunkBlockData2.GetBlockIdUnsafe(localIndex3D2) == rockBlockId)
                            {
                                chunkBlockData2.SetBlockUnsafe(localIndex3D2, stratum.BlockId);
                            }
                            yupper--;
                        }

                    }

                    if (finishedColumn) break;
                }

                if (finishedColumn) break;
            }

            // cancel original function
            return false;
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(Mod.Info.ModID);
            api.Logger.Event("Successfully removed rockstrata generation patch!");
        }

    }
}
