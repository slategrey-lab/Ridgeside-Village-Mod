﻿using StardewModdingAPI;
using StardewModdingAPI.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using StardewValley;
using StardewValley.Menus;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using StardewModdingAPI.Utilities;

namespace RidgesideVillage
{
    internal static class HarmonyPatch_UntimedSO
    {
        private static IMonitor Monitor { get; set; }
        private static IModHelper Helper { get; set; }

        private readonly static List<string> NPCNames = new List<string> { 
            "Acorn", "Aguar", "Alissa", "Anton", "Ariah", "Belinda", "Bert", "Blair", "Bliss", "Bryle", "Carmen",
            "Corine", "Daia", "Ezekiel", "Faye", "Flor", "Freddie", "Helen", "Ian", "Irene", "Jeric", "Jio", "Keahi",
            "Kenneth", "Kiarra", "Kimpoi", "Kiwi", "Lenny", "Lola", "Lorenzo", "Louie", "Maddie", "Maive", "Malaya",
            "Naomi", "Olga", "Paula", "Philip", "Pika", "Pipo", "Raeriyala", "Richard", "Sari", "Shanice", "Shiro",
            "Sonny", "Trinnie", "Undreya", "Ysabelle", "Yuuma", "Zayne"};
        private static Texture2D RSVemojis;

        public static void ApplyPatch(Harmony harmony, IModHelper helper)
        {
            Helper = helper;

            Helper.Events.GameLoop.DayEnding += OnDayEnd;
            Log.Trace($"Applying Harmony Patch \"{nameof(HarmonyPatch_UntimedSO)}\" prefixing SDV method.");
            
            harmony.Patch(
                original: AccessTools.Method(typeof(SpecialOrder), nameof(SpecialOrder.IsTimedQuest)),
                postfix: new HarmonyMethod(typeof(HarmonyPatch_UntimedSO), nameof(SpecialOrders_IsTimed_postifx))
            );

            //only method called once on quest end. Is called for *all* players, not just host.
            harmony.Patch(
                original: AccessTools.Method(typeof(SpecialOrder), nameof(SpecialOrder.HostHandleQuestEnd)),
                postfix: new HarmonyMethod(typeof(HarmonyPatch_UntimedSO), nameof(SpecialOrders_HostHandleQuestEnd_postfix))
            );

            //causes issues on MAC apparently??
            if (Constants.TargetPlatform == GamePlatform.Windows) 
            {
                 harmony.Patch(
                    original: AccessTools.Method(typeof(SpecialOrdersBoard), nameof(SpecialOrdersBoard.GetPortraitForRequester)),
                    postfix: new HarmonyMethod(typeof(HarmonyPatch_UntimedSO), nameof(SpecialOrdersBoard_GetPortrait_postifx))
                );
                try
                {
                    Type QFSpecialBoardClass = Type.GetType("QuestFramework.Framework.Menus.CustomOrderBoard, QuestFramework");
                    harmony.Patch(
                        original: AccessTools.Method(QFSpecialBoardClass, "GetPortraitForRequester"),
                        postfix: new HarmonyMethod(typeof(HarmonyPatch_UntimedSO), nameof(SpecialOrdersBoard_GetPortrait_postifx))
                    );
                }
                catch
                {
                    Log.Info("Couldnt patch Quest Framework. Emojis in the SO board might not show up");
                }
            }
            else
            {
                Log.Trace($"Not patching GetProtraitForRequester because platform is {Constants.TargetPlatform}");
            }
            
           
        }

        private static void SpecialOrders_IsTimed_postifx(ref SpecialOrder __instance, ref bool __result)
        {
            if (__instance.questKey.Value.StartsWith("RSV.UntimedSpecialOrder"))
            {
                __result = false;
            }
        }

        private static void SpecialOrdersBoard_GetPortrait_postifx(SpecialOrdersBoard __instance, string requester_name, ref KeyValuePair<Texture2D, Rectangle>?  __result)
        {
            try
            {
                if (RSVemojis == null)
                {
                    RSVemojis = Helper.Content.Load<Texture2D>(PathUtilities.NormalizeAssetName("LooseSprites\\RSVemojis"), ContentSource.GameContent);
                    if(RSVemojis== null)
                    {
                        Log.Error($"Loading error: Couldn't load {PathUtilities.NormalizeAssetName("LooseSprites\\RSVemojis")}");
                        return;
                    }
                }

                if (__result == null)
                {
                    int index = NPCNames.FindIndex(name => name.Equals(requester_name, StringComparison.OrdinalIgnoreCase));
                    if (index != -1)
                    {
                        __result = new KeyValuePair<Texture2D, Rectangle>(HarmonyPatch_UntimedSO.RSVemojis, new Rectangle(index % 14 * 9, index / 14 * 9, 9, 9));
                        return;
                    }
                }
                return;
            }
            catch(Exception e)
            {
                Log.Error("Error in SpecialOrdersBoard_GetPortrait_postifx");
                Log.Error(e.Message);
                Log.Error(e.StackTrace);
                return;
            }
            
        }

        private static void SpecialOrders_HostHandleQuestEnd_postfix(ref SpecialOrder __instance)
        {
            try
            {
                if (__instance.questKey.Value == "RSV.UntimedSpecialOrder.DaiaQuest")
                {
                    int questID = ExternalAPIs.QF.ResolveQuestId("preparations_complete@Rafseazz.RSVQF");
                    Game1.player.addQuest((questID));
                }
            }
            catch (Exception e)
            {

                Log.Error("Error in SpecialOrders_HostHandleQuestEnd_postfix");
                Log.Error(e.Message);
                Log.Error(e.StackTrace);
            }
           
        }

        public static void OnDayEnd(object sender, DayEndingEventArgs e)
        {
            if (!Context.IsMainPlayer)
                return;

            foreach(SpecialOrder o in Game1.player.team.specialOrders)
            {
                if (o.questKey.Value.StartsWith("RSV.UntimedSpecialOrder"))
                {
                    o.dueDate.Value = Game1.Date.TotalDays + 100;
                }
            }
        }

    }
}
