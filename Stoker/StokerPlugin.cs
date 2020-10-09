using Assets.IUnified;
using BepInEx;
using HarmonyLib;
using MonsterTrainModdingAPI.Interfaces;
using MonsterTrainModdingAPI.Managers;
using MonsterTrainModdingAPI.Utilities;
using Stoker.Scripts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Assertions.Must;
using UnityEngine.UI;

namespace Stoker
{
    /// <summary>
    /// How Stoker Works Summary:
    /// 1. Load the Assets and Store References to Parts of those Assets
    /// 2. Add Listeners and Subscribe for Notifications to call functions
    /// 3. Call those functions updating internal information
    /// 
    /// Possible Future Optimizations:
    /// Move Database Updating onto an IEnumerator and Start Coroutines for them
    /// Remove Useless Calls
    /// Simplify Code
    /// Recomment on alot of code, it's bad.
    /// Move alot of functions into their own seperate classes... god object oriented can suck for stuff where you just want to make function calls
    /// 
    /// Possible Future Features:
    /// Auto-Reselect for Quicker Removal
    /// Fix that problem with selection looking weird
    /// </summary>
    [BepInPlugin("io.github.crazyjackel.Stoker", "Stoker Deck Editor Application", "1.2.0")]
    [BepInProcess("MonsterTrain.exe")]
    [BepInProcess("MtLinkHandler.exe")]
    public class StokerPlugin : BaseUnityPlugin, IClient, IDeckNotifications, IInitializable, IRelicNotifications
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
        private const string name_Button = "Button";
        private const string name_searchBar = "InputField";
        private const string name_duplicateBackground = "Duplicate";
        private const string name_addRelicBackground = "AddRelic";
        private const string name_removeRelicBackground = "RemoveRelic";
        private const string name_addUpgradeBackground = "AddUpgrade";
        private const string name_removeUpgradeBackground = "RemoveUpgrade";
        private const string name_mainBackground = "MainBackground";
        private const string name_secondaryBackground = "ScrollListBackGround";
        private const string name_secondaryBackground2 = "ScrollListBackGround_2";
        private const string name_secondaryBackground3 = "ScrollListBackGround_3";
        private const string name_secondaryBackground4 = "ScrollListBackGround_4";
        private const string name_secondaryBackground5 = "ScrollListBackGround_5";
        private const string name_secondaryBackground6 = "ScrollListBackGround_6";
        private const string name_viewport = "ScrollListViewport";
        private const string name_content = "Content";
        #endregion

        #region Provider Fields
        public SaveManager currentSave;
        public RelicManager relicManager;
        private GameStateManager Game;
        private AllGameData data;
        #endregion

        #region Local Fields
        private GameObject Canvas;
        private GameObject SelectionButtonPrefab;
        private GameObject RemoveButton;
        private GameObject AddButton;
        private GameObject DuplicateButton;
        private GameObject AddRelicButton;
        private GameObject RemoveRelicButton;
        private GameObject AddUpgradeButton;
        private GameObject RemoveUpgradeButton;
        private GameObject DeckContent;
        private GameObject CardDatabaseContent;
        private GameObject RelicDatabaseContent;
        private GameObject UpgradeDatabaseContent;
        private GameObject RelicContent;
        private GameObject UpgradeContent;
        private GameObject SearchBar;

        public CardState selectedCardState;
        public SelectionButton<CardState> selectedCardStateGameobject;
        public CardData selectedCardData;
        public SelectionButton<CardData> selectedCardDataGameobject;
        public RelicData selectedRelicData;
        public SelectionButton<RelicData> selectedRelicDataGameobject;
        public RelicState selectedRelicState;
        public SelectionButton<RelicState> selectedRelicStateGameobject;
        public CardUpgradeData selectedUpgradeData;
        public SelectionButton<CardUpgradeData> selectedUpgradeDataGameobject;
        public CardUpgradeState selectedUpgradeState;
        public SelectionButton<CardUpgradeState> selectedUpgradeStateGameobject;

        private List<DerivedCardStateSelectionButton> SelectionButtonsPool = new List<DerivedCardStateSelectionButton>();
        private List<DerivedRelicStateSelectionButton> RelicStateSelectionButtonsPool = new List<DerivedRelicStateSelectionButton>();
        private List<DerivedRelicDataSelectionButton> RelicDataSelectionButtonsPool = new List<DerivedRelicDataSelectionButton>();
        private List<DerivedCardDataSelectionButton> AllGameDataSelectionButtonsPool = new List<DerivedCardDataSelectionButton>();
        private List<DerivedUpgradeDataSelectionButton> UpgradeDataSelectionButtonsPool = new List<DerivedUpgradeDataSelectionButton>();
        private List<DerivedUpgradeStateSelectionButton> UpgradeStateSelectionButtonsPool = new List<DerivedUpgradeStateSelectionButton>();

