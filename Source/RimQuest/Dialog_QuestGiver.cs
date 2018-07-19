using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimQuest
{
	public class Dialog_QuestGiver : Window
	{

		private string title = "RQ_QuestOpportunity".Translate();
		
		private const float TitleHeight = 42f;

		private const float ButtonHeight = 35f;

		public float interactionDelay;

		public const float defaultSilverCost = 50;

		public int actualSilverCost = 50;

		public int actualPlayerSilver = 0;

		public QuestPawn questPawn;

		public Pawn interactor;

		public IncidentDef selectedIncident = null;

		private Vector2 scrollPosition = Vector2.zero;

		private float creationRealTime = -1f;

		private string text => "RQ_QuestDialog".Translate(new object[]{interactor.LabelShort, questPawn.pawn.LabelShort, actualSilverCost});

		public Dialog_QuestGiver(QuestPawn newQuestPawn, Pawn newInteractor)
		{
			questPawn = newQuestPawn;
			interactor = newInteractor;
			//this.closeOnEscapeKey = true;
			this.forcePause = true;
			this.absorbInputAroundWindow = true;
			//this.closeOnEscapeKey = false;
			this.creationRealTime = RealTime.LastRealTime;
			this.onlyOneOfTypeAllowed = false;
			actualSilverCost = DetermineSilverCost();
			actualPlayerSilver = DetermineSilverAvailable(interactor);
		}

		private int DetermineSilverAvailable(Pawn pawn)
		{
			var currencies = pawn.Map.listerThings.ThingsOfDef(ThingDefOf.Silver);
			if (currencies == null || currencies.Count <= 0) return 0;
			return currencies.Sum(currency => currency.stackCount);
		}
		
		private int DetermineSilverCost()
		{
			var currentSilver = defaultSilverCost; //50
			var priceFactorBuy_TraderPriceFactor = (float)questPawn.pawn.Faction.RelationWith(Faction.OfPlayer).goodwill;
			priceFactorBuy_TraderPriceFactor += (priceFactorBuy_TraderPriceFactor < 0f) ? 0f : 100f;
			priceFactorBuy_TraderPriceFactor *= (priceFactorBuy_TraderPriceFactor < 0f) ? -1f : 1f;
			priceFactorBuy_TraderPriceFactor *= 0.005f;
			priceFactorBuy_TraderPriceFactor = 1f - priceFactorBuy_TraderPriceFactor;
			
			var priceGain_PlayerNegotiator = interactor.GetStatValue(StatDefOf.TradePriceImprovement, true); //Max 20
			
			//Avoid 0's for division operation
			priceGain_PlayerNegotiator = Mathf.Max(priceFactorBuy_TraderPriceFactor, 1);
			currentSilver = Mathf.Max(currentSilver, 1);
			currentSilver /= priceGain_PlayerNegotiator;
			
			currentSilver = currentSilver + (currentSilver * priceFactorBuy_TraderPriceFactor * (1f + Find.Storyteller.difficulty.tradePriceFactorLoss));
			currentSilver = Mathf.Min(currentSilver, 200f);
			return Mathf.RoundToInt(currentSilver);
		}

		public override Vector2 InitialSize => new Vector2(640f, 460f);

		private float TimeUntilInteractive => this.interactionDelay - (Time.realtimeSinceStartup - this.creationRealTime);

		private bool InteractionDelayExpired => this.TimeUntilInteractive <= 0f;

		public override void DoWindowContents(Rect inRect)
		{
			float num = inRect.y;
			//if (!this.title.NullOrEmpty())
			//{
			Text.Font = GameFont.Medium;
			Widgets.Label(new Rect(0f, num, inRect.width, 42f), this.title);
			num += 42f;
			//}
			Text.Font = GameFont.Small;
			Rect outRect = new Rect(inRect.x, num, inRect.width, inRect.height - 35f - 5f - num);
			float width = outRect.width - 16f;
			Rect viewRect = new Rect(0f, 0f, width, CalcHeight(width) + CalcOptionsHeight(width));
			Widgets.BeginScrollView(outRect, ref this.scrollPosition, viewRect, true);
			Widgets.Label(new Rect(0f, 0f, viewRect.width, viewRect.height - CalcOptionsHeight(width)), this.text.AdjustedFor(questPawn.pawn));
			for (var index = 0; index < questPawn.quests.Count; index++)
			{
				IncidentDef incidentDef = questPawn.quests[index];
				Rect rect6 = new Rect(24f, (viewRect.height - CalcOptionsHeight(width)) + (Text.CalcHeight(incidentDef.LabelCap, width) + 12f) * index + 8f, viewRect.width / 2f,
					Text.CalcHeight(incidentDef.LabelCap, width));
				if (Mouse.IsOver(rect6))
				{
					Widgets.DrawHighlight(rect6);
				}
				;
				if (Widgets.RadioButtonLabeled(rect6, incidentDef.LabelCap, selectedIncident == incidentDef))
				{
					selectedIncident = incidentDef;
				}
			}
			Widgets.EndScrollView();
			if (Widgets.ButtonText(new Rect(0f, inRect.height - 35f, inRect.width / 2f - 20f, 35f), "CancelButton".Translate(), true, false, true))
			{
				this.Close(true);
			}
			if (actualPlayerSilver >= actualSilverCost)
			{
				if (selectedIncident != null && Widgets.ButtonText(new Rect(inRect.width / 2f + 20f, inRect.height - 35f, inRect.width / 2f - 20f, 35f), "Confirm".Translate(), true, false, true))
				{
					IncidentParms incidentParms = StorytellerUtility.DefaultParmsNow(selectedIncident.category, Find.World);
					if (selectedIncident.pointsScaleable)
					{
						StorytellerComp storytellerComp = Find.Storyteller.storytellerComps.First((StorytellerComp x) => x is StorytellerComp_ThreatCycle || x is StorytellerComp_RandomMain);
						incidentParms = storytellerComp.GenerateParms(selectedIncident.category, incidentParms.target);
					}
					selectedIncident.Worker.TryExecute(incidentParms);
					var questPawns = Find.World.GetComponent<RimQuestTracker>().questPawns;
					if (questPawns != null && questPawns.Contains(questPawn))
						questPawns.Remove(questPawn);
					SoundDefOf.ExecuteTrade.PlayOneShotOnCamera(null);
					ReceiveSilver(questPawn.pawn, actualSilverCost);
					this.Close(true);
					Find.WindowStack.Add(new Dialog_MessageBox("RQ_QuestDialogTwo".Translate(new object[]{questPawn.pawn.LabelShort, interactor.LabelShort}).AdjustedFor(questPawn.pawn), "OK".Translate(), null, null, null, title));
				}
			}
			else
			{
				if (Widgets.ButtonText(new Rect(inRect.width / 2f + 20f, inRect.height - 35f, inRect.width / 2f - 20f, 35f), "RQ_LackFunds".Translate(), true, false, true))
				{
					SoundDefOf.ClickReject.PlayOneShotOnCamera(null);
					Messages.Message("RQ_LackFundsMessage".Translate(), MessageTypeDefOf.RejectInput);
				}				
			}
		}

		public void ReceiveSilver(Pawn receiver, int amountOwed)
		{
			int amountUnpaid = amountOwed;
			List<Thing> currencies = receiver.Map.listerThings.ThingsOfDef(ThingDefOf.Silver);
			if (currencies != null && currencies.Count > 0)
			{
				foreach (Thing currency in currencies.InRandomOrder<Thing>())
				{
					if (amountUnpaid <= 0)
					{
						break;
					}
					int num = Math.Min(amountUnpaid, currency.stackCount);
					currency.SplitOff(num).Destroy(DestroyMode.Vanish);
					amountUnpaid -= num;
				}
			}
			Thing thing = ThingMaker.MakeThing(ThingDefOf.Silver, null);
			thing.stackCount = amountOwed;
			receiver.inventory.TryAddItemNotForSale(thing);
		}

		private float CalcHeight(float width)
		{
			var result = Text.CalcHeight(text, width);
			return result;
		}

		private float CalcOptionsHeight(float width)
		{
			var result = 0f;
			foreach (var inc in questPawn.quests)
			{
				result += Text.CalcHeight(inc.letterLabel, width);
			}
			return result;
		}
	}
}
