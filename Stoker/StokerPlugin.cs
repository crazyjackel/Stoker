using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using MonsterTrainModdingAPI.Utilities;
using System.IO;
using Stoker.Scripts;
using MonsterTrainModdingAPI.Managers;
using MonsterTrainModdingAPI.Interfaces;

namespace Stoker
{
    [BepInPlugin("io.github.crazyjackel.Stoker", "Stoker Deck Editor Application", "1.1.0")]
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
        private const string name_removeButton = "Button";
        private const string name_searchBar = "InputField";
        private const string name_addButton = "Button";
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

        private List<DerivedCardStateSelectionButton> SelectionButtonsPool = new List<DerivedCardStateSelectionButton>();
        private List<DerivedRelicStateSelectionButton> RelicStateSelectionButtonsPool = new List<DerivedRelicStateSelectionButton>();
        private List<DerivedRelicDataSelectionButton> RelicDataSelectionButtonsPool = new List<DerivedRelicDataSelectionButton>();
        private List<DerivedCardDataSelectionButton> AllGameDataSelectionButtonsPool = new List<DerivedCardDataSelectionButton>();

        private string search;
        private BundleAssetLoadingInfo info;
        private bool isInit = false;
        #endregion

        #region Unity Methods
        public void Initialize()
        {
            //Load Assets
            var assembly = Assembly.GetExecutingAssembly();
            PluginManager.AssemblyNameToPath.TryGetValue(assembly.FullName, out string basePath);
            info = new BundleAssetLoadingInfo
            {
                PluginPath = basePath,
                FilePath = bundleName,
            };
            BundleManager.RegisterBundle(GUIDGenerator.GenerateDeterministicGUID(info.FullPath), info);


            //Instantiate then Hide Canvas
            var GameObj = BundleManager.LoadAssetFromBundle(info, assetName_Canvas) as GameObject;
            Canvas = GameObject.Instantiate(GameObj);
            DontDestroyOnLoad(Canvas);
            Canvas.SetActive(false);

            //Load Prefab to Instantiate Later
            SelectionButtonPrefab = BundleManager.LoadAssetFromBundle(info, assetName_SelectionButton) as GameObject;

            //Find local Buttons and add Listeners
            //Load Content where to add Selection Buttons
            DeckContent = Canvas.transform.Find($"{name_mainBackground}/{name_secondaryBackground}/{name_viewport}/{name_content}").gameObject;
            CardDatabaseContent = Canvas.transform.Find($"{name_mainBackground}/{name_secondaryBackground2}/{name_viewport}/{name_content}").gameObject;
            RelicDatabaseContent = Canvas.transform.Find($"{name_mainBackground}/{name_secondaryBackground3}/{name_viewport}/{name_content}").gameObject;
            RelicContent = Canvas.transform.Find($"{name_mainBackground}/{name_secondaryBackground4}/{name_viewport}/{name_content}").gameObject;
            UpgradeContent = Canvas.transform.Find($"{name_mainBackground}/{name_secondaryBackground5}/{name_viewport}/{name_content}").gameObject;
            UpgradeDatabaseContent = Canvas.transform.Find($"{name_mainBackground}/{name_secondaryBackground6}/{name_viewport}/{name_content}").gameObject;

            RemoveButton = Canvas.transform.Find($"{name_mainBackground}/{name_removeBackground}/{name_removeButton}").gameObject;
            RemoveButton.GetComponent<Button>().onClick.AddListener(AttemptToRemoveSelectedCard);
            AddButton = Canvas.transform.Find($"{name_mainBackground}/{name_addBackground}/{name_addButton}").gameObject;
            AddButton.GetComponent<Button>().onClick.AddListener(AttemptToAddSelectedCardData);
            SearchBar = Canvas.transform.Find($"{name_mainBackground}/{name_searchBarBackground}/{name_searchBar}").gameObject;
            SearchBar.GetComponent<InputField>().onValueChanged.AddListener(UpdateDatabases);

            //Subscribe as Client
            DepInjector.AddClient(this);
            this.Logger.LogInfo("Stoker Plugin Initialized");
            //Add as Listener to Other Signals
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

        #region RelicNotifications Methods
        public void RelicAddedNotification(List<RelicState> relics, RelicState newRelic, Team.Type team)
        {
            List<RelicState> query = relics.OrderBy(relic => relic.GetName()).ToList();

            //If deck is bigger, increase pool size
            if (query.Count > RelicStateSelectionButtonsPool.Count)
            {
                for (int i = RelicStateSelectionButtonsPool.Count; i < query.Count; i++)
                {
                    DerivedRelicStateSelectionButton selectionButton = CreateRelicStateSelectionButton(RelicContent.transform);
                    selectionButton.OnClick += OnClickRelicState;
                    selectionButton.UpdateTextFunc += getRelicStateName;
                    RelicStateSelectionButtonsPool.Add(selectionButton);
                }
            }
            else
            {
                //Hide excessive Selection Buttons
                for (int i = query.Count; i < RelicStateSelectionButtonsPool.Count; i++)
                {
                    SelectionButtonsPool[i].gameObject.SetActive(false);
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
            }

            //Get Reference to GameStateManager When Initialized
            if (DepInjector.MapProvider<GameStateManager>(newProvider, ref this.Game))
            {
                //Subscribe listener to Signal
                Game.runStartedSignal.AddListener(GameStartedListener);
            }
            if(DepInjector.MapProvider<RelicManager>(newProvider, ref this.relicManager))
            {
                relicManager.AddRelicNotifications(this);
                this.Logger.LogInfo("Relic Manager Initialized");
                InitializeRelicDataBase();
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
        public DerivedCardDataSelectionButton CreateCardDataSelectionButton(Transform parent)
        {
            GameObject sbp = GameObject.Instantiate(SelectionButtonPrefab);
            DontDestroyOnLoad(sbp);
            sbp.transform.SetParent(parent);
            DerivedCardDataSelectionButton sb = sbp.AddComponent<DerivedCardDataSelectionButton>();
            sb.plugin = this;
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
            return sb;
        }
        private DerivedRelicStateSelectionButton CreateRelicStateSelectionButton(Transform parent)
        {
            GameObject sbp = GameObject.Instantiate(SelectionButtonPrefab);
            DontDestroyOnLoad(sbp);
            sbp.transform.SetParent(parent);
            DerivedRelicStateSelectionButton sb = sbp.AddComponent<DerivedRelicStateSelectionButton>();
            sb.plugin = this;
            return sb;
        }
        #endregion

        #region Database Initializers
        public void InitializeCardDataBase()
        {
            if (currentSave != null && data != null)
            {
                List<CardData> dataList = data.GetAllCardData().OrderBy(x => x.GetName()).ToList();
                for (int i = 0; i < dataList.Count; i++)
                {

                    DerivedCardDataSelectionButton selectionButton = CreateCardDataSelectionButton(CardDatabaseContent.transform);
                    selectionButton.OnClick += OnClickCardData;
                    selectionButton.UpdateTextFunc += GetCardDataName;
                    selectionButton.UpdateText(dataList[i]);
                    AllGameDataSelectionButtonsPool.Add(selectionButton);
                }
            }
        }
        private void InitializeRelicDataBase()
        {
            if(data != null && currentSave != null)
            {
                List<CollectableRelicData> dataList = data.GetAllCollectableRelicData().OrderBy(x => x.GetName()).ToList();
                for (int i = 0; i < dataList.Count; i++)
                {
                    DerivedRelicDataSelectionButton selectionButton = Createrel
                    
                }
            }
        }
        #endregion
        /// <summary>
        /// Updates the Databases based on search parameter
        /// </summary>
        /// <param name="newSearch"></param>
        public void UpdateDatabases(string newSearch)
        {
            search = newSearch;
            if (currentSave != null && data != null)
            {
                List<CardData> dataList = data.GetAllCardData().OrderBy(x => x.GetName()).ToList();
                if (dataList.Count > AllGameDataSelectionButtonsPool.Count)
                {
                    for (int i = AllGameDataSelectionButtonsPool.Count; i < dataList.Count; i++)
                    {
                        DerivedCardDataSelectionButton selectionButton = CreateCardDataSelectionButton(CardDatabaseContent.transform);
                        selectionButton.OnClick += OnClickCardData;
                        selectionButton.UpdateTextFunc += GetCardDataName;
                        selectionButton.UpdateText(dataList[i]);
                        AllGameDataSelectionButtonsPool.Add(selectionButton);
                    }
                }


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

        public void AttemptToAddSelectedRelicData()
        {
            if(currentSave != null && selectedRelicData != null)
            {
                currentSave.AddRelic(selectedRelicData);
            }
        }
        #endregion

        #region StokerPlugin Delegation Methods
        public void OnClickCardState(StokerPlugin plugin, SelectionButton<CardState> obj, CardState item)
        {
            //Sets the Selected Card State to this
            plugin.selectedCardState = item;
            //Update old Card State's GameObj's color
            if (plugin.selectedCardStateGameobject != null)
            {
                Color color = new Color(74f/255, 78f/255, 84f/255);
                plugin.selectedCardStateGameobject.button.colors = new ColorBlock
                {
                    colorMultiplier = obj.button.colors.colorMultiplier,
                    normalColor = color,
                    disabledColor = color,
                    highlightedColor = color,
                    pressedColor = color
                };
            }
            //Set Selected Card State to this
            plugin.selectedCardStateGameobject = obj;
            //Update Colors
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
            if (plugin.selectedCardDataGameobject != null)
            {
                Color color = new Color(74f/255,78f/255,84f/255);
                plugin.selectedCardDataGameobject.button.colors = new ColorBlock
                {
                    colorMultiplier = obj.button.colors.colorMultiplier,
                    normalColor = color,
                    disabledColor = color,
                    highlightedColor = color,
                    pressedColor = color
                };
            }
            plugin.selectedCardDataGameobject = obj;
            obj.button.colors = new ColorBlock
            {
                colorMultiplier = obj.button.colors.colorMultiplier,
                normalColor = Color.red,
                disabledColor = Color.red,
                highlightedColor = Color.red,
                pressedColor = Color.red
            };
        }
        public void OnClickRelicData(StokerPlugin plugin, SelectionButton<RelicData> obj, RelicData item)
        {
            plugin.selectedRelicData = item;
            if (plugin.selectedCardDataGameobject != null)
            {
                Color color = new Color(74f / 255, 78f / 255, 84f / 255);
                plugin.selectedRelicDataGameobject.button.colors = new ColorBlock
                {
                    colorMultiplier = obj.button.colors.colorMultiplier,
                    normalColor = color,
                    disabledColor = color,
                    highlightedColor = color,
                    pressedColor = color
                };
            }
            plugin.selectedRelicDataGameobject = obj;
            obj.button.colors = new ColorBlock
            {
                colorMultiplier = obj.button.colors.colorMultiplier,
                normalColor = Color.red,
                disabledColor = Color.red,
                highlightedColor = Color.red,
                pressedColor = Color.red
            };
        }
        public void OnClickRelicState(StokerPlugin plugin, SelectionButton<RelicState> obj, RelicState item)
        {
            plugin.selectedRelicState = item;
            if (plugin.selectedRelicStateGameobject != null)
            {
                Color color = new Color(74f / 255, 78f / 255, 84f / 255);
                plugin.selectedRelicStateGameobject.button.colors = new ColorBlock
                {
                    colorMultiplier = obj.button.colors.colorMultiplier,
                    normalColor = color,
                    disabledColor = color,
                    highlightedColor = color,
                    pressedColor = color
                };
            }
            plugin.selectedRelicStateGameobject = obj;
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
        private string getRelicStateName(RelicState arg)
        {
            return arg.GetName();
        }
        #endregion
    }
}
