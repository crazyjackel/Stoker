using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Stoker.Scripts
{
    public class SelectionButton : MonoBehaviour
    {
        public StokerPlugin plugin;
        public CardState card;
        public Text textComponent;
        public Image image;
        public Button button;

        //Setup Refernces
        void Awake()
        {
            //textComponent doesnt seem to be initialized properly here
            textComponent = GetComponentInChildren<Text>();
            button = GetComponent<Button>();
            image = GetComponent<Image>();
            button.onClick.AddListener(delegate { SelectCard(); });
        }

        public void SelectCard()
        {
            plugin.selectedCardState = card;
            if (plugin.selectedCardGameobject != null)
            {
                plugin.selectedCardGameobject.button.colors = button.colors;
            }
            button.colors = new ColorBlock
            {
                colorMultiplier = button.colors.colorMultiplier,
                highlightedColor = button.colors.highlightedColor,
                normalColor = button.colors.pressedColor,
                pressedColor = button.colors.pressedColor
            };
        }

        public void UpdateText(CardState newCard)
        {
            card = newCard;
            string modifiers = "";
            CardStateModifiers st = card.GetCardStateModifiers();
            if (st != null)
            {
                List<CardUpgradeState> upgrades = st.GetCardUpgrades();
                if(upgrades.Count > 0)
                {
                    modifiers += ":";
                }
                for (int index = 0; index < upgrades.Count; index++)
                {
                    modifiers += upgrades[index].GetAssetName() + ((index != upgrades.Count - 1) ? "|" : "");
                }
            }
            string text = $"{card.GetAssetName()}{modifiers}";
            Console.WriteLine(text);
            textComponent = GetComponentInChildren<Text>();
            textComponent.text = text;
        }
    }
}