        private string search = "";
        private BundleAssetLoadingInfo info;
        private List<CardUpgradeData> applyableUpgrades = new List<CardUpgradeData>();
        private Color grey = new Color(74f / 255, 78f / 255, 84f / 255);
        private bool IsInit = false;
        #endregion

        #region Unity Methods
        public void Initialize()
        {
            Logger.Log(BepInEx.Logging.LogLevel.All, "Initializing AssetBundle");
            //Load Assets
            var assembly = Assembly.GetExecutingAssembly();
            PluginManager.AssemblyNameToPath.TryGetValue(assembly.FullName, out string basePath);
            info = new BundleAssetLoadingInfo
            {
                PluginPath = basePath,
                FilePath = bundleName,
            };
            BundleManager.RegisterBundle(GUIDGenerator.GenerateDeterministicGUID(info.FullPath), info);
            Logger.Log(BepInEx.Logging.LogLevel.All, "Loaded AssetBundle");

            //Instantiate then Hide Canvas
            var GameObj = BundleManager.LoadAssetFromBundle(info, assetName_Canvas) as GameObject;
            Logger.Log(BepInEx.Logging.LogLevel.All, "Loaded Main Asset: " + GameObj.name);
            Canvas = GameObject.Instantiate(GameObj);
            DontDestroyOnLoad(Canvas);
            Canvas.SetActive(false);

            //Load Prefab to Instantiate Later
            SelectionButtonPrefab = BundleManager.LoadAssetFromBundle(info, assetName_SelectionButton) as GameObject;
            Logger.Log(BepInEx.Logging.LogLevel.All, "Loaded Button Prefab");

            //Find local Buttons and add Listeners
            //Load Content where to add Selection Buttons
            DeckContent = Canvas.transform.Find($"{name_mainBackground}/{name_secondaryBackground}/{name_viewport}/{name_content}").gameObject;
            Logger.Log(BepInEx.Logging.LogLevel.All, "Found Deck Content");
            CardDatabaseContent = Canvas.transform.Find($"{name_mainBackground}/{name_secondaryBackground2}/{name_viewport}/{name_content}").gameObject;
            Logger.Log(BepInEx.Logging.LogLevel.All, "Found Card Database Content");
            RelicDatabaseContent = Canvas.transform.Find($"{name_mainBackground}/{name_secondaryBackground3}/{name_viewport}/{name_content}").gameObject;
            Logger.Log(BepInEx.Logging.LogLevel.All, "Found Relic Database Content");
            RelicContent = Canvas.transform.Find($"{name_mainBackground}/{name_secondaryBackground4}/{name_viewport}/{name_content}").gameObject;
            Logger.Log(BepInEx.Logging.LogLevel.All, "Found Relic Content");
            UpgradeContent = Canvas.transform.Find($"{name_mainBackground}/{name_secondaryBackground5}/{name_viewport}/{name_content}").gameObject;
            Logger.Log(BepInEx.Logging.LogLevel.All, "Found Upgrade Content");
            UpgradeDatabaseContent = Canvas.transform.Find($"{name_mainBackground}/{name_secondaryBackground6}/{name_viewport}/{name_content}").gameObject;
            Logger.Log(BepInEx.Logging.LogLevel.All, "Found Upgrade Database Content");

            //Get Buttons and Input Fields
            RemoveButton = Canvas.transform.Find($"{name_mainBackground}/{name_removeBackground}/{name_Button}").gameObject;
            Logger.Log(BepInEx.Logging.LogLevel.All, "Found Remove Button");
            RemoveButton.GetComponent<Button>().onClick.AddListener(AttemptToRemoveSelectedCard);
            AddButton = Canvas.transform.Find($"{name_mainBackground}/{name_addBackground}/{name_Button}").gameObject;
            Logger.Log(BepInEx.Logging.LogLevel.All, "Found Add Button");
            AddButton.GetComponent<Button>().onClick.AddListener(AttemptToAddSelectedCardData);
            DuplicateButton = Canvas.transform.Find($"{name_mainBackground}/{name_duplicateBackground}/{name_Button}").gameObject;
            Logger.Log(BepInEx.Logging.LogLevel.All, "Found Duplication Button");
            DuplicateButton.GetComponent<Button>().onClick.AddListener(AttemptToDuplicateSelectedCard);
            AddRelicButton = Canvas.transform.Find($"{name_mainBackground}/{name_addRelicBackground}/{name_Button}").gameObject;
            Logger.Log(BepInEx.Logging.LogLevel.All, "Found Add Relic Button");
            AddRelicButton.GetComponent<Button>().onClick.AddListener(AttemptToAddSelectedRelicData);
            RemoveRelicButton = Canvas.transform.Find($"{name_mainBackground}/{name_removeRelicBackground}/{name_Button}").gameObject;
            Logger.Log(BepInEx.Logging.LogLevel.All, "Found Remove Relic Button");
            RemoveRelicButton.GetComponent<Button>().onClick.AddListener(AttemptToRemoveSelectedRelic);
            AddUpgradeButton = Canvas.transform.Find($"{name_mainBackground}/{name_addUpgradeBackground}/{name_Button}").gameObject;
            Logger.Log(BepInEx.Logging.LogLevel.All, "Found Add Upgrade Button");
            AddUpgradeButton.GetComponent<Button>().onClick.AddListener(AttemptToUpgradeCardState);
            RemoveUpgradeButton = Canvas.transform.Find($"{name_mainBackground}/{name_removeUpgradeBackground}/{name_Button}").gameObject;
            Logger.Log(BepInEx.Logging.LogLevel.All, "Found Remove Upgrade Button");
            RemoveUpgradeButton.GetComponent<Button>().onClick.AddListener(AttemptToRemoveUpgradeState);
            SearchBar = Canvas.transform.Find($"{name_mainBackground}/{name_searchBarBackground}/{name_searchBar}").gameObject;
            Logger.Log(BepInEx.Logging.LogLevel.All, "Found Search Bar");
            SearchBar.GetComponent<InputField>().onValueChanged.AddListener(UpdateDatabases);
            Logger.Log(BepInEx.Logging.LogLevel.All, "Found Elements");
            //Subscribe as Client
            DepInjector.AddClient(this);
            this.Logger.LogInfo("Stoker Plugin Initialized");
        }


        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Equals))
            {
                Canvas.SetActive(!Canvas.activeSelf);
            }
        }
        #endregion

        #region DeckNotifications Methods
        public void DeckChangedNotification(List<CardState> deck, int visibleDeckCount)
        {
            //Alphabetize deck, remove champion cards as they crash the game
            List<CardState> query = deck.OrderBy(card => card.GetTitle()).Where(x => !x.IsChampionCard()).ToList();

            //If deck is bigger, increase pool size
            if (query.Count > SelectionButtonsPool.Count)
            {
                for (int i = SelectionButtonsPool.Count; i < query.Count; i++)
                {
                    DerivedCardStateSelectionButton selectionButton = CreateCardStateSelectionButton(DeckContent.transform);

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

        #region RelicNotifications Methods
        public void RelicAddedNotification(List<RelicState> relics, RelicState newRelic, Team.Type team)
        {
            UpdateRelics();
        }

        public void RelicTriggeredNotification(RelicState relic, IRelicEffect triggeredEffect)
        {

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
                this.Logger.LogInfo("Relic Manager Initialized");
                InitializeRelicDataBase();
                IsInit = false;
            }

            //Get Reference to GameStateManager When Initialized
            if (DepInjector.MapProvider<GameStateManager>(newProvider, ref this.Game))
            {
                //Subscribe listener to Signal
                Game.runStartedSignal.AddListener(GameStartedListener);
            }
            if (DepInjector.MapProvider<RelicManager>(newProvider, ref this.relicManager))
            {
                relicManager.AddRelicNotifications(this);
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

        #region SelectionButtonCreators
        /// <summary>
        /// Creates a Selection Button for CardData
        /// </summary>
        /// <param name="parent"></param>
        /// <returns></returns>
        public DerivedUpgradeStateSelectionButton CreateUpgradeStateSelectionButton(Transform parent)
        {
            GameObject sbp = GameObject.Instantiate(SelectionButtonPrefab);
            DontDestroyOnLoad(sbp);
            sbp.transform.SetParent(parent);
            DerivedUpgradeStateSelectionButton sb = sbp.AddComponent<DerivedUpgradeStateSelectionButton>();
            sb.plugin = this;
            sb.OnClick += OnClickUpgradeState;
            sb.UpdateTextFunc += GetUpgradeStateName;
            return sb;
        }
        /// <summary>
        /// Creates a Selection Button for CardData
        /// </summary>
        /// <param name="parent"></param>
        /// <returns></returns>
        public DerivedUpgradeDataSelectionButton CreateUpgradeDataSelectionButton(Transform parent)
        {
            GameObject sbp = GameObject.Instantiate(SelectionButtonPrefab);
            DontDestroyOnLoad(sbp);
            sbp.transform.SetParent(parent);
            DerivedUpgradeDataSelectionButton sb = sbp.AddComponent<DerivedUpgradeDataSelectionButton>();
            sb.plugin = this;
            sb.OnClick += OnClickUpgradeData;
            sb.UpdateTextFunc += GetUpgradeDataName;
            return sb;
        }
        /// <summary>
        /// Creates a Selection Button for CardData
        /// </summary>
        /// <param name="parent"></param>
        /// <returns></returns>
        public DerivedCardDataSelectionButton CreateCardDataSelectionButton(Transform parent)
        {
            GameObject sbp = GameObject.Instantiate(SelectionButtonPrefab);
            DontDestroyOnLoad(sbp);
            sbp.transform.SetParent(parent);
            DerivedCardDataSelectionButton sb = sbp.AddComponent<DerivedCardDataSelectionButton>();
            sb.plugin = this;
            sb.OnClick += OnClickCardData;
            sb.UpdateTextFunc += GetCardDataName;
            return sb;
        }
        /// <summary>
        /// Creates a Selection Button for CardState
        /// </summary>
        /// <param name="parent"></param>
        /// <returns></returns>
        public DerivedCardStateSelectionButton CreateCardStateSelectionButton(Transform parent)
        {
            GameObject sbp = GameObject.Instantiate(SelectionButtonPrefab);
            DontDestroyOnLoad(sbp);
            sbp.transform.SetParent(parent);
            DerivedCardStateSelectionButton sb = sbp.AddComponent<DerivedCardStateSelectionButton>();
            sb.plugin = this;
            sb.OnClick += OnClickCardState;
            sb.UpdateTextFunc += GetCardStateName;
            return sb;
        }
        public DerivedRelicStateSelectionButton CreateRelicStateSelectionButton(Transform parent)
        {
            GameObject sbp = GameObject.Instantiate(SelectionButtonPrefab);
            DontDestroyOnLoad(sbp);
            sbp.transform.SetParent(parent);
            DerivedRelicStateSelectionButton sb = sbp.AddComponent<DerivedRelicStateSelectionButton>();
            sb.plugin = this;
            sb.OnClick += OnClickRelicState;
            sb.UpdateTextFunc += getRelicStateName;
            return sb;
        }
        public DerivedRelicDataSelectionButton CreateRelicDataSelectionButton(Transform parent)
        {
            GameObject sbp = GameObject.Instantiate(SelectionButtonPrefab);
            DontDestroyOnLoad(sbp);
            sbp.transform.SetParent(parent);
            DerivedRelicDataSelectionButton sb = sbp.AddComponent<DerivedRelicDataSelectionButton>();
            sb.plugin = this;
            sb.OnClick += OnClickRelicData;
            sb.UpdateTextFunc += getRelicDataName;
            return sb;
        }
        #endregion

        #region Database Initializers
        public void InitializeCardDataBase()
        {
            if (currentSave != null && data != null)
            {
                List<CardData> dataList = data.GetAllCardData().Where(x => x.GetRarity() != CollectableRarity.Champion).OrderBy(x => x.GetName()).ToList();
                for (int i = 0; i < dataList.Count; i++)
                {

                    DerivedCardDataSelectionButton selectionButton = CreateCardDataSelectionButton(CardDatabaseContent.transform);

                    selectionButton.UpdateText(dataList[i]);
                    AllGameDataSelectionButtonsPool.Add(selectionButton);
                }
            }
        }
        private void InitializeRelicDataBase()
        {
            if (data != null && currentSave != null)
            {
                List<CollectableRelicData> dataList = data.GetAllCollectableRelicData().OrderBy(x => x.GetName()).ToList();
                for (int i = 0; i < dataList.Count; i++)
                {
                    DerivedRelicDataSelectionButton selectionButton = CreateRelicDataSelectionButton(RelicDatabaseContent.transform);
                    selectionButton.UpdateText(dataList[i]);
                    RelicDataSelectionButtonsPool.Add(selectionButton);
                }
            }
        }
        #endregion

        #region Listeners
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
            if (currentSave != null && selectedCardState != null && selectedCardStateGameobject != null && selectedCardStateGameobject.gameObject.activeSelf)
            {
                currentSave.RemoveCardFromDeck(selectedCardState);
            }
        }
        public void AttemptToDuplicateSelectedCard()
        {
            if (currentSave != null && selectedCardState != null && selectedCardStateGameobject.gameObject.activeSelf)
            {
                currentSave.AddCardToDeck(data.FindCardData(selectedCardState.GetCardDataID()), selectedCardState.GetCardStateModifiers(), true);
            }
        }
        public void AttemptToRemoveSelectedRelic()
        {
            if (currentSave != null && selectedRelicState != null && selectedRelicStateGameobject.gameObject.activeSelf)
            {
                SaveData savedata = (SaveData)AccessTools.PropertyGetter(typeof(SaveManager), "ActiveSaveData").Invoke(currentSave, new object[] { });
                Logger.Log(BepInEx.Logging.LogLevel.All, "Removing Blessing " + selectedRelicState.GetName());
                savedata.GetBlessings().Remove(selectedRelicState);
                RelicManager.RelicAdded.Dispatch(currentSave.GetCollectedRelics(), null, Team.Type.Monsters);
            }
        }
        public void AttemptToAddSelectedCardData()
        {
            if (currentSave != null && selectedCardData != null && selectedCardDataGameobject.gameObject.activeSelf)
            {
                currentSave.AddCardToDeck(selectedCardData);
            }
        }
        public void AttemptToAddSelectedRelicData()
        {
            if (currentSave != null && selectedRelicData != null && selectedRelicDataGameobject.gameObject.activeSelf)
            {
                currentSave.AddRelic(selectedRelicData);
            }
        }
        public void AttemptToUpgradeCardState()
        {
            if (selectedCardState != null && selectedUpgradeData != null)
            {
                CardUpgradeState cardUpgradeState = Activator.CreateInstance<CardUpgradeState>();
                cardUpgradeState.Setup(selectedUpgradeData);
                selectedCardState.Upgrade(cardUpgradeState, null);
                selectedCardState.UpdateCardBodyText(currentSave);
                UpdateUpgrades();
                RefreshSelectionCardStateSelectionButtons();
            }
        }
        public void AttemptToRemoveUpgradeState()
        {
            if (selectedCardState != null && selectedUpgradeState != null)
            {
                selectedCardState.GetCardStateModifiers().RemoveUpgrade(selectedUpgradeState);
                selectedCardState.UpdateCardBodyText(currentSave);
                UpdateUpgrades();
                RefreshSelectionCardStateSelectionButtons();
            }
        }
        #endregion

        #region Update Functions
        /// <summary>
        /// Update the Upgrades based on CardState
        /// </summary>
        public void UpdateUpgrades()
        {
            if (currentSave != null && selectedCardState != null)
            {
                List<CardUpgradeState> query = selectedCardState.GetCardStateModifiers().GetCardUpgrades().OrderBy(x => x.GetUpgradeTitle()).ToList();

                if (query.Count > UpgradeStateSelectionButtonsPool.Count)
                {
                    for (int i = UpgradeStateSelectionButtonsPool.Count; i < query.Count; i++)
                    {
                        DerivedUpgradeStateSelectionButton selectionButton = CreateUpgradeStateSelectionButton(UpgradeContent.transform);
                        UpgradeStateSelectionButtonsPool.Add(selectionButton);
                    }
                }
                else
                {
                    //Hide excessive Selection Buttons
                    for (int i = query.Count; i < UpgradeStateSelectionButtonsPool.Count; i++)
                    {
                        UpgradeStateSelectionButtonsPool[i].gameObject.SetActive(false);
                    }
                }
                //Go through each SelectionButton, updating their text and internal card reference
                SelectionButton<CardUpgradeState> sbi;
                for (int j = 0; j < query.Count; j++)
                {
                    sbi = UpgradeStateSelectionButtonsPool[j];
                    sbi.gameObject.SetActive(true);
                    sbi.UpdateText(query[j]);
                }
            }
        }
        /// <summary>
        /// Update and Filter out the Upgrade Database by String
        /// </summary>
        /// <param name="search"></param>
        public void UpdateUpgradeDatabaseBySearchString(string search)
        {
            List<CardUpgradeData> query = applyableUpgrades.Where(x => x.GetUpgradeTitleKey().Localize(null).ToLower().Contains(search.ToLower())).ToList();
            for (int i = 0; i < query.Count; i++)
            {
                UpgradeDataSelectionButtonsPool[i].gameObject.SetActive(true);
                UpgradeDataSelectionButtonsPool[i].UpdateText(query[i]);
            }
            for (int j = query.Count; j < UpgradeDataSelectionButtonsPool.Count; j++)
            {
                UpgradeDataSelectionButtonsPool[j].gameObject.SetActive(false);
            }
        }
        /// <summary>
        /// Update and Filter out the Upgrade Database by Cardstate
        /// </summary>
        /// <param name="cardState"></param>
        public void UpdateUpgradeDatabaseByCardState(CardState cardState)
        {
            if (data != null)
            {
                List<CardUpgradeData> cardUpgradeDatas = (List<CardUpgradeData>)AccessTools.Field(typeof(AllGameData), "cardUpgradeDatas").GetValue(data);
                applyableUpgrades = cardUpgradeDatas.Where(x => x.GetFilters().All(y => y.FilterCard<CardState>(selectedCardState, relicManager))).Where(x => GetUpgradeText(x) != null && GetUpgradeText(x) != "").ToList();

                if (applyableUpgrades.Count > UpgradeDataSelectionButtonsPool.Count)
                {
                    for (int i = UpgradeDataSelectionButtonsPool.Count; i < applyableUpgrades.Count; i++)
                    {
                        DerivedUpgradeDataSelectionButton selectionButton = CreateUpgradeDataSelectionButton(UpgradeDatabaseContent.transform);
                        UpgradeDataSelectionButtonsPool.Add(selectionButton);
                    }
                }
                else
                {
                    //Hide excessive Selection Buttons
                    for (int i = applyableUpgrades.Count; i < UpgradeDataSelectionButtonsPool.Count; i++)
                    {
                        UpgradeDataSelectionButtonsPool[i].gameObject.SetActive(false);
                    }
                }
                //Go through each SelectionButton, updating their text and internal card reference
                SelectionButton<CardUpgradeData> sbi;
                for (int j = 0; j < applyableUpgrades.Count; j++)
                {
                    sbi = UpgradeDataSelectionButtonsPool[j];
                    sbi.gameObject.SetActive(true);
                    sbi.UpdateText(applyableUpgrades[j]);
                }
                UpdateUpgradeDatabaseBySearchString(search);
            }
        }
        /// <summary>
        /// Updates the Current Relic States
        /// </summary>
        public void UpdateRelics()
        {
            if (currentSave != null)
            {
                List<RelicState> query = currentSave.GetCollectedRelics().OrderBy(relic => relic.GetName()).ToList();

                //If deck is bigger, increase pool size
                if (query.Count > RelicStateSelectionButtonsPool.Count)
                {
                    for (int i = RelicStateSelectionButtonsPool.Count; i < query.Count; i++)
                    {
                        DerivedRelicStateSelectionButton selectionButton = CreateRelicStateSelectionButton(RelicContent.transform);
                        RelicStateSelectionButtonsPool.Add(selectionButton);
                    }
                }
                else
                {
                    //Hide excessive Selection Buttons
                    for (int i = query.Count; i < RelicStateSelectionButtonsPool.Count; i++)
                    {
                        RelicStateSelectionButtonsPool[i].gameObject.SetActive(false);
                    }
                }

                //Go through each SelectionButton, updating their text and internal card reference
                SelectionButton<RelicState> sbi;
                for (int j = 0; j < query.Count; j++)
                {
                    sbi = RelicStateSelectionButtonsPool[j];
                    sbi.gameObject.SetActive(true);
                    sbi.UpdateText(query[j]);
                }
            }
        }
        /// <summary>
        /// Updates the Databases based on search parameter
        /// </summary>
        /// <param name="newSearch"></param>
        public void UpdateDatabases(string newSearch)
        {
            search = newSearch;
            if (currentSave != null && data != null)
            {
                //Go Through DataList Realigning new Items, in case a mod has been enabled or disabled
                List<CardData> dataList = data.GetAllCardData().Where(x => x.GetRarity() != CollectableRarity.Champion).OrderBy(x => x.GetName()).ToList();
                if (dataList.Count != AllGameDataSelectionButtonsPool.Count)
                {
                    for (int j = 0; j < AllGameDataSelectionButtonsPool.Count; j++)
                    {
                        AllGameDataSelectionButtonsPool[j].UpdateText(dataList[j]);
                    }
                    for (int i = AllGameDataSelectionButtonsPool.Count; i < dataList.Count; i++)
                    {
                        DerivedCardDataSelectionButton selectionButton = CreateCardDataSelectionButton(CardDatabaseContent.transform);
                        selectionButton.UpdateText(dataList[i]);
                        AllGameDataSelectionButtonsPool.Add(selectionButton);
                    }
                }
                List<CollectableRelicData> relicDataList = data.GetAllCollectableRelicData().OrderBy(x => x.GetName()).ToList();
                if (relicDataList.Count != RelicDataSelectionButtonsPool.Count)
                {
                    for (int j = 0; j < RelicDataSelectionButtonsPool.Count; j++)
                    {
                        RelicDataSelectionButtonsPool[j].UpdateText(relicDataList[j]);
                    }
                    for (int i = RelicDataSelectionButtonsPool.Count; i < relicDataList.Count; i++)
                    {
                        DerivedRelicDataSelectionButton selectionButton = CreateRelicDataSelectionButton(RelicDatabaseContent.transform);

                        selectionButton.UpdateText(relicDataList[i]);
                        RelicDataSelectionButtonsPool.Add(selectionButton);
                    }
                }

                //Do Search Culling (WIP)
                string searchCopy = string.Copy(search).ToLower();
                foreach (ClassData clan in data.GetAllClassDatas())
                {
                    if (searchCopy.Contains($"[{clan.GetTitle().ToLower()}]"))
                    {
                        Logger.LogInfo($"Search contains tag: [{clan}]");
                        dataList = dataList.Where((x) => (x.GetLinkedClass() == clan)).ToList();
                        searchCopy.Replace($"[{clan.ToString().ToLower()}]", "");
                    }
                }
                //Go through each database and cull based on search results
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
                relicDataList = relicDataList.Where(x => x.GetName().ToLower().Contains(searchCopy)).ToList();
                for (int ri = 0; ri < relicDataList.Count; ri++)
                {
                    RelicDataSelectionButtonsPool[ri].gameObject.SetActive(true);
                    RelicDataSelectionButtonsPool[ri].UpdateText(relicDataList[ri]);
                }
                for (int rj = relicDataList.Count; rj < RelicDataSelectionButtonsPool.Count; rj++)
                {
                    RelicDataSelectionButtonsPool[rj].gameObject.SetActive(false);
                }
                UpdateUpgradeDatabaseBySearchString(search);
            }
        }

        public void RefreshSelectionCardStateSelectionButtons()
        {
            foreach (DerivedCardStateSelectionButton button in SelectionButtonsPool)
            {
                button.UpdateText(button.item);
            }
        }
        #endregion

        #endregion

        #region StokerPlugin Delegation Methods
        //I probably could convert these functions to be generic and the selected objects to a type dictionary... but I am lazy, so too bad.
        public void OnClickCardState(StokerPlugin plugin, SelectionButton<CardState> obj, CardState item)
        {
            //Sets the Selected Card State to this
            plugin.selectedCardState = item;
            //Update old Card State's GameObj's color
            if (plugin.selectedCardStateGameobject != null)
            {
                plugin.selectedCardStateGameobject.button.colors = getGreyColorBlock();
            }
            //Set Selected Card State to this
            plugin.selectedCardStateGameobject = obj;
            //Update Colors
            obj.button.colors = getRedColorBlock();
            selectedUpgradeData = null;
            selectedUpgradeState = null;
            if (plugin.selectedUpgradeStateGameobject != null)
            {
                plugin.selectedUpgradeStateGameobject.button.colors = getGreyColorBlock();
                plugin.selectedUpgradeStateGameobject = null;
            }
            if (plugin.selectedUpgradeDataGameobject != null)
            {
                plugin.selectedUpgradeDataGameobject.button.colors = getGreyColorBlock();
                plugin.selectedUpgradeDataGameobject = null;
            }
            selectedCardState.UpdateCardBodyText(currentSave);
            UpdateUpgrades();
            UpdateUpgradeDatabaseByCardState(selectedCardState);
            //Prevents a desync due to deck issues.
            if (!IsInit)
            {
                DeckChangedNotification(currentSave.GetDeckState(), currentSave.GetVisibleDeckCount());
                IsInit = true;
            }
        }
        public void OnClickCardData(StokerPlugin plugin, SelectionButton<CardData> obj, CardData item)
        {
            plugin.selectedCardData = item;
            if (plugin.selectedCardDataGameobject != null)
            {
                plugin.selectedCardDataGameobject.button.colors = getGreyColorBlock();
            }
            plugin.selectedCardDataGameobject = obj;
            obj.button.colors = getRedColorBlock();
        }
        public void OnClickRelicData(StokerPlugin plugin, SelectionButton<RelicData> obj, RelicData item)
        {
            plugin.selectedRelicData = item;
            if (plugin.selectedRelicDataGameobject != null)
            {
                plugin.selectedRelicDataGameobject.button.colors = getGreyColorBlock();
            }
            plugin.selectedRelicDataGameobject = obj;
            obj.button.colors = getRedColorBlock();
        }
        public void OnClickRelicState(StokerPlugin plugin, SelectionButton<RelicState> obj, RelicState item)
        {
            plugin.selectedRelicState = item;
            if (plugin.selectedRelicStateGameobject != null)
            {
                plugin.selectedRelicStateGameobject.button.colors = getGreyColorBlock();
            }
            plugin.selectedRelicStateGameobject = obj;
            obj.button.colors = getRedColorBlock();
        }
        public void OnClickUpgradeState(StokerPlugin plugin, SelectionButton<CardUpgradeState> obj, CardUpgradeState item)
        {
            plugin.selectedUpgradeState = item;
            if (plugin.selectedUpgradeStateGameobject != null)
            {
                plugin.selectedUpgradeStateGameobject.button.colors = getGreyColorBlock();
            }
            plugin.selectedUpgradeStateGameobject = obj;
            obj.button.colors = getRedColorBlock();
        }
        public void OnClickUpgradeData(StokerPlugin plugin, SelectionButton<CardUpgradeData> obj, CardUpgradeData item)
        {
            plugin.selectedUpgradeData = item;
            if (plugin.selectedUpgradeDataGameobject != null)
            {
                Color color = new Color(74f / 255, 78f / 255, 84f / 255);
                plugin.selectedUpgradeDataGameobject.button.colors = getGreyColorBlock();
            }
            plugin.selectedUpgradeDataGameobject = obj;
            obj.button.colors = getRedColorBlock();
        }
        public string GetCardStateName(CardState card)
        {
            string modifiers = "";
            CardStateModifiers st = card.GetCardStateModifiers();
            if (st != null)
            {
                List<CardUpgradeState> upgrades = st.GetCardUpgrades().OrderBy(x => x.GetUpgradeTitle()).ToList();
                if (upgrades.Count > 0)
                {
                    modifiers += ":";
                }
                for (int index = 0; index < upgrades.Count; index++)
                {
                    modifiers += GetUpgradeText(data.FindCardUpgradeData(upgrades[index].GetCardUpgradeDataId())) + ((index != upgrades.Count - 1) ? ", " : "");
                }
            }
            string text2 = $"{card.GetTitle()}{modifiers}";
            return text2;
        }
        public string GetCardDataName(CardData card)
        {
            return card.GetName();
        }
        private string getRelicStateName(RelicState arg)
        {
            return arg.GetName();
        }
        public string getRelicDataName(RelicData relic)
        {
            return relic.GetName();
        }
        private string GetUpgradeDataName(CardUpgradeData arg)
        {
            return GetUpgradeText(arg);
        }
        public string GetUpgradeStateName(CardUpgradeState arg)
        {
            return GetUpgradeText(data.FindCardUpgradeData(arg.GetCardUpgradeDataId()));
        }
        #endregion

        public static string GetUpgradeText(CardUpgradeData arg)
        {
            string val = arg.GetUpgradeTitleKey().Localize(null);
            if (val.Length == 0)
            {
                for (int i = 0; i < arg.GetTraitDataUpgrades().Count; i++)
                {
                    val += arg.GetTraitDataUpgrades()[i].GetTraitStateName() + ((i != arg.GetTraitDataUpgrades().Count - 1) ? ", " : "");
                }
                if (val.Length != 0 && arg.GetStatusEffectUpgrades().Count > 0)
                {
                    val += " & ";
                }
                for (int j = 0; j < arg.GetStatusEffectUpgrades().Count; j++)
                {
                    val += StatusEffectManager.GetLocalizedName(arg.GetStatusEffectUpgrades()[j].statusId, arg.GetStatusEffectUpgrades()[j].count) 
                        + ((j != arg.GetStatusEffectUpgrades().Count - 1) ? ", " : "");
                }
                if (val.Length != 0 && arg.GetTriggerUpgrades().Count > 0)
                {
                    val += " & ";
                }
                for (int k = 0; k < arg.GetTriggerUpgrades().Count; k++)
                {
                    val += CharacterTriggerData.GetKeywordText(arg.GetTriggerUpgrades()[k].GetTrigger()) 
                        + ": " 
                        + arg.GetTriggerUpgrades()[k].GetDescriptionKey().Localize(null) 
                        + ((k != arg.GetTriggerUpgrades().Count - 1) ? ", " : "");
                }
                if (val.Length != 0 && arg.GetCardTriggerUpgrades().Count > 0)
                {
                    val += " & ";
                }
                for (int l = 0; l < arg.GetCardTriggerUpgrades().Count; l++)
                {
                    CardTriggerTypeMethods.GetLocalizedName(arg.GetCardTriggerUpgrades()[l].GetTrigger(), out string text);
                    val += text 
                        + ": "
                        + arg.GetCardTriggerUpgrades()[l].GetDescriptionKey().Localize(null);
                }
                if (arg.GetBonusDamage() > 0)
                {
                    if (val.Length != 0) val += " ";
                    val += "Bonus Damage: " + arg.GetBonusDamage();
                }
                if (arg.GetBonusHeal() > 0)
                {
                    if (val.Length != 0) val += "\n";
                    val += "Bonus Heal: " + arg.GetBonusHeal();
                }
                if (arg.GetBonusHP() > 0)
                {
                    if (val.Length != 0) val += "\n";
                    val += "Bonus HP: " + arg.GetBonusHP();
                }
                if (arg.GetBonusSize() > 0)
                {
                    if (val.Length != 0) val += "\n";
                    val += "Bonus Size: " + arg.GetBonusSize();
                }
                if (arg.GetCostReduction() < 99 && arg.GetCostReduction() != 0)
                {
                    if (val.Length != 0) val += "\n";
                    val += "Reduce Cost: " + arg.GetCostReduction();
                }
                if (arg.GetXCostReduction() > 0)
                {
                    if (val.Length != 0) val += "\n";
                    val += "Reduce X Cost: " + arg.GetXCostReduction();
                }
            }
            if (val.Length == 0)
            {
                val = arg.GetUpgradeDescriptionKey().Localize(null);
            }
            if(val.Length == 0)
            {
                val = arg.name;
            }
            val = RemoveTags(val);
            return val;
        }
        /// <summary>
        /// Removes the Tags from strings, replaces them with their name= property
        /// If anyone knows how to write this in Regex, let me know
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string RemoveTags(string input)
        {
            string text = input;
            while (text.Contains("<"))
            {
                string InsertString = "";
                int i = text.IndexOf('<');
                string substring = text.Substring(i);
                int k = substring.IndexOf('>');
                if (k == -1)
                {
                    break;
                }
                string bracket = substring.Substring(0, k + 1);
                if (bracket.Contains("name="))
                {
                    int l = bracket.IndexOf("name=\"");
                    int m = bracket.Substring(l + 6).IndexOf('\"');
                    InsertString = bracket.Substring(l + 6, m);
                }
                text = text.Remove(i, k + 1);
                text = text.Insert(i, InsertString);
            }
            return text;
        }
        public ColorBlock getGreyColorBlock()
        {
            return new ColorBlock
            {
                colorMultiplier = 1,
                normalColor = grey,
                disabledColor = grey,
                highlightedColor = grey,
                pressedColor = grey
            };
        }
        public ColorBlock getRedColorBlock()
        {
            return new ColorBlock
            {
                colorMultiplier = 1,
                normalColor = Color.red,
                disabledColor = Color.red,
                highlightedColor = Color.red,
                pressedColor = Color.red
            };
        }
    

    }
}
