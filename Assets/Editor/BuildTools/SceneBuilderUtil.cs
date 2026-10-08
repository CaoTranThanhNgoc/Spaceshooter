using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace SpaceHawk.EditorTools
{
    public static class SceneBuilderUtil
    {
        public static EventSystem CreateEventSystem()
        {
            EventSystem existing = Object.FindFirstObjectByType<EventSystem>();
            if (existing != null) return existing;

            GameObject go = new GameObject("EventSystem");
            EventSystem es = go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
            return es;
        }

        public static Canvas CreateCanvas(string name, int sortOrder = 0)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            // Landscape phones vary a lot in width (16:9 up to ~20:9) but height is the stable
            // axis, so match height fully: every screen sees exactly 1080 vertical units, only
            // the visible width changes. Panels below size themselves as a % of width so they
            // never overflow on narrower aspect ratios.
            scaler.matchWidthOrHeight = 1f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static UnityEngine.SceneManagement.Scene NewEmptyScene(string path)
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            EditorSceneManager.SaveScene(scene, path);
            return scene;
        }

        public static void SaveScene(UnityEngine.SceneManagement.Scene scene)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        public static GameObject SaveAsPrefab(GameObject sceneObject, string prefabPath, bool destroySceneObject = true)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(prefabPath));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(sceneObject, prefabPath);
            if (destroySceneObject) Object.DestroyImmediate(sceneObject);
            return prefab;
        }

        /// <summary>Adds a tag to ProjectSettings/TagManager.asset if it isn't already defined
        /// (fresh projects only ship the handful of Unity built-in tags).</summary>
        public static void EnsureTag(string tag)
        {
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty tagsProp = tagManager.FindProperty("tags");

            for (int i = 0; i < tagsProp.arraySize; i++)
                if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag) return;

            tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
            tagManager.ApplyModifiedProperties();
        }
    }
}
