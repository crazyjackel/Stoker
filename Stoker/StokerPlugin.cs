using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using MonoMod.RuntimeDetour;
using UnityEngine;
using UnityEngine.UI;
using MonsterTrainModdingAPI.Utilities;
using MonsterTrainModdingAPI.Enum;
using System.IO;
using Stoker.Scripts;

namespace Stoker
{
    [BepInPlugin("io.github.crazyjackel.Stoker", "Stoker Editor Application", "0.0.2")]
    [BepInProcess("MonsterTrain.exe")]
    [BepInProcess("MtLinkHandler.exe")]
    public class StokerPlugin : BaseUnityPlugin, IClient, IDeckNotifications
    {
        #region Constant Groups
        //constant strings for assessing Bundle
        private const string bundleName = "stokerassetbundle";
        private const string assetName_Canvas = "DeckEditor";
        private const string assetName_SelectionButton = "CardSelection";

        //constant strings for finding unity objects
        private const string name_removeBackground = "Remove";
        private const string name_searchBarBackground = "Search";
        private const string name_addBackground = "Add";
        private const string name_removeButton = "Button";
        private const string name_searchBar = "InputField";
        private const string name_addButton = "Button";
        private const string name_mainBackground = "MainBackground";
        private const string name_secondaryBackground = "ScrollListBackGround";
        private const string name_secondaryBackground2 = "ScrollListBackGround_2";
        private const string name_viewport = "ScrollListViewport";
        private const string name_content = "Content";
        #endregion

        #region Provider Fields
        public SaveManager currentSave;
        private GameStateManager Game;
        private AllGameData data;
        #endregion

        #region Local Fields
        //Unused in this Version
        private List<Hook> myHooks;

        private GameObject Canvas;
        private GameObject SelectionButtonPrefab;
        private GameObject RemoveButton;
        private GameObject AddButton;
        private GameObject ButtonContent;
        private GameObject ButtonContent_2;
        private GameObject SearchBar;

        public CardState selectedCardState;
        public SelectionButton<CardState> selectedCardStateGameobject;
        public CardData selectedCardData;
        public SelectionButton<CardData> selectedCardDataGameobject;

        private List<DerivedCardStateSelectionButton> SelectionButtonsPool = new List<DerivedCardStateSelectionButton>();
        private List<DerivedCardDataSelectionButton> AllGameDataSelectionButtonsPool = new List<DerivedCardDataSelectionButton>();

        private string search;
        #endregion

        #region Unity Methods
        void Awake()
        {
            //Instantiate then Hide Canvas
            Canvas = GameObject.Instantiate(AssetBundleUtils.LoadAssetFromPath<GameObject>(bundleName, assetName_Canvas));
            DontDestroyOnLoad(Canvas);
            Canvas.SetActive(false);

            //Load Prefab to Instantiate Later
            SelectionButtonPrefab = AssetBundleUtils.LoadAssetFromPath<GameObject>(bundleName, assetName_SelectionButton);

            //Find local Buttons and add Listeners
            ButtonContent = Canvas.transform.Find($"{name_mainBackground}/{name_secondaryBackground}/{name_viewport}/{name_content}").gameObject;
            ButtonContent_2 = Canvas.transform.Find($"{name_mainBackground}/{name_secondaryBackground2}/{name_viewport}/{name_content}").gameObject;
            RemoveButton = Canvas.transform.Find($"{name_mainBackground}/{name_removeBackground}/{name_removeButton}").gameObject;
            RemoveButton.GetComponent<Button>().onClick.AddListener(AttemptToRemoveSelectedCard);
            AddButton = Canvas.transform.Find($"{name_mainBackground}/{name_addBackground}/{name_addButton}").gameObject;
            AddButton.GetComponent<Button>().onClick.AddListener(AttemptToAddSelectedCardData);
            SearchBar = Canvas.transform.Find($"{name_mainBackground}/{name_searchBarBackground}/{name_searchBar}").gameObject;
            SearchBar.GetComponent<InputField>().onValueChanged.AddListener(UpdateCardDataBase);

            //Subscribe as Client
            DepInjector.AddClient(this);
            this.Logger.LogInfo("Stoker Plugin Initialized");
        }


