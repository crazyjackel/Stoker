using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Stoker.Scripts
{
    public class SelectionButton<T> : MonoBehaviour
    {
        public StokerPlugin plugin;
        public T item;
        public Button button;

        public Action<StokerPlugin, SelectionButton<T>, T> OnClick;
        public Func<T, string> UpdateTextFunc;

        //Setup Refernces
        void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(ClickCard);
        }

        public void ClickCard()
        {
            OnClick.Invoke(plugin, this, item);
        }

        public void UpdateText(T newItem)
        {
            item = newItem;
            string text = UpdateTextFunc.Invoke(item);
            GetComponentInChildren<Text>().text = text;
        }
    }
}
