using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace BoneAndIvory
{
    [StaticConstructorOnStartup]
    public static class Patch_CostList
    {
        // Store original values
        private static List<ThingDefCountClass> originalWallCostList;
        private static List<ThingDefCountClass> originalSkullFloorCostList;
        private static List<ThingDefCountClass> originalSkullFloorFineCostList;
        private static List<ThingDefCountClass> originalSkullPathwayCostList;
        
        // Store stone floor variant defs
        private static List<TerrainDef> stoneFloorDefs = new List<TerrainDef>();
        private static List<TerrainDef> stoneFloorFineDefs = new List<TerrainDef>();
        private static List<TerrainDef> stonePathwayDefs = new List<TerrainDef>();
        private static readonly Dictionary<TerrainDef, ThingDef> stoneForVariant = new Dictionary<TerrainDef, ThingDef>();
        
        // Cache base floor defs
        private static TerrainDef skullFloorBase;
        private static TerrainDef skullFloorFineBase;
        private static TerrainDef skullPathwayBase;
        
        private static bool hasAppliedOnce = false;
        
        // Cache mod settings for Harmony patch (set once on load, never changes)
        // Made internal so Harmony patch class can access them
        internal static bool cachedUseStoneBlocksForFloors = false;
        internal static bool settingsCached = false;

        static Patch_CostList()
        {
            var harmony = new Harmony("celphcs30.BoneAndIvory");
            Log.Message("[BoneAndIvory] Registering Harmony patches...");
            
            // Manually patch ResolveReferences to ensure it works
            try
            {
                var originalMethod = typeof(DesignationCategoryDef).GetMethod("ResolveReferences", 
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (originalMethod != null)
                {
                    var postfixMethod = typeof(Patch_DesignationCategoryDef_ResolveReferences).GetMethod("Postfix", 
                        BindingFlags.Public | BindingFlags.Static);
                    if (postfixMethod != null)
                    {
                        harmony.Patch(originalMethod, postfix: new HarmonyMethod(postfixMethod));
                        Log.Message("[BoneAndIvory] Successfully patched DesignationCategoryDef.ResolveReferences");
                    }
                    else
                    {
                        Log.Error("[BoneAndIvory] Could not find Postfix method in Patch_DesignationCategoryDef_ResolveReferences");
                    }
                }
                else
                {
                    Log.Warning("[BoneAndIvory] Could not find DesignationCategoryDef.ResolveReferences method - trying PatchAll");
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[BoneAndIvory] Error manually patching: {ex.Message}");
            }
            
            // Also try PatchAll as fallback
            harmony.PatchAll();
            Log.Message("[BoneAndIvory] Harmony patches registered");
            
            CacheOriginalCosts();
            CacheStoneFloorDefs();
            
            // Apply settings immediately in static constructor - this runs before designators are generated
            // We need to get settings early, but they might not be loaded yet
            Log.Message("[BoneAndIvory] Static constructor running - attempting early settings application");
            
            // Try to apply settings immediately
            TryApplySettingsEarly();
            
            // Also run via LongEventHandler as backup
            LongEventHandler.ExecuteWhenFinished(() => {
                Log.Message("[BoneAndIvory] LongEventHandler callback - applying settings NOW");
                ApplyCostListChanges();
            });
        }
        
        private static void TryApplySettingsEarly()
        {
            try
            {
                var mod = LoadedModManager.GetMod<BoneAndIvoryMod>();
                if (mod != null)
                {
                    var settings = mod.GetSettings<ModSettings_BoneAndIvory>();
                    if (settings != null)
                    {
                        Log.Message("[BoneAndIvory] Early settings found - applying immediately");
                        // Cache settings for Harmony patch
                        cachedUseStoneBlocksForFloors = settings.useStoneBlocksForFloors;
                        settingsCached = true;
                        
                        // Apply floor visibility immediately
                        ApplyFloorVisibility(settings.useStoneBlocksForFloors);
                    }
                    else
                    {
                        Log.Message("[BoneAndIvory] Early settings not found yet - will apply via LongEventHandler");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[BoneAndIvory] Error in TryApplySettingsEarly: {ex.Message}");
            }
        }
        
        private static void ApplyFloorVisibility(bool useStoneBlocks)
        {
            Log.Message($"[BoneAndIvory] ApplyFloorVisibility called with useStoneBlocks={useStoneBlocks}");
            
            if (useStoneBlocks)
            {
                // Hide skull floors
                if (skullFloorBase != null)
                {
                    skullFloorBase.designationCategory = null;
                    skullFloorBase.canGenerateDefaultDesignator = false;
                }
                if (skullFloorFineBase != null)
                {
                    skullFloorFineBase.designationCategory = null;
                    skullFloorFineBase.canGenerateDefaultDesignator = false;
                }
                if (skullPathwayBase != null)
                {
                    skullPathwayBase.designationCategory = null;
                    skullPathwayBase.canGenerateDefaultDesignator = false;
                }
            }
            else
            {
                // Show skull floors
                if (skullFloorBase != null)
                {
                    skullFloorBase.designationCategory = DesignationCategoryDefOf.Floors;
                    skullFloorBase.canGenerateDefaultDesignator = true;
                }
                if (skullFloorFineBase != null)
                {
                    skullFloorFineBase.designationCategory = DesignationCategoryDefOf.Floors;
                    skullFloorFineBase.canGenerateDefaultDesignator = true;
                }
                if (skullPathwayBase != null)
                {
                    skullPathwayBase.designationCategory = DesignationCategoryDefOf.Floors;
                    skullPathwayBase.canGenerateDefaultDesignator = true;
                }
            }
        }

        private static void CacheOriginalCosts()
        {
            var boneWall = DefDatabase<ThingDef>.GetNamedSilentFail("BoneWall");
            if (boneWall != null && boneWall.costList != null)
            {
                originalWallCostList = boneWall.costList.Select(c => new ThingDefCountClass(c.thingDef, c.count)).ToList();
            }

            skullFloorBase = DefDatabase<TerrainDef>.GetNamedSilentFail("SkullFloor");
            if (skullFloorBase != null && skullFloorBase.costList != null)
            {
                originalSkullFloorCostList = skullFloorBase.costList.Select(c => new ThingDefCountClass(c.thingDef, c.count)).ToList();
            }

            skullFloorFineBase = DefDatabase<TerrainDef>.GetNamedSilentFail("SkullFloorFine");
            if (skullFloorFineBase != null && skullFloorFineBase.costList != null)
            {
                originalSkullFloorFineCostList = skullFloorFineBase.costList.Select(c => new ThingDefCountClass(c.thingDef, c.count)).ToList();
            }

            skullPathwayBase = DefDatabase<TerrainDef>.GetNamedSilentFail("SkullPw");
            if (skullPathwayBase != null && skullPathwayBase.costList != null)
            {
                originalSkullPathwayCostList = skullPathwayBase.costList.Select(c => new ThingDefCountClass(c.thingDef, c.count)).ToList();
            }
        }

        private static void CacheStoneFloorDefs()
        {
            var stoneBlockDefs = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(d => d.thingCategories != null && d.thingCategories.Contains(ThingCategoryDefOf.StoneBlocks))
                .ToList();

            foreach (var stoneBlock in stoneBlockDefs)
            {
                string stoneName = stoneBlock.defName.Replace("Blocks", "");

                var variant = DefDatabase<TerrainDef>.GetNamedSilentFail("SkullFloor" + stoneName);
                if (variant != null)
                {
                    stoneFloorDefs.Add(variant);
                    stoneForVariant[variant] = stoneBlock;
                }

                var fineVariant = DefDatabase<TerrainDef>.GetNamedSilentFail("SkullFloorFine" + stoneName);
                if (fineVariant != null)
                {
                    stoneFloorFineDefs.Add(fineVariant);
                    stoneForVariant[fineVariant] = stoneBlock;
                }

                var pathwayVariant = DefDatabase<TerrainDef>.GetNamedSilentFail("SkullPw" + stoneName);
                if (pathwayVariant != null)
                {
                    stonePathwayDefs.Add(pathwayVariant);
                    stoneForVariant[pathwayVariant] = stoneBlock;
                }
            }
        }

        public static void ApplyCostListChanges()
        {
            Log.Message("[BoneAndIvory] ApplyCostListChanges called - hasAppliedOnce: " + hasAppliedOnce);
            
            var mod = LoadedModManager.GetMod<BoneAndIvoryMod>();
            if (mod == null)
            {
                Log.Warning("[BoneAndIvory] Mod not found, retrying later...");
                return;
            }

            var settings = mod.GetSettings<ModSettings_BoneAndIvory>();
            if (settings == null)
            {
                Log.Warning("[BoneAndIvory] Settings not found, retrying later...");
                return;
            }

            Log.Message($"[BoneAndIvory] Settings loaded - useStoneBlocksForWalls: {settings.useStoneBlocksForWalls}, useStoneBlocksForFloors: {settings.useStoneBlocksForFloors}");
            
            // Cache settings for Harmony patch (only once on first load)
            if (!settingsCached)
            {
                cachedUseStoneBlocksForFloors = settings.useStoneBlocksForFloors;
                settingsCached = true;
                Log.Message($"[BoneAndIvory] Cached settings for Harmony patch - useStoneBlocksForFloors: {cachedUseStoneBlocksForFloors}");
            }
            
            // Always apply - this is the "nuke" approach
            if (!hasAppliedOnce)
            {
                Log.Message("[BoneAndIvory] *** FIRST APPLICATION - APPLYING SETTINGS NOW ***");
                hasAppliedOnce = true;
            }
            else
            {
                Log.Message("[BoneAndIvory] Subsequent application (settings changed)");
            }
            
            Log.Message($"[BoneAndIvory] ApplyCostListChanges running - useStoneBlocksForFloors: {settings.useStoneBlocksForFloors}");

            var skullDef = DefDatabase<ThingDef>.GetNamedSilentFail("Skull");
            if (skullDef == null) return;

            // Update walls
            var boneWall = DefDatabase<ThingDef>.GetNamedSilentFail("BoneWall");
            if (boneWall != null)
            {
                if (settings.useStoneBlocksForWalls)
                {
                    // Use stone blocks via stuffCategories
                    boneWall.costList = null;
                    boneWall.costStuffCount = settings.stoneBlockCostForWall;
                    if (boneWall.stuffCategories == null)
                    {
                        boneWall.stuffCategories = new List<StuffCategoryDef>();
                    }
                    if (!boneWall.stuffCategories.Contains(StuffCategoryDefOf.Stony))
                    {
                        boneWall.stuffCategories.Add(StuffCategoryDefOf.Stony);
                    }
                }
                else
                {
                    // Use skulls - update cost from settings (skullDef already declared above)
                    if (skullDef != null)
                    {
                        boneWall.costList = new List<ThingDefCountClass> { new ThingDefCountClass(skullDef, settings.skullCostForWall) };
                    }
                    boneWall.costStuffCount = 0;
                    if (boneWall.stuffCategories != null)
                    {
                        boneWall.stuffCategories.Remove(StuffCategoryDefOf.Stony);
                        if (boneWall.stuffCategories.Count == 0)
                        {
                            boneWall.stuffCategories = null;
                        }
                    }
                    Log.Message($"[BoneAndIvory] Wall configured for skulls (cost: {settings.skullCostForWall}).");
                }
            }

            // Update floors - explicit show/hide toggling
            if (settings.useStoneBlocksForFloors)
            {
                Log.Message($"[BoneAndIvory] *** ENABLING STONE BLOCKS FOR FLOORS ***");
                Log.Message($"[BoneAndIvory] Found {stoneFloorDefs.Count} SkullFloor, {stoneFloorFineDefs.Count} SkullFloorFine, {stonePathwayDefs.Count} SkullPw variants");
                
                // Hide skull floors: designationCategory = null, canGenerateDefaultDesignator = false
                if (skullFloorBase != null)
                {
                    Log.Message($"[BoneAndIvory] Hiding SkullFloor - before: designationCategory={skullFloorBase.designationCategory?.defName ?? "null"}, canGenerate={skullFloorBase.canGenerateDefaultDesignator}");
                    skullFloorBase.designationCategory = null;
                    skullFloorBase.canGenerateDefaultDesignator = false;
                    Log.Message($"[BoneAndIvory] Hiding SkullFloor - after: designationCategory={skullFloorBase.designationCategory?.defName ?? "null"}, canGenerate={skullFloorBase.canGenerateDefaultDesignator}");
                }
                if (skullFloorFineBase != null)
                {
                    skullFloorFineBase.designationCategory = null;
                    skullFloorFineBase.canGenerateDefaultDesignator = false;
                }
                if (skullPathwayBase != null)
                {
                    skullPathwayBase.designationCategory = null;
                    skullPathwayBase.canGenerateDefaultDesignator = false;
                }

                // Show stone variants: designationCategory = Floors, canGenerateDefaultDesignator = true
                // Update their costs from settings
                ShowAndUpdateStoneVariants(stoneFloorDefs, settings.stoneBlockCostForSkullFloor);
                ShowAndUpdateStoneVariants(stoneFloorFineDefs, settings.stoneBlockCostForSkullFineFloor);
                ShowAndUpdateStoneVariants(stonePathwayDefs, settings.stoneBlockCostForSkullPathway);
            }
            else
            {
                Log.Message($"[BoneAndIvory] *** DISABLING STONE BLOCKS FOR FLOORS - SHOWING SKULL FLOORS ***");
                // Show skull floors: designationCategory = Floors, canGenerateDefaultDesignator = true
                // Update costs from settings (skullDef already declared above)
                if (skullDef != null)
                {
                    if (skullFloorBase != null)
                    {
                        Log.Message($"[BoneAndIvory] Showing SkullFloor - before: designationCategory={skullFloorBase.designationCategory?.defName ?? "null"}, canGenerate={skullFloorBase.canGenerateDefaultDesignator}");
                        skullFloorBase.designationCategory = DesignationCategoryDefOf.Floors;
                        skullFloorBase.canGenerateDefaultDesignator = true;
                        skullFloorBase.costList = new List<ThingDefCountClass> { new ThingDefCountClass(skullDef, settings.skullCostForSkullFloor) };
                        Log.Message($"[BoneAndIvory] Showing SkullFloor - after: designationCategory={skullFloorBase.designationCategory?.defName ?? "null"}, canGenerate={skullFloorBase.canGenerateDefaultDesignator}, cost={settings.skullCostForSkullFloor} {skullDef.defName}");
                    }
                    if (skullFloorFineBase != null)
                    {
                        Log.Message($"[BoneAndIvory] Showing SkullFloorFine - before: designationCategory={skullFloorFineBase.designationCategory?.defName ?? "null"}, canGenerate={skullFloorFineBase.canGenerateDefaultDesignator}");
                        skullFloorFineBase.designationCategory = DesignationCategoryDefOf.Floors;
                        skullFloorFineBase.canGenerateDefaultDesignator = true;
                        skullFloorFineBase.costList = new List<ThingDefCountClass> { new ThingDefCountClass(skullDef, settings.skullCostForSkullFineFloor) };
                        Log.Message($"[BoneAndIvory] Showing SkullFloorFine - after: designationCategory={skullFloorFineBase.designationCategory?.defName ?? "null"}, canGenerate={skullFloorFineBase.canGenerateDefaultDesignator}, cost={settings.skullCostForSkullFineFloor} {skullDef.defName}");
                    }
                    if (skullPathwayBase != null)
                    {
                        Log.Message($"[BoneAndIvory] Showing SkullPw - before: designationCategory={skullPathwayBase.designationCategory?.defName ?? "null"}, canGenerate={skullPathwayBase.canGenerateDefaultDesignator}");
                        skullPathwayBase.designationCategory = DesignationCategoryDefOf.Floors;
                        skullPathwayBase.canGenerateDefaultDesignator = true;
                        skullPathwayBase.costList = new List<ThingDefCountClass> { new ThingDefCountClass(skullDef, settings.skullCostForSkullPathway) };
                        Log.Message($"[BoneAndIvory] Showing SkullPw - after: designationCategory={skullPathwayBase.designationCategory?.defName ?? "null"}, canGenerate={skullPathwayBase.canGenerateDefaultDesignator}, cost={settings.skullCostForSkullPathway} {skullDef.defName}");
                    }
                }

                // Hide stone variants: designationCategory = null, canGenerateDefaultDesignator = false
                HideStoneVariants(stoneFloorDefs);
                HideStoneVariants(stoneFloorFineDefs);
                HideStoneVariants(stonePathwayDefs);
            }
            
            // After applying changes, try to force designator refresh
            // This is a workaround if Harmony patch doesn't run
            ForceDesignatorRefresh();
        }
        
        private static void ForceDesignatorRefresh()
        {
            try
            {
                var floorsCategory = DesignationCategoryDefOf.Floors;
                if (floorsCategory != null)
                {
                    // Try to find and clear the designators list to force regeneration
                    var designatorsList = GetDesignatorsListForRefresh(floorsCategory);
                    if (designatorsList != null)
                    {
                        Log.Message($"[BoneAndIvory] Found designators list with {designatorsList.Count} items - will filter it");
                        // Filter the list instead of clearing (safer)
                        FilterDesignatorsList(designatorsList);
                    }
                    else
                    {
                        Log.Message("[BoneAndIvory] Could not find designators list for refresh");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[BoneAndIvory] Error in ForceDesignatorRefresh: {ex.Message}");
            }
        }
        
        private static void FilterDesignatorsList(List<Designator> designators)
        {
            if (designators == null || designators.Count == 0)
                return;
            
            if (!settingsCached)
                return;
            
            Log.Message($"[BoneAndIvory] Filtering {designators.Count} designators, useStoneBlocksForFloors: {cachedUseStoneBlocksForFloors}");
            
            // Log all designator types to see what we're getting
            var designatorTypes = new System.Collections.Generic.Dictionary<string, int>();
            foreach (var d in designators)
            {
                if (d != null)
                {
                    string typeName = d.GetType().Name;
                    if (!designatorTypes.ContainsKey(typeName))
                    {
                        designatorTypes[typeName] = 0;
                    }
                    designatorTypes[typeName]++;
                }
            }
            Log.Message($"[BoneAndIvory] Designator types found: {string.Join(", ", designatorTypes.Select(kvp => $"{kvp.Key}:{kvp.Value}"))}");
            
            // Create filtered list
            var filtered = new List<Designator>();
            int skullFloorsFound = 0;
            int stoneVariantsFound = 0;
            int otherDesignators = 0;
            
            foreach (var designator in designators)
            {
                if (designator == null)
                {
                    filtered.Add(designator);
                    otherDesignators++;
                    continue;
                }
                
                string designatorType = designator.GetType().Name;
                
                // Log all designator types to see what we're getting
                if (designatorType.Contains("Dropdown") || designatorType.Contains("dropdown"))
                {
                    Log.Message($"[BoneAndIvory] Found dropdown designator type: {designatorType}, full type: {designator.GetType().FullName}");
                }
                
                // Get the def from designator - try multiple approaches
                BuildableDef def = null;
                if (designator is Designator_Build buildDesignator)
                {
                    def = buildDesignator.PlacingDef;
                }
                else
                {
                    // Try reflection for other designator types
                    var placingDefProp = designator.GetType().GetProperty("PlacingDef", 
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (placingDefProp != null)
                    {
                        def = placingDefProp.GetValue(designator) as BuildableDef;
                    }
                }
                
                // Check if this is a dropdown designator that might contain our floors
                // Try multiple ways to detect dropdown designators
                bool isDropdown = designatorType.Contains("Dropdown") || 
                                 designatorType.Contains("dropdown") ||
                                 designator.GetType().FullName.Contains("Dropdown");
                
                if (isDropdown)
                {
                    // Check the Elements property to see what designators are inside this dropdown
                    var elementsProp = designator.GetType().GetProperty("Elements", 
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var elementsField = designator.GetType().GetField("elements", 
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    
                    System.Collections.IEnumerable elements = null;
                    if (elementsProp != null)
                    {
                        elements = elementsProp.GetValue(designator) as System.Collections.IEnumerable;
                    }
                    else if (elementsField != null)
                    {
                        elements = elementsField.GetValue(designator) as System.Collections.IEnumerable;
                    }
                    
                    if (elements != null)
                    {
                        // Check if any element is for our stone variants
                        bool containsStoneVariant = false;
                        
                        foreach (var element in elements)
                        {
                            if (element == null) continue;
                            
                            // Get the def from the element designator
                            BuildableDef elementDef = null;
                            if (element is Designator_Build elementBuildDesignator)
                            {
                                elementDef = elementBuildDesignator.PlacingDef;
                            }
                            else
                            {
                                var placingDefProp = element.GetType().GetProperty("PlacingDef", 
                                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (placingDefProp != null)
                                {
                                    elementDef = placingDefProp.GetValue(element) as BuildableDef;
                                }
                            }
                            
                            if (elementDef is TerrainDef elementTerrainDef && elementTerrainDef.defName != null)
                            {
                                string elementDefName = elementTerrainDef.defName;
                                bool elementIsStoneVariant = (elementDefName.StartsWith("SkullFloor") || elementDefName.StartsWith("SkullPw")) &&
                                                             (elementDefName.Contains("Sandstone") || elementDefName.Contains("Granite") || 
                                                              elementDefName.Contains("Limestone") || elementDefName.Contains("Slate") || 
                                                              elementDefName.Contains("Marble"));
                                
                                if (elementIsStoneVariant)
                                {
                                    containsStoneVariant = true;
                                    Log.Message($"[BoneAndIvory] Dropdown contains stone variant: {elementDefName}");
                                    break;
                                }
                            }
                        }
                        
                        // If this dropdown contains our stone variants, it's one of our dropdowns
                        if (containsStoneVariant)
                        {
                            Log.Message($"[BoneAndIvory] Found our dropdown (contains stone variants), useStoneBlocksForFloors: {cachedUseStoneBlocksForFloors}");
                            
                            // If stone mode is ON, keep the dropdown (it contains stone variants)
                            // If stone mode is OFF, filter it out (we want individual skull floors instead)
                            if (cachedUseStoneBlocksForFloors)
                            {
                                filtered.Add(designator);
                                Log.Message($"[BoneAndIvory] Keeping dropdown (stone mode ON)");
                            }
                            else
                            {
                                Log.Message($"[BoneAndIvory] FILTERING OUT dropdown (stone mode OFF, showing individual skull floors)");
                            }
                            continue;
                        }
                    }
                    
                    // Not our dropdown, keep it
                    filtered.Add(designator);
                    otherDesignators++;
                    continue;
                }
                
                if (def == null || !(def is TerrainDef terrainDef))
                {
                    // Not a terrain designator, keep it
                    filtered.Add(designator);
                    otherDesignators++;
                    continue;
                }
                
                string defName = terrainDef.defName;
                if (defName == null)
                {
                    filtered.Add(designator);
                    otherDesignators++;
                    continue;
                }
                
                // Check if this is one of our floors
                bool isSkullFloor = defName == "SkullFloor" || defName == "SkullFloorFine" || defName == "SkullPw";
                bool isStoneVariant = (defName.StartsWith("SkullFloor") || defName.StartsWith("SkullPw")) &&
                                      (defName.Contains("Sandstone") || defName.Contains("Granite") || 
                                       defName.Contains("Limestone") || defName.Contains("Slate") || 
                                       defName.Contains("Marble"));
                
                // Log all terrain designators that start with "Skull" to see what we're getting
                if (defName.StartsWith("Skull"))
                {
                    Log.Message($"[BoneAndIvory] Found Skull terrain designator: {defName} (type: {designatorType}), isSkullFloor={isSkullFloor}, isStoneVariant={isStoneVariant}");
                }
                
                if (isSkullFloor)
                {
                    skullFloorsFound++;
                    if (!cachedUseStoneBlocksForFloors)
                    {
                        filtered.Add(designator);
                        Log.Message($"[BoneAndIvory] Keeping skull floor: {defName}");
                    }
                    else
                    {
                        Log.Message($"[BoneAndIvory] FILTERING OUT skull floor: {defName}");
                    }
                }
                else if (isStoneVariant)
                {
                    stoneVariantsFound++;
                    if (cachedUseStoneBlocksForFloors)
                    {
                        filtered.Add(designator);
                        Log.Message($"[BoneAndIvory] Keeping stone variant: {defName}");
                    }
                    else
                    {
                        Log.Message($"[BoneAndIvory] FILTERING OUT stone variant: {defName}");
                    }
                }
                else
                {
                    filtered.Add(designator);
                    otherDesignators++;
                }
            }
            
            Log.Message($"[BoneAndIvory] Summary: {skullFloorsFound} skull floors, {stoneVariantsFound} stone variants, {otherDesignators} other designators");
            Log.Message($"[BoneAndIvory] Before filtering: {designators.Count} designators, After filtering: {filtered.Count} designators");
            
            // Replace the list
            int originalCount = designators.Count;
            designators.Clear();
            designators.AddRange(filtered);
            Log.Message($"[BoneAndIvory] Replaced designators list: {originalCount} -> {filtered.Count}");
        }
        
        private static List<Designator> GetDesignatorsListForRefresh(DesignationCategoryDef categoryDef)
        {
            // Try all possible field/property names
            string[] fieldNames = { "allResolvedDesignators", "resolvedDesignators", "designators", "allDesignators" };
            string[] propertyNames = { "AllResolvedDesignators", "ResolvedDesignators", "Designators", "AllDesignators" };

            // Try fields first
            foreach (var fieldName in fieldNames)
            {
                var field = typeof(DesignationCategoryDef).GetField(fieldName, 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    var value = field.GetValue(categoryDef);
                    if (value is List<Designator> list)
                    {
                        return list;
                    }
                }
            }

            // Try properties
            foreach (var propName in propertyNames)
            {
                var prop = typeof(DesignationCategoryDef).GetProperty(propName, 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null)
                {
                    var value = prop.GetValue(categoryDef);
                    if (value is List<Designator> list)
                    {
                        return list;
                    }
                }
            }

            return null;
        }

        private static void ShowAndUpdateStoneVariants(List<TerrainDef> variants, int cost)
        {
            int updated = 0;
            foreach (var def in variants)
            {
                if (def == null) continue;

                if (!stoneForVariant.TryGetValue(def, out var stoneBlock) || stoneBlock == null)
                {
                    Log.Warning($"[BoneAndIvory] Could not find stone block for variant {def.defName}");
                    continue;
                }

                // Update cost from settings - always replace entire list to avoid lingering entries
                def.costList = new List<ThingDefCountClass> { new ThingDefCountClass(stoneBlock, cost) };

                // Show variant: variants start with designationCategory = Floors and canGenerateDefaultDesignator = true in XML
                // Just ensure they're still enabled (designators were generated at startup)
                Log.Message($"[BoneAndIvory] Showing variant {def.defName} - BEFORE: designationCategory={def.designationCategory?.defName ?? "null"}, canGenerate={def.canGenerateDefaultDesignator}");
                def.designationCategory = DesignationCategoryDefOf.Floors; // Ensure it's set (should already be from XML)
                def.canGenerateDefaultDesignator = true;
                Log.Message($"[BoneAndIvory] Showing variant {def.defName} - AFTER: designationCategory={def.designationCategory?.defName ?? "null"}, canGenerate={def.canGenerateDefaultDesignator}, designatorDropdown={def.designatorDropdown?.defName ?? "null"}, cost={cost} {stoneBlock.defName}");
                
                // Log first variant details
                if (updated == 0)
                {
                    Log.Message($"[BoneAndIvory] First variant configured: {def.defName}");
                }
                updated++;
            }
            if (variants.Count > 0)
            {
                Log.Message($"[BoneAndIvory] Showed and updated {updated}/{variants.Count} stone variants with cost {cost}");
            }
        }

        private static void HideStoneVariants(List<TerrainDef> variants)
        {
            int hidden = 0;
            foreach (var def in variants)
            {
                if (def == null) continue;
                // Hide variant: set designationCategory = null to hide from menu (designators already generated, but this should hide them)
                // Note: Setting canGenerateDefaultDesignator = false won't remove already-generated designators
                def.designationCategory = null;
                def.canGenerateDefaultDesignator = false;
                hidden++;
            }
            if (variants.Count > 0)
            {
                Log.Message($"[BoneAndIvory] Hid {hidden}/{variants.Count} stone variants.");
            }
        }
    }

    // Harmony patch to filter designators based on mod settings
    // Patches DesignationCategoryDef.ResolveReferences to filter designators once on load
    [HarmonyPatch(typeof(DesignationCategoryDef), "ResolveReferences")]
    public static class Patch_DesignationCategoryDef_ResolveReferences
    {
        public static void Postfix(DesignationCategoryDef __instance)
        {
            Log.Message($"[BoneAndIvory] Harmony patch ResolveReferences called for: {__instance?.defName ?? "null"}");
            
            // Only process Floors category
            if (__instance == null || __instance.defName != "Floors")
                return;

            // Only run once settings are cached (safe check)
            if (!Patch_CostList.settingsCached)
            {
                Log.Message("[BoneAndIvory] Harmony patch: Settings not cached yet, skipping");
                return;
            }

            Log.Message($"[BoneAndIvory] Harmony patch: Filtering designators for Floors category, useStoneBlocksForFloors: {Patch_CostList.cachedUseStoneBlocksForFloors}");

            // Use reflection to find and filter the designators list
            var designatorsList = GetDesignatorsList(__instance);
            if (designatorsList == null)
            {
                Log.Warning("[BoneAndIvory] Could not find designators list in DesignationCategoryDef - filtering skipped");
                return;
            }

            Log.Message($"[BoneAndIvory] Harmony patch: Found {designatorsList.Count} designators to filter");
            FilterDesignators(designatorsList);
        }
        
        
        private static List<Designator> GetDesignatorsList(DesignationCategoryDef categoryDef)
        {
            // Try all possible field/property names
            string[] fieldNames = { "allResolvedDesignators", "resolvedDesignators", "designators", "allDesignators" };
            string[] propertyNames = { "AllResolvedDesignators", "ResolvedDesignators", "Designators", "AllDesignators" };

            // Try fields first
            foreach (var fieldName in fieldNames)
            {
                var field = typeof(DesignationCategoryDef).GetField(fieldName, 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    var value = field.GetValue(categoryDef);
                    if (value is List<Designator> list)
                    {
                        Log.Message($"[BoneAndIvory] Harmony patch: Found designators list via field '{fieldName}'");
                        return list;
                    }
                }
            }

            // Try properties
            foreach (var propName in propertyNames)
            {
                var prop = typeof(DesignationCategoryDef).GetProperty(propName, 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null)
                {
                    var value = prop.GetValue(categoryDef);
                    if (value is List<Designator> list)
                    {
                        Log.Message($"[BoneAndIvory] Harmony patch: Found designators list via property '{propName}'");
                        return list;
                    }
                }
            }

            // Last resort: enumerate all fields and find List<Designator>
            var allFields = typeof(DesignationCategoryDef).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in allFields)
            {
                if (field.FieldType == typeof(List<Designator>))
                {
                    var value = field.GetValue(categoryDef) as List<Designator>;
                    if (value != null)
                    {
                        Log.Message($"[BoneAndIvory] Harmony patch: Found designators list via field '{field.Name}' (enumerated)");
                        return value;
                    }
                }
            }

            Log.Warning("[BoneAndIvory] Harmony patch: Could not find designators list in DesignationCategoryDef");
            return null;
        }
        
        private static void FilterDesignators(List<Designator> designators)
        {
            if (designators == null || designators.Count == 0)
                return;

            // Create a filtered list
            var filtered = new List<Designator>();
            int skullFloorsFound = 0;
            int stoneVariantsFound = 0;
            int otherDesignators = 0;
            
            foreach (var designator in designators)
            {
                if (designator == null)
                    continue;

                // Check if this designator is for one of our floor defs
                // Use reflection to get the def - try common property/field names
                BuildableDef def = null;
                string designatorTypeName = designator.GetType().Name;
                
                // Try Designator_Build.PlacingDef (most common for build designators)
                var placingDefProp = designator.GetType().GetProperty("PlacingDef", 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (placingDefProp != null)
                {
                    def = placingDefProp.GetValue(designator) as BuildableDef;
                    if (def != null)
                    {
                        Log.Message($"[BoneAndIvory] Harmony patch: Found def via PlacingDef property: {def.defName} (designator type: {designatorTypeName})");
                    }
                }
                
                // If not found, try other common names
                if (def == null)
                {
                    var targetDefProp = designator.GetType().GetProperty("targetDef", 
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (targetDefProp != null)
                    {
                        def = targetDefProp.GetValue(designator) as BuildableDef;
                        if (def != null)
                        {
                            Log.Message($"[BoneAndIvory] Harmony patch: Found def via targetDef property: {def.defName}");
                        }
                    }
                }
                
                // Also try field access
                if (def == null)
                {
                    var placingDefField = designator.GetType().GetField("placingDef", 
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (placingDefField != null)
                    {
                        def = placingDefField.GetValue(designator) as BuildableDef;
                        if (def != null)
                        {
                            Log.Message($"[BoneAndIvory] Harmony patch: Found def via placingDef field: {def.defName}");
                        }
                    }
                }
                
                // Last resort: enumerate all fields/properties to find BuildableDef
                if (def == null)
                {
                    var allFields = designator.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    foreach (var field in allFields)
                    {
                        if (typeof(BuildableDef).IsAssignableFrom(field.FieldType))
                        {
                            def = field.GetValue(designator) as BuildableDef;
                            if (def != null)
                            {
                                Log.Message($"[BoneAndIvory] Harmony patch: Found def via field '{field.Name}': {def.defName}");
                                break;
                            }
                        }
                    }
                }
                
                if (def == null || !(def is TerrainDef terrainDef))
                {
                    // Not a terrain def, keep it
                    filtered.Add(designator);
                    otherDesignators++;
                    continue;
                }

                string defName = terrainDef.defName;
                if (defName == null)
                {
                    filtered.Add(designator);
                    otherDesignators++;
                    continue;
                }

                // Check if this is one of our base skull floors
                bool isSkullFloor = defName == "SkullFloor" || 
                                   defName == "SkullFloorFine" || 
                                   defName == "SkullPw";

                // Check if this is one of our stone variants
                bool isStoneVariant = (defName.StartsWith("SkullFloor") && 
                                       (defName.Contains("Sandstone") || 
                                        defName.Contains("Granite") || 
                                        defName.Contains("Limestone") || 
                                        defName.Contains("Slate") || 
                                        defName.Contains("Marble"))) ||
                                      (defName.StartsWith("SkullPw") && 
                                       (defName.Contains("Sandstone") || 
                                        defName.Contains("Granite") || 
                                        defName.Contains("Limestone") || 
                                        defName.Contains("Slate") || 
                                        defName.Contains("Marble")));

                // Filter based on cached settings (performant - no repeated mod lookups)
                if (isSkullFloor)
                {
                    skullFloorsFound++;
                    // Show skull floors when stone mode is OFF
                    if (!Patch_CostList.cachedUseStoneBlocksForFloors)
                    {
                        filtered.Add(designator);
                        Log.Message($"[BoneAndIvory] Harmony patch: Keeping skull floor designator: {defName}");
                    }
                    else
                    {
                        Log.Message($"[BoneAndIvory] Harmony patch: Filtering out skull floor designator: {defName}");
                    }
                }
                else if (isStoneVariant)
                {
                    stoneVariantsFound++;
                    // Show stone variants when stone mode is ON
                    if (Patch_CostList.cachedUseStoneBlocksForFloors)
                    {
                        filtered.Add(designator);
                        Log.Message($"[BoneAndIvory] Harmony patch: Keeping stone variant designator: {defName}");
                    }
                    else
                    {
                        Log.Message($"[BoneAndIvory] Harmony patch: Filtering out stone variant designator: {defName}");
                    }
                }
                else
                {
                    // Not one of our floors, keep it (safe - don't filter other mods' floors)
                    filtered.Add(designator);
                    otherDesignators++;
                }
            }

            Log.Message($"[BoneAndIvory] Harmony patch: Filtered designators - Found {skullFloorsFound} skull floors, {stoneVariantsFound} stone variants, {otherDesignators} other designators. Before: {designators.Count}, After: {filtered.Count}");

            // Replace the designators list with filtered version
            designators.Clear();
            designators.AddRange(filtered);
        }
    }
}
