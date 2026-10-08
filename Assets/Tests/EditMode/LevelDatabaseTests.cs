using NUnit.Framework;
using UnityEngine;
using SpaceHawk.Data;

namespace SpaceHawk.Tests
{
    public class LevelDatabaseTests
    {
        [Test]
        public void GetByIndex_ReturnsCorrectLevel()
        {
            LevelData a = ScriptableObject.CreateInstance<LevelData>();
            LevelData b = ScriptableObject.CreateInstance<LevelData>();
            LevelDatabase db = ScriptableObject.CreateInstance<LevelDatabase>();
            db.levels = new[] { a, b };

            Assert.AreEqual(a, db.GetByIndex(0));
            Assert.AreEqual(b, db.GetByIndex(1));
            Assert.IsNull(db.GetByIndex(5));
            Assert.IsNull(db.GetByIndex(-1));

            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
            Object.DestroyImmediate(db);
        }

        [Test]
        public void IndexOf_FindsMatchingLevel()
        {
            LevelData a = ScriptableObject.CreateInstance<LevelData>();
            LevelData b = ScriptableObject.CreateInstance<LevelData>();
            LevelDatabase db = ScriptableObject.CreateInstance<LevelDatabase>();
            db.levels = new[] { a, b };

            Assert.AreEqual(1, db.IndexOf(b));
            Assert.AreEqual(-1, db.IndexOf(null));

            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
            Object.DestroyImmediate(db);
        }
    }
}