        void Update()
        {
            if (Input.GetKeyDown(KeyCode.P))
            {
                Canvas.SetActive(!Canvas.activeSelf);
            }
        }
        #endregion

        #region DeckNotifications Methods
        public void DeckChangedNotification(List<CardState> deck, int visibleDeckCount)
        {

            //Alphabetize deck
            List<CardState> query = deck.OrderBy(card => card.GetTitle()).ToList();

            //If deck is bigger, increase pool size
            if (query.Count > SelectionButtonsPool.Count)
            {
                for (int i = SelectionButtonsPool.Count; i < query.Count; i++)
                {
                    DerivedCardStateSelectionButton selectionButton = CreateCardStateSelectionButton(ButtonContent.transform);
                    selectionButton.OnClick += OnClickCardState;
                    selectionButton.UpdateTextFunc += GetCardStateName;
                    SelectionButtonsPool.Add(selectionButton);
                }
            }
            else
            {
                //Hide excessive Selection Buttons
                for (int i = query.Count; i < SelectionButtonsPool.Count; i++)
                {
                    SelectionButtonsPool[i].gameObject.SetActive(false);
                }
            }

            //Go through each SelectionButton, updating their text and internal card reference
            SelectionButton<CardState> sbi;
            for (int j = 0; j < query.Count; j++)
            {
                sbi = SelectionButtonsPool[j];
                sbi.gameObject.SetActive(true);
                sbi.UpdateText(query[j]);
            }
        }
        #endregion

        #region IClient Methods
        public void NewProviderAvailable(IProvider newProvider)
        {
            //Get Reference to SaveManager when Initialized
            if (DepInjector.MapProvider<SaveManager>(newProvider, ref this.currentSave))
            {
                //Subscribe to receive Deck Notifications
                currentSave.AddDeckNotifications(this);
                data = currentSave.GetAllGameData();
                this.Logger.LogInfo("SaveManager Initialized");
                InitializeCardDataBase();
            }

            //Get Reference to GameStateManager When Initialized
            if (DepInjector.MapProvider<GameStateManager>(newProvider, ref this.Game))
            {
                //Subscribe listener to Signal
                Game.runStartedSignal.AddListener(GameStartedListener);
            }
        }

        //Notify current save is null when SaveManager is removed
        public void ProviderRemoved(IProvider removeProvider)
        {
            if (removeProvider == (IProvider)currentSave)
            {
                currentSave = null;
            }
        }

        public void NewProviderFullyInstalled(IProvider newProvider)
        {
        }
        #endregion

        #region StokerPlugin Methods
        public DerivedCardDataSelectionButton CreateCardDataSelectionButton(Transform parent)
        {
            GameObject sbp = GameObject.Instantiate(SelectionButtonPrefab);
            DontDestroyOnLoad(sbp);
            sbp.transform.SetParent(parent);
            DerivedCardDataSelectionButton sb = sbp.AddComponent<DerivedCardDataSelectionButton>();
            sb.plugin = this;
            return sb;
        }

        public DerivedCardStateSelectionButton CreateCardStateSelectionButton(Transform parent)
        {
            GameObject sbp = GameObject.Instantiate(SelectionButtonPrefab);
            DontDestroyOnLoad(sbp);
            sbp.transform.SetParent(parent);
            DerivedCardStateSelectionButton sb = sbp.AddComponent<DerivedCardStateSelectionButton>();
            sb.plugin = this;
            return sb;
        }

        public void InitializeCardDataBase()
        {
            if (currentSave != null && data != null)
            {
                List<CardData> dataList = data.GetAllCardData().OrderBy(x => x.GetName()).ToList();
                for (int i = 0; i < dataList.Count; i++)
                {

                    DerivedCardDataSelectionButton selectionButton = CreateCardDataSelectionButton(ButtonContent_2.transform);
                    selectionButton.OnClick += OnClickCardData;
                    selectionButton.UpdateTextFunc += GetCardDataName;
                    selectionButton.UpdateText(dataList[i]);
                    AllGameDataSelectionButtonsPool.Add(selectionButton);
                }
            }
        }

