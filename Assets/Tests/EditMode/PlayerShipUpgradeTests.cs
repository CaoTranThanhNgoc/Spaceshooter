using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using SpaceHawk.Core;
using SpaceHawk.Data;
using SpaceHawk.Gameplay;

namespace SpaceHawk.Tests
{
    /// <summary>Exercises the real Player.prefab (not a mock) to catch wiring bugs that only show
    /// up once Unity actually instantiates it - e.g. a stale reference a plain code review of the
    /// .prefab YAML could miss.</summary>
    public class PlayerShipUpgradeTests
    {
        private GameObject _instance;

        [SetUp]
        public void SetUp()
        {
            SaveManager.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            if (_instance != null) Object.DestroyImmediate(_instance);
        }

        private PlayerShip Spawn()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Player.prefab");
            Assert.IsNotNull(prefab, "Player.prefab not found - run Tools/Space Hawk/0. Build Everything first.");
            _instance = Object.Instantiate(prefab);
            PlayerShip ship = _instance.GetComponent<PlayerShip>();
            Assert.IsNotNull(ship);
            Assert.IsNotNull(ship.bodySprite, "PlayerShip.bodySprite is not wired on the prefab.");
            return ship;
        }

        [Test]
        public void ApplyShip_ShowsTheSpriteOfEveryShipInTheRoster()
        {
            PlayerShip ship = Spawn();
            Assert.AreEqual(ShipCatalog.FamilyCount, ship.hulls?.Length, "One sprite set per family expected.");

            for (int index = 0; index < ShipCatalog.Count; index++)
            {
                ship.ApplyShip(index);
                Sprite expected = ship.hulls[ShipCatalog.FamilyOf(index)].levelSprites[ShipCatalog.TierOf(index)];
                Assert.AreEqual(expected, ship.bodySprite.sprite, $"Ship {index}: wrong sprite on screen.");

                BoxCollider2D box = ship.GetComponent<BoxCollider2D>();
                Assert.LessOrEqual(box.size.y, 1.6f + 0.001f, $"Ship {index}: hitbox taller than the cap.");
                Assert.Greater(box.size.x, 0.2f);

                Vector2 shown = ship.bodySprite.sprite.bounds.size * ship.transform.localScale.x;
                Assert.LessOrEqual(shown.x, 1.7f + 0.001f, $"Ship {index}: wider than allowed on screen.");
                Assert.LessOrEqual(shown.y, 1.9f + 0.001f, $"Ship {index}: taller than allowed on screen.");
                Assert.AreEqual(1f, ship.firePoint.lossyScale.x, 0.001f, $"Ship {index}: the beam's origin must keep its real size.");
            }
        }

    }
}
