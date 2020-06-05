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
        private const string name_removeButton = "Button";
        private const string name_mainBackground = "MainBackground";
        private const string name_secondaryBackground = "ScrollListBackGround";
        private const string name_viewport = "ScrollListViewport";
        private const string name_content = "Content";
        #endregion

        #region Provider Fields
        public SaveManager currentSave;
        private GameStateManager Game;
        #endregion

        #region Local Fields
        //Unused in this Version
        private List<Hook> myHooks;

        private GameObject Canvas;
        private GameObject SelectionButtonPrefab;
        private GameObject RemoveButton;
        private GameObject ButtonContent;

        public CardState selectedCardState;
        public SelectionButton selectedCardGameobject;
        
        private List<SelectionButton> SelectionButtonsPool = new List<SelectionButton>();
        #endregion

        #region Unity Methods
        void Awake()
        {
            //Subscribe as Client
            DepInjector.AddClient(this);

            //Instantiate then Hide Canvas
            Canvas = GameObject.Instantiate(AssetBundleUtils.LoadAssetFromPath<GameObject>(bundleName, assetName_Canvas));
            Console.WriteLine(Canvas.transform.parent);
            DontDestroyOnLoad(Canvas);
            Canvas.SetActive(false);

            //Load Prefab to Instantiate Later
            SelectionButtonPrefab = AssetBundleUtils.LoadAssetFromPath<GameObject>(bundleName, assetName_SelectionButton);

            //Find local Buttons and add Listeners
            ButtonContent = Canvas.transform.Find($"{name_mainBackground}/{name_secondaryBackground}/{name_viewport}/{name_content}").gameObject;
            RemoveButton = Canvas.transform.Find($"{name_mainBackground}/{name_removeBackground}/{name_removeButton}").gameObject;
            RemoveButton.GetComponent<Button>().onClick.AddListener(AttemptToRemoveSelectedCard);
        }

        
        void Update()
        {
            if (Canvas == null) Console.WriteLine("Oh no");
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
                    GameObject sbp = GameObject.Instantiate(SelectionButtonPrefab);
                    DontDestroyOnLoad(sbp);
                    sbp.transform.SetParent(ButtonContent.transform);
                    SelectionButton sb = sbp.AddComponent<SelectionButton>();
                    sb.plugin = this;
                    SelectionButtonsPool.Add(sb);
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
            SelectionButton sbi;
            for (int j = 0; j < query.Count; j++)
            {
                Console.WriteLine(j);
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
        #endregion
    }
}
