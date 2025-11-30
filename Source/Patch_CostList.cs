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
            
            CacheOriginalCosts();
            CacheStoneFloorDefs();
            
            // Apply settings immediately in static constructor - this runs before designators are generated
            // We need to get settings early, but they might not be loaded yet
            TryApplySettingsEarly();
            
            // Also run via LongEventHandler as backup
            LongEventHandler.ExecuteWhenFinished(() => {
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
                        // Cache settings for Harmony patch
                        cachedUseStoneBlocksForFloors = settings.useStoneBlocksForFloors;
                        settingsCached = true;
                        
                        // Apply floor visibility immediately
                        ApplyFloorVisibility(settings.useStoneBlocksForFloors);
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
            
            // Cache settings for Harmony patch (only once on first load)
            if (!settingsCached)
            {
                cachedUseStoneBlocksForFloors = settings.useStoneBlocksForFloors;
                settingsCached = true;
            }
            
            // Always apply - this is the "nuke" approach
            if (!hasAppliedOnce)
            {
                hasAppliedOnce = true;
            }

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
                }
            }

            // Update floors - explicit show/hide toggling
            if (settings.useStoneBlocksForFloors)
            {
                // Hide skull floors: designationCategory = null, canGenerateDefaultDesignator = false
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

                // Show stone variants: designationCategory = Floors, canGenerateDefaultDesignator = true
                // Update their costs from settings
                ShowAndUpdateStoneVariants(stoneFloorDefs, settings.stoneBlockCostForSkullFloor);
                ShowAndUpdateStoneVariants(stoneFloorFineDefs, settings.stoneBlockCostForSkullFineFloor);
                ShowAndUpdateStoneVariants(stonePathwayDefs, settings.stoneBlockCostForSkullPathway);
            }
            else
            {
                // Show skull floors: designationCategory = Floors, canGenerateDefaultDesignator = true
                // Update costs from settings (skullDef already declared above)
                if (skullDef != null)
                {
                    if (skullFloorBase != null)
                    {
                        skullFloorBase.designationCategory = DesignationCategoryDefOf.Floors;
                        skullFloorBase.canGenerateDefaultDesignator = true;
                        skullFloorBase.costList = new List<ThingDefCountClass> { new ThingDefCountClass(skullDef, settings.skullCostForSkullFloor) };
                    }
                    if (skullFloorFineBase != null)
                    {
                        skullFloorFineBase.designationCategory = DesignationCategoryDefOf.Floors;
                        skullFloorFineBase.canGenerateDefaultDesignator = true;
                        skullFloorFineBase.costList = new List<ThingDefCountClass> { new ThingDefCountClass(skullDef, settings.skullCostForSkullFineFloor) };
                    }
                    if (skullPathwayBase != null)
                    {
                        skullPathwayBase.designationCategory = DesignationCategoryDefOf.Floors;
                        skullPathwayBase.canGenerateDefaultDesignator = true;
                        skullPathwayBase.costList = new List<ThingDefCountClass> { new ThingDefCountClass(skullDef, settings.skullCostForSkullPathway) };
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
                        // Filter the list instead of clearing (safer)
                        FilterDesignatorsList(designatorsList);
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
            
            // Create filtered list
            var filtered = new List<Designator>();
            
            foreach (var designator in designators)
            {
                if (designator == null)
                {
                    filtered.Add(designator);
                    continue;
                }
                
                string designatorType = designator.GetType().Name;
                
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
                                    break;
                                }
                            }
                        }
                        
                        // If this dropdown contains our stone variants, it's one of our dropdowns
                        if (containsStoneVariant)
                        {
                            // If stone mode is ON, keep the dropdown (it contains stone variants)
                            // If stone mode is OFF, filter it out (we want individual skull floors instead)
                            if (cachedUseStoneBlocksForFloors)
                            {
                                filtered.Add(designator);
                            }
                            continue;
                        }
                    }
                    
                    // Not our dropdown, keep it
                    filtered.Add(designator);
                    continue;
                }
                
                if (def == null || !(def is TerrainDef terrainDef))
                {
                    // Not a terrain designator, keep it
                    filtered.Add(designator);
                    continue;
                }
                
                string defName = terrainDef.defName;
                if (defName == null)
                {
                    filtered.Add(designator);
                    continue;
                }
                
                // Check if this is one of our floors
                bool isSkullFloor = defName == "SkullFloor" || defName == "SkullFloorFine" || defName == "SkullPw";
                bool isStoneVariant = (defName.StartsWith("SkullFloor") || defName.StartsWith("SkullPw")) &&
                                      (defName.Contains("Sandstone") || defName.Contains("Granite") || 
                                       defName.Contains("Limestone") || defName.Contains("Slate") || 
                                       defName.Contains("Marble"));
                
                if (isSkullFloor)
                {
                    if (!cachedUseStoneBlocksForFloors)
                    {
                        filtered.Add(designator);
                    }
                }
                else if (isStoneVariant)
                {
                    if (cachedUseStoneBlocksForFloors)
                    {
                        filtered.Add(designator);
                    }
                }
                else
                {
                    filtered.Add(designator);
                }
            }
            
            // Replace the list
            designators.Clear();
            designators.AddRange(filtered);
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
                def.designationCategory = DesignationCategoryDefOf.Floors; // Ensure it's set (should already be from XML)
                def.canGenerateDefaultDesignator = true;
                updated++;
            }
        }

        private static void HideStoneVariants(List<TerrainDef> variants)
        {
            foreach (var def in variants)
            {
                if (def == null) continue;
                // Hide variant: set designationCategory = null to hide from menu (designators already generated, but this should hide them)
                // Note: Setting canGenerateDefaultDesignator = false won't remove already-generated designators
                def.designationCategory = null;
                def.canGenerateDefaultDesignator = false;
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
            // Only process Floors category
            if (__instance == null || __instance.defName != "Floors")
                return;

            // Only run once settings are cached (safe check)
            if (!Patch_CostList.settingsCached)
                return;

            // Use reflection to find and filter the designators list
            var designatorsList = GetDesignatorsList(__instance);
            if (designatorsList == null)
            {
                Log.Warning("[BoneAndIvory] Could not find designators list in DesignationCategoryDef - filtering skipped");
                return;
            }

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

            // Last resort: enumerate all fields and find List<Designator>
            var allFields = typeof(DesignationCategoryDef).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in allFields)
            {
                if (field.FieldType == typeof(List<Designator>))
                {
                    var value = field.GetValue(categoryDef) as List<Designator>;
                    if (value != null)
                    {
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
            
            foreach (var designator in designators)
            {
                if (designator == null)
                    continue;

                // Check if this designator is for one of our floor defs
                // Use reflection to get the def - try common property/field names
                BuildableDef def = null;
                
                // Try Designator_Build.PlacingDef (most common for build designators)
                var placingDefProp = designator.GetType().GetProperty("PlacingDef", 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (placingDefProp != null)
                {
                    def = placingDefProp.GetValue(designator) as BuildableDef;
                }
                
                // If not found, try other common names
                if (def == null)
                {
                    var targetDefProp = designator.GetType().GetProperty("targetDef", 
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (targetDefProp != null)
                    {
                        def = targetDefProp.GetValue(designator) as BuildableDef;
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
                                break;
                            }
                        }
                    }
                }
                
                if (def == null || !(def is TerrainDef terrainDef))
                {
                    // Not a terrain def, keep it
                    filtered.Add(designator);
                    continue;
                }

                string defName = terrainDef.defName;
                if (defName == null)
                {
                    filtered.Add(designator);
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
                    // Show skull floors when stone mode is OFF
                    if (!Patch_CostList.cachedUseStoneBlocksForFloors)
                    {
                        filtered.Add(designator);
                    }
                }
                else if (isStoneVariant)
                {
                    // Show stone variants when stone mode is ON
                    if (Patch_CostList.cachedUseStoneBlocksForFloors)
                    {
                        filtered.Add(designator);
                    }
                }
                else
                {
                    // Not one of our floors, keep it (safe - don't filter other mods' floors)
                    filtered.Add(designator);
                }
            }

            // Replace the designators list with filtered version
            designators.Clear();
            designators.AddRange(filtered);
        }
    }
}
