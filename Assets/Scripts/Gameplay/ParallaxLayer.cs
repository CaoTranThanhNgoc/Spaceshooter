using UnityEngine;

namespace SpaceHawk.Gameplay
{
    /// <summary>
    /// Scrolls a single background layer downward (simulating the ship flying forward) and
    /// wraps it seamlessly using a second auto-created tile stacked above the first.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class ParallaxLayer : MonoBehaviour
    {
        public Sprite sprite;
        public float scrollSpeed = 1f;
        public int sortingOrder = 0;

        private Transform _tileA;
        private Transform _tileB;
        private float _tileHeight;

        private void Start()
        {
            SpriteRenderer rendererA = GetComponent<SpriteRenderer>();
            rendererA.sprite = sprite;
            rendererA.sortingOrder = sortingOrder;
            _tileHeight = rendererA.bounds.size.y;
            _tileA = transform;

            GameObject second = new GameObject(name + "_Tile2");
            second.transform.SetParent(transform.parent, false);
            second.transform.SetPositionAndRotation(transform.position + new Vector3(0f, _tileHeight, 0f), transform.rotation);
            second.transform.localScale = transform.localScale;
            SpriteRenderer rendererB = second.AddComponent<SpriteRenderer>();
            rendererB.sprite = sprite;
            rendererB.sortingOrder = sortingOrder;
            _tileB = second.transform;
        }

        private void Update()
        {
            if (_tileA == null) return;

            Vector3 delta = Vector3.down * scrollSpeed * Time.deltaTime;
            _tileA.position += delta;
            _tileB.position += delta;

            if (_tileA.position.y < -_tileHeight) _tileA.position += Vector3.up * (_tileHeight * 2f);
            if (_tileB.position.y < -_tileHeight) _tileB.position += Vector3.up * (_tileHeight * 2f);
        }
    }
}
