using BepInEx;
using BepInEx.Logging;
using LeTai.Asset.TranslucentImage;
using MiniRpcLib;
using MiniRpcLib.Action;
using RoR2;
using RoR2.UI;
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace ChestItems {

    [BepInPlugin(ModGuid, "Chest Item Picker", "1.0.0")]
    public class MyPlugin : BaseUnityPlugin {

        private const string ModGuid = "com.github.mcmrarm.chestitempicker";

        private static FieldInfo chestBehaviorDropPickupMember = typeof(ChestBehavior).GetField("dropPickup", BindingFlags.NonPublic | BindingFlags.Instance);

        private IRpcAction<Action<NetworkWriter>> NetShowItemPickerAction;
        private IRpcAction<Action<NetworkWriter>> NetItemPickedAction;

        public void Start() {
            var miniRpc = MiniRpc.CreateInstance(ModGuid);
            NetShowItemPickerAction = miniRpc.RegisterAction(Target.Client, NetShowItemPicker);
            NetItemPickedAction = miniRpc.RegisterAction(Target.Server, NetItemPicked);

            On.RoR2.ChestBehavior.Start += (orig, self) => {
                orig(self);
                // By default the listener list contains: [0]PurchaseInteraction.SetAvailable(false) and [1]ChestBehavior.Open()
                self.GetComponent<PurchaseInteraction>().onPurchase.SetPersistentListenerState(1, UnityEngine.Events.UnityEventCallState.Off);
                self.GetComponent<PurchaseInteraction>().onPurchase.AddListener((v) => {
                    var user = v.GetComponent<CharacterBody>()?.master?.GetComponent<PlayerCharacterMasterController>()?.networkUser;
                    if (user == null)
                        return;
                    List<PickupIndex> pickups = GetAvailablePickups(self);
                    CallNetShowItemPicker(user, self.netId, pickups);
                });
            };
        }

        private List<PickupIndex> GetAvailablePickups(ChestBehavior chest) {
            var availablePickups = new List<PickupIndex>();
            var generatedPickup = (PickupIndex)chestBehaviorDropPickupMember.GetValue(chest);
            if (generatedPickup.itemIndex != ItemIndex.None) {
                var tier = ItemCatalog.GetItemDef(generatedPickup.itemIndex).tier;
                if (tier == ItemTier.Tier1 || tier == ItemTier.Tier2 || tier == ItemTier.Tier3)
                    availablePickups.AddRange(Run.instance.availableTier1DropList);
                if (tier == ItemTier.Tier2 || tier == ItemTier.Tier3)
                    availablePickups.AddRange(Run.instance.availableTier2DropList);
                if (tier == ItemTier.Tier3)
                    availablePickups.AddRange(Run.instance.availableTier3DropList);
                if (tier == ItemTier.Lunar)
                    availablePickups.AddRange(Run.instance.availableLunarDropList);
                //if (tier != ItemTier.Tier1 && tier != ItemTier.Tier2 && tier != ItemTier.Tier3 && tier != ItemTier.Lunar)
                //    return;
            } else if (generatedPickup.equipmentIndex != EquipmentIndex.None) {
                if (EquipmentCatalog.GetEquipmentDef(generatedPickup.equipmentIndex).isLunar) {
                    availablePickups.AddRange(Run.instance.availableLunarDropList);
                } else {
                    availablePickups.AddRange(Run.instance.availableEquipmentDropList);
                }
            }
            return availablePickups;
        }

        private void ShowItemPicker(List<PickupIndex> availablePickups, ItemCallback cb) {
            var itemInventoryDisplay = GameObject.Find("ItemInventoryDisplay");

            float uiWidth = 400f;
            if (availablePickups.Count > 8 * 5) // at least 5 rows of 8 items
                uiWidth = 500f;
            if (availablePickups.Count > 10 * 5) // at least 5 rows of 10 items
                uiWidth = 600f;

            Logger.Log(LogLevel.Info, "Run started");
            var g = new GameObject();
            g.name = "ChestItemsUI";
            g.layer = 5; // UI
            g.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            g.GetComponent<Canvas>().sortingOrder = -1; // Required or the UI will render over pause and tooltips.
            // g.AddComponent<CanvasScaler>().scaleFactor = 10.0f;
            // g.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
            g.AddComponent<GraphicRaycaster>();
            g.AddComponent<MPEventSystemProvider>().fallBackToMainEventSystem = true;
            g.AddComponent<MPEventSystemLocator>();
            g.AddComponent<CursorOpener>();

            var ctr = new GameObject();
            ctr.name = "Container";
            ctr.transform.SetParent(g.transform, false);
            ctr.AddComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, uiWidth);

            var bg2 = new GameObject();
            bg2.name = "Background";
            bg2.transform.SetParent(ctr.transform, false);
            bg2.AddComponent<TranslucentImage>().color = new Color(0f, 0f, 0f, 1f);
            bg2.GetComponent<TranslucentImage>().raycastTarget = true;
            bg2.GetComponent<TranslucentImage>().material = Resources.Load<GameObject>("Prefabs/UI/Tooltip").GetComponentInChildren<TranslucentImage>(true).material;
            bg2.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 0f);
            bg2.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
            bg2.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 0);

            var bg = new GameObject();
            bg.name = "Background";
            bg.transform.SetParent(ctr.transform, false);
            bg.AddComponent<Image>().sprite = itemInventoryDisplay.GetComponent<Image>().sprite;
            bg.GetComponent<Image>().type = Image.Type.Sliced;
            bg.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 0f);
            bg.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
            bg.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 0);

            var header = new GameObject();
            header.name = "Header";
            header.transform.SetParent(ctr.transform, false);
            header.transform.localPosition = new Vector2(0, 0);
            header.AddComponent<HGTextMeshProUGUI>().fontSize = 30;
            header.GetComponent<HGTextMeshProUGUI>().text = "Select the item";
            header.GetComponent<HGTextMeshProUGUI>().color = Color.white;
            header.GetComponent<HGTextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
            header.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 1f);
            header.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
            header.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1f);
            header.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 90);

            var itemCtr = new GameObject();
            itemCtr.name = "Item Container";
            itemCtr.transform.SetParent(ctr.transform, false);
            itemCtr.transform.localPosition = new Vector2(0, -100f);
            itemCtr.AddComponent<GridLayoutGroup>().childAlignment = TextAnchor.UpperCenter;
            itemCtr.GetComponent<GridLayoutGroup>().cellSize = new Vector2(50f, 50f);
            itemCtr.GetComponent<GridLayoutGroup>().spacing = new Vector2(8f, 8f);
            itemCtr.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 1f);
            itemCtr.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
            itemCtr.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1f);
            itemCtr.GetComponent<RectTransform>().sizeDelta = new Vector2(-16f, 0);
            itemCtr.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            var itemIconPrefab = itemInventoryDisplay.GetComponent<ItemInventoryDisplay>().itemIconPrefab;
            foreach (PickupIndex index in availablePickups) {
                if (index.itemIndex == ItemIndex.None)
                    continue;
                var item = Instantiate<GameObject>(itemIconPrefab, itemCtr.transform).GetComponent<ItemIcon>();
                item.SetItemIndex(index.itemIndex, 1);
                item.gameObject.AddComponent<Button>().onClick.AddListener(() => {
                    Logger.LogInfo("Item picked: " + index);
                    UnityEngine.Object.Destroy(g);
                    cb(index);
                });
            }
            foreach (PickupIndex index in availablePickups) {
                if (index.equipmentIndex == EquipmentIndex.None)
                    continue;
                var def = EquipmentCatalog.GetEquipmentDef(index.equipmentIndex);
                var item = Instantiate<GameObject>(itemIconPrefab, itemCtr.transform).GetComponent<ItemIcon>();
                item.GetComponent<RawImage>().texture = def.pickupIconTexture;
                item.stackText.enabled = false;
                item.tooltipProvider.titleToken = def.nameToken;
                item.tooltipProvider.titleColor = ColorCatalog.GetColor(def.colorIndex);
                item.tooltipProvider.bodyToken = def.pickupToken;
                item.tooltipProvider.bodyColor = Color.gray;
                item.gameObject.AddComponent<Button>().onClick.AddListener(() => {
                    Logger.LogInfo("Equipment picked: " + index);
                    UnityEngine.Object.Destroy(g);
                    cb(index);
                });
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(itemCtr.GetComponent<RectTransform>());
            ctr.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, itemCtr.GetComponent<RectTransform>().sizeDelta.y + 100f + 20f);
        }

        public delegate void ItemCallback(PickupIndex index);
        

        // I used the annotations to just make the code more readable, they are unused if compiling via VS (and if compiling via unity they add additional asserts)

        [Client]
        private void NetShowItemPicker(NetworkUser user, NetworkReader reader) {
            var chestId = reader.ReadNetworkId();
            int count = reader.ReadInt32();
            var pickups = new List<PickupIndex>(count);
            for (int i = 0; i < count; i++)
                pickups.Add(PickupIndex.ReadFromNetworkReader(reader));

            ShowItemPicker(pickups, x => CallNetItemPicked(chestId, x));
        }

        [Server]
        private void CallNetShowItemPicker(NetworkUser user, NetworkInstanceId chestId, List<PickupIndex> pickups) {
            NetShowItemPickerAction.Invoke(w => {
                w.Write(chestId);
                w.Write(pickups.Count);
                foreach (var i in pickups)
                    PickupIndex.WriteToNetworkWriter(w, i);
            }, user);
        }

        [Server]
        private void NetItemPicked(NetworkUser user, NetworkReader reader) {
            var chestNetId = reader.ReadNetworkIdentity();
            var selectedPickup = PickupIndex.ReadFromNetworkReader(reader);

            var chest = chestNetId.GetComponent<ChestBehavior>();
            chestBehaviorDropPickupMember.SetValue(chest, selectedPickup);
            chest.Open();
        }

        [Client]
        private void CallNetItemPicked(NetworkInstanceId chestId, PickupIndex selectedPickup) {
            NetItemPickedAction.Invoke(w => {
                w.Write(chestId);
                PickupIndex.WriteToNetworkWriter(w, selectedPickup);
            });
        }

    }
}
