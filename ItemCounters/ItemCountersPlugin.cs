using BepInEx;
using RoR2;
using System.Reflection;
using System.Text;
using UnityEngine.UI;

namespace ItemCounters {

    [BepInPlugin(ModGuid, "Item Counters", "1.0.0")]
    public class ItemCountersPlugin : BaseUnityPlugin {

        private const string ModGuid = "com.github.mcmrarm.itemcounters";

        private static FieldInfo scoreboardStripMasterField = typeof(RoR2.UI.ScoreboardStrip).GetField("master", BindingFlags.NonPublic | BindingFlags.Instance);
        
        public void Start() {
            On.RoR2.UI.ScoreboardStrip.SetMaster += (orig, self, master) => {
                orig(self, master);
                self.moneyText.GetComponent<LayoutElement>().preferredWidth = 200;
            };
            On.RoR2.UI.ScoreboardStrip.UpdateMoneyText += (orig, self) => {
                var master = (CharacterMaster)scoreboardStripMasterField.GetValue(self);
                if (!master || !master.inventory)
                    return;
                int tier1Count = master.inventory.GetTotalItemCountOfTier(ItemTier.Tier1);
                int tier2Count = master.inventory.GetTotalItemCountOfTier(ItemTier.Tier2);
                int tier3Count = master.inventory.GetTotalItemCountOfTier(ItemTier.Tier3);
                int lunarCount = master.inventory.GetTotalItemCountOfTier(ItemTier.Lunar);
                int bossCount = master.inventory.GetTotalItemCountOfTier(ItemTier.Boss);
                int totalItemCount = tier1Count + tier2Count + tier3Count + lunarCount + bossCount;

                StringBuilder sb = new StringBuilder();
                sb.AppendFormat("<nobr><color=#fff>{0} (", totalItemCount);
                if (tier1Count > 0)
                    sb.AppendFormat("<color=#{1}>{0}</color> ", tier1Count, ColorCatalog.GetColorHexString(ColorCatalog.ColorIndex.Tier1Item));
                if (tier2Count > 0)
                    sb.AppendFormat("<color=#{1}>{0}</color> ", tier2Count, ColorCatalog.GetColorHexString(ColorCatalog.ColorIndex.Tier2Item));
                if (tier3Count > 0)
                    sb.AppendFormat("<color=#{1}>{0}</color> ", tier3Count, ColorCatalog.GetColorHexString(ColorCatalog.ColorIndex.Tier3Item));
                if (lunarCount > 0)
                    sb.AppendFormat("<color=#{1}>{0}</color> ", lunarCount, ColorCatalog.GetColorHexString(ColorCatalog.ColorIndex.LunarItem));
                if (bossCount > 0)
                    sb.AppendFormat("<color=#{1}>{0}</color> ", bossCount, ColorCatalog.GetColorHexString(ColorCatalog.ColorIndex.BossItem));
                if (sb[sb.Length - 1] == ' ')
                    sb[sb.Length - 1] = ')';
                else if (sb[sb.Length - 1] == '(')
                    sb.Length = sb.Length - 2;
                sb.Append("</color></nobr>\n$");
                sb.Append(master.money);
                self.moneyText.text = sb.ToString();
            };
        }
    }
}
