using Verse;

namespace BoneAndIvory
{
    public class ModSettings_BoneAndIvory : ModSettings
    {
        // Toggle settings
        public bool useStoneBlocksForWalls = false;
        public bool useStoneBlocksForFloors = false;
        
        // Stone block costs (used when toggles are enabled)
        public int stoneBlockCostForWall = 3;
        public int stoneBlockCostForSkullFloor = 3;
        public int stoneBlockCostForSkullFineFloor = 7;
        public int stoneBlockCostForSkullPathway = 1;
        
        // Skull costs (used when toggles are disabled)
        public int skullCostForWall = 3;
        public int skullCostForSkullFloor = 3;
        public int skullCostForSkullFineFloor = 7;
        public int skullCostForSkullPathway = 1;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref useStoneBlocksForWalls, "useStoneBlocksForWalls", false);
            Scribe_Values.Look(ref useStoneBlocksForFloors, "useStoneBlocksForFloors", false);
            Scribe_Values.Look(ref stoneBlockCostForWall, "stoneBlockCostForWall", 3);
            Scribe_Values.Look(ref stoneBlockCostForSkullFloor, "stoneBlockCostForSkullFloor", 3);
            Scribe_Values.Look(ref stoneBlockCostForSkullFineFloor, "stoneBlockCostForSkullFineFloor", 7);
            Scribe_Values.Look(ref stoneBlockCostForSkullPathway, "stoneBlockCostForSkullPathway", 1);
            Scribe_Values.Look(ref skullCostForWall, "skullCostForWall", 3);
            Scribe_Values.Look(ref skullCostForSkullFloor, "skullCostForSkullFloor", 3);
            Scribe_Values.Look(ref skullCostForSkullFineFloor, "skullCostForSkullFineFloor", 7);
            Scribe_Values.Look(ref skullCostForSkullPathway, "skullCostForSkullPathway", 1);
            base.ExposeData();
        }
    }
}