        public void UpdateCardDataBase(string newSearch)
        {
            search = newSearch;
            if (currentSave != null && data != null)
            {
                List<CardData> dataList = data.GetAllCardData().OrderBy(x => x.GetName()).ToList();
                if (dataList.Count > AllGameDataSelectionButtonsPool.Count)
                {
                    for (int i = AllGameDataSelectionButtonsPool.Count; i < dataList.Count; i++)
                    {
                        DerivedCardDataSelectionButton selectionButton = CreateCardDataSelectionButton(ButtonContent_2.transform);
                        selectionButton.OnClick += OnClickCardData;
                        selectionButton.UpdateTextFunc += GetCardDataName;
                        selectionButton.UpdateText(dataList[i]);
                        AllGameDataSelectionButtonsPool.Add(selectionButton);
                    }
                }

                
                string searchCopy = string.Copy(search).ToLower();
                foreach (MTClan clan in (MTClan[])Enum.GetValues(typeof(MTClan)))
                {
                    if (searchCopy.Contains($"[{clan.ToString().ToLower()}]"))
                    {
                        Logger.LogInfo($"Search contains tag: [{clan}]");
                        dataList = dataList.Where((x) => (x.GetLinkedClassID() == ClanIDs.GetClanID(clan))).ToList();
                        searchCopy.Replace($"[{clan.ToString().ToLower()}]", "");
                    }
                }

                dataList = dataList.Where((x) => x.GetName().ToLower().Contains(searchCopy)).ToList();

                for (int i = 0; i < dataList.Count; i++)
                {
                    AllGameDataSelectionButtonsPool[i].gameObject.SetActive(true);
                    AllGameDataSelectionButtonsPool[i].UpdateText(dataList[i]);
                }

                for (int j = dataList.Count; j < AllGameDataSelectionButtonsPool.Count; j++)
                {
                    AllGameDataSelectionButtonsPool[j].gameObject.SetActive(false);
                }
            }
        }

        //Listener to GameStarting
        public void GameStartedListener(RunType type)
        {
            if (currentSave != null)
            {
                DeckChangedNotification(currentSave.GetDeckState(), currentSave.GetVisibleDeckCount());
            }
        }

        //Function to remove cards from current saveManager
        public void AttemptToRemoveSelectedCard()
        {
            if (currentSave != null && selectedCardState != null)
            {
                currentSave.RemoveCardFromDeck(selectedCardState);
            }
        }

        public void AttemptToAddSelectedCardData()
        {
            if (currentSave != null && selectedCardData != null)
            {
                currentSave.AddCardToDeck(selectedCardData);
            }
        }
        #endregion

        #region StokerPlugin Delegation Methods
        public void OnClickCardState(StokerPlugin plugin, SelectionButton<CardState> obj, CardState item)
        {
            plugin.selectedCardState = item;
            if (plugin.selectedCardStateGameobject != null)
            {
                plugin.selectedCardStateGameobject.button.colors = obj.button.colors;
            }
            obj.button.colors = new ColorBlock
            {
                colorMultiplier = obj.button.colors.colorMultiplier,
                normalColor = Color.red,
                disabledColor = Color.red,
                highlightedColor = Color.red,
                pressedColor = Color.red
            };
        }
        public void OnClickCardData(StokerPlugin plugin, SelectionButton<CardData> obj, CardData item)
        {
            plugin.selectedCardData = item;
            if (plugin.selectedCardStateGameobject != null)
            {
                plugin.selectedCardStateGameobject.button.colors = obj.button.colors;
            }
            obj.button.colors = new ColorBlock
            {
                colorMultiplier = obj.button.colors.colorMultiplier,
                normalColor = Color.red,
                disabledColor = Color.red,
                highlightedColor = Color.red,
                pressedColor = Color.red
            };
        }
        public string GetCardStateName(CardState card)
        {
            string modifiers = "";
            CardStateModifiers st = card.GetCardStateModifiers();
            if (st != null)
            {
                List<CardUpgradeState> upgrades = st.GetCardUpgrades();
                if (upgrades.Count > 0)
                {
                    modifiers += ":";
                }
                for (int index = 0; index < upgrades.Count; index++)
                {
                    modifiers += upgrades[index].GetAssetName() + ((index != upgrades.Count - 1) ? "|" : "");
                }
            }
            string text2 = $"{card.GetTitle()}{modifiers}";
            return text2;
        }
        public string GetCardDataName(CardData card)
        {
            return card.GetName();
        }
        #endregion
    }
}
