using RimWorld;
using UnityEngine;
using Verse;

namespace BoneAndIvory
{
    public class BoneAndIvoryMod : Mod
    {
        public ModSettings_BoneAndIvory Settings;

        public BoneAndIvoryMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<ModSettings_BoneAndIvory>();
        }

        public override string SettingsCategory() => "CelphIvoryAndSkulls";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var s = Settings ??= GetSettings<ModSettings_BoneAndIvory>();
            
                // Snapshot old values BEFORE UI changes them
                bool oldWalls = s.useStoneBlocksForWalls;
                bool oldFloors = s.useStoneBlocksForFloors;
                int oldWallCost = s.stoneBlockCostForWall;
                int oldWallSkullCost = s.skullCostForWall;
                int oldFloorCost = s.stoneBlockCostForSkullFloor;
                int oldFineCost = s.stoneBlockCostForSkullFineFloor;
                int oldPathCost = s.stoneBlockCostForSkullPathway;
                int oldFloorSkullCost = s.skullCostForSkullFloor;
                int oldFineSkullCost = s.skullCostForSkullFineFloor;
                int oldPathSkullCost = s.skullCostForSkullPathway;
            
            var l = new Listing_Standard();
            l.Begin(inRect);

                // Wall toggle
                l.CheckboxLabeled("Use stone blocks for walls (instead of skulls)", ref s.useStoneBlocksForWalls);
                if (s.useStoneBlocksForWalls)
                {
                    l.Label($"Stone block cost per wall: {s.stoneBlockCostForWall}");
                    s.stoneBlockCostForWall = (int)l.Slider(s.stoneBlockCostForWall, 1, 20);
                }
                else
                {
                    l.Label($"Skull cost per wall: {s.skullCostForWall}");
                    s.skullCostForWall = (int)l.Slider(s.skullCostForWall, 1, 20);
                }

                l.Gap(12f);

                // Floor toggle
                l.CheckboxLabeled("Use stone blocks for floors (instead of skulls)", ref s.useStoneBlocksForFloors);
                if (s.useStoneBlocksForFloors)
                {
                    l.Label($"Bone Floor: {s.stoneBlockCostForSkullFloor}");
                    s.stoneBlockCostForSkullFloor = (int)l.Slider(s.stoneBlockCostForSkullFloor, 1, 20);
                    l.Label($"Skull Fine Floor: {s.stoneBlockCostForSkullFineFloor}");
                    s.stoneBlockCostForSkullFineFloor = (int)l.Slider(s.stoneBlockCostForSkullFineFloor, 1, 20);
                    l.Label($"Skull Pathway: {s.stoneBlockCostForSkullPathway}");
                    s.stoneBlockCostForSkullPathway = (int)l.Slider(s.stoneBlockCostForSkullPathway, 1, 20);
                }
                else
                {
                    l.Label($"Bone Floor: {s.skullCostForSkullFloor}");
                    s.skullCostForSkullFloor = (int)l.Slider(s.skullCostForSkullFloor, 1, 20);
                    l.Label($"Skull Fine Floor: {s.skullCostForSkullFineFloor}");
                    s.skullCostForSkullFineFloor = (int)l.Slider(s.skullCostForSkullFineFloor, 1, 20);
                    l.Label($"Skull Pathway: {s.skullCostForSkullPathway}");
                    s.skullCostForSkullPathway = (int)l.Slider(s.skullCostForSkullPathway, 1, 20);
                }

            l.Gap(12f);
            l.Label("Note: Skull spikes are unaffected by these settings.");
            l.Label("Note: Floor changes require exiting to main menu or restarting the game to take effect.");

            l.Gap(12f);
                if (l.ButtonText("Reset to Default"))
                {
                    s.useStoneBlocksForWalls = false;
                    s.useStoneBlocksForFloors = false;
                    s.stoneBlockCostForWall = 3;
                    s.skullCostForWall = 3;
                    s.stoneBlockCostForSkullFloor = 3;
                    s.stoneBlockCostForSkullFineFloor = 7;
                    s.stoneBlockCostForSkullPathway = 1;
                    s.skullCostForSkullFloor = 3;
                    s.skullCostForSkullFineFloor = 7;
                    s.skullCostForSkullPathway = 1;
                }

            l.End();
            
                // Compare after UI to detect changes (old values were captured before UI)
                bool settingsChanged = 
                    oldWalls != s.useStoneBlocksForWalls ||
                    oldFloors != s.useStoneBlocksForFloors ||
                    oldWallCost != s.stoneBlockCostForWall ||
                    oldWallSkullCost != s.skullCostForWall ||
                    oldFloorCost != s.stoneBlockCostForSkullFloor ||
                    oldFineCost != s.stoneBlockCostForSkullFineFloor ||
                    oldPathCost != s.stoneBlockCostForSkullPathway ||
                    oldFloorSkullCost != s.skullCostForSkullFloor ||
                    oldFineSkullCost != s.skullCostForSkullFineFloor ||
                    oldPathSkullCost != s.skullCostForSkullPathway;
            
            if (settingsChanged)
            {
                WriteSettings();
                Patch_CostList.ApplyCostListChanges();
            }
        }
    }
}
