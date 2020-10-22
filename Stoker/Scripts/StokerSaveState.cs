using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;
using System.Linq;

namespace Stoker.Scripts
{
    public static class SaveStateHelper
    {
        public static string SaveToString(StokerSaveState state)
        {
            return JsonConvert.SerializeObject(state);
        }
        public static StokerSaveState BuildFromJson(string JsonString)
        {
            return JsonConvert.DeserializeObject<StokerSaveState>(JsonString);
        }
        public static void BuildSaveState(StokerSaveState saveState, SaveManager save)
        {
            if (save != null && saveState != null)
            {
                //Get all the RelicDataIDs for our relics
                saveState.relics = save.GetCollectedRelics().ConvertAll<string>(x => x.GetRelicDataID()).ToArray();
                //Explanation of what i am doing here
                //I grab the deck and convert it into the appropriate IDs
                saveState.cards = save.GetDeckState()
                    .Where(
                        x =>
                            x.GetSpawnCharacterData() == null
                            || (x.GetSpawnCharacterData() != null && !x.GetSpawnCharacterData().IsChampion())
                        )
                    .ToList()
                    .ConvertAll(
                        x =>
                            new CardWrapper
                            {
                                cardName = x.GetID(),
                                upgradeNames = x.GetCardStateModifiers().GetCardUpgrades()
                                    .ConvertAll(
                                        y =>
                                            y.GetCardUpgradeDataId()
                                    ).ToArray()
                            }
                        ).ToArray();
            }
        }
        public static void LoadSaveState(StokerSaveState saveState, SaveManager save)
        {
            if (save != null && saveState != null)
            {
                //remove all cards except for champion

                var deckState = save.GetDeckState();
                if(deckState != null)
                {
                    deckState.RemoveAll(x => !x.IsChampionCard());
                    List<IDeckNotifications> deckNotifications = (List<IDeckNotifications>)AccessTools.Field(typeof(SaveManager), "deckNotifications").GetValue(save);
                    deckNotifications.ForEach(rn => rn.DeckChangedNotification(deckState, save.GetVisibleDeckCount()));
                }
                save.RemoveAllRelics();
                if (saveState.relics != null)
                {
                    foreach (var relic in saveState.relics)
                    {
                        if (relic != null)
                        {
                            var relicData = save.GetAllGameData().FindCollectableRelicData(relic);
                            if (relicData != null)
                            {
                                save.AddRelic(relicData);
                            }
                        }
                    }
                }
                if (saveState.cards != null)
                {
                    foreach (var card in saveState.cards)
                    {
                        if (card.cardName != null || card.cardName != "")
                        {
                            var cardData = save.GetAllGameData().FindCardData(card.cardName);
                            var spawn = cardData.GetSpawnCharacterData();

                            if (cardData != null && (spawn == null || (spawn != null && !spawn.IsChampion())))
                            {
                                var state = save.AddCardToDeck(cardData);
                                if (card.upgradeNames != null)
                                {
                                    foreach (string upgrade in card.upgradeNames)
                                    {
                                        if (upgrade != "")
                                        {
                                            CardUpgradeState cardUpgradeState = Activator.CreateInstance<CardUpgradeState>();
                                            CardUpgradeData data = save.GetAllGameData().FindCardUpgradeData(upgrade);
                                            cardUpgradeState.Setup(data);
                                            state.Upgrade(cardUpgradeState, null);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    [Serializable]
    public class StokerSaveState
    {
        public string[] relics;
        public CardWrapper[] cards;
    }
    [Serializable]
    public class CardWrapper
    {
        public string cardName;
        public string[] upgradeNames;
    }
}
