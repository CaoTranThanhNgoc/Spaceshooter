using UnityEngine;

namespace SpaceHawk.Data
{
    /// <summary>One selectable ship hull's visuals - Ship_LVL_1..5 (which one shows depends on the
    /// Inventory upgrade level, independent of hull choice) plus its matching exhaust flame frames.
    /// Baked once per hull (Ship_01/02/03) at Editor build time; PlayerShip and InventoryPanel both
    /// pick the entry matching SaveManager.GetSelectedShip().</summary>
    [System.Serializable]
    public class ShipHullSprites
    {
        public Sprite[] levelSprites;
        public Sprite[] exhaustFrames;
    }
}
