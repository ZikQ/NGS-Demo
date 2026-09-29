using NGS.Unity.Core.InventorySystem;
using NGS.Unity.Core.SpellSystem.Spells;

namespace Assets.Scripts.Game.MOBA.UI
{
    public class ShopItemIcon : ItemIcon
    {
        public void Bind(Item item, SpellInstance useSpell) => Show(item, useSpell);
    }
}
