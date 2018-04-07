using System.Collections.Generic;
using RimWorld.Planet;
using Verse;

namespace RimQuest
{
    public class RimQuestTracker : WorldComponent
    {
        
        public List<QuestPawn> questPawns = new List<QuestPawn>();
        
        public RimQuestTracker(World world) : base(world)
        {
        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();
            if (Find.TickManager.TicksGame % 250 == 0 && !questPawns.NullOrEmpty())
            {
                questPawns.RemoveAll(x => x.pawn == null || x.pawn.Downed || x.pawn.Dead || x.pawn.Destroyed);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref questPawns, "questPawns", LookMode.Deep);
        }
    }
}