using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BlueprintSystem
{
    public enum BlueprintLoopScrollViewDirection
    {
        VerticalGrid,
        HorizontalGrid
    }

    [DisallowMultipleComponent]
    public sealed class BlueprintLoopScrollView : MonoBehaviour
    {
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform content;
        [SerializeField] private GameObject itemTemplate;
        [SerializeField] private BlueprintLoopScrollViewDirection direction = BlueprintLoopScrollViewDirection.VerticalGrid;
        [SerializeField] private int constraintCount = 1;
        [SerializeField] private Vector2 cellSize = new Vector2(100f, 32f);
        [SerializeField] private Vector2 spacing = Vector2.zero;
        [SerializeField] private int poolPadding = 2;
        [SerializeField] private string bindEventName = "OnBindItem";

        private readonly List<GameObject> _pool = new List<GameObject>();
        private IList _items = new List<object>();
        private BlueprintExecutionContext _context;
        private int _firstVisibleIndex = -1;

        public int PoolCount
        {
            get { return _pool.Count; }
        }

        public int VisibleItemCount
        {
            get { return _items == null ? 0 : _items.Count; }
        }

        private void Awake()
        {
            ResolveReferences();
            if (itemTemplate != null)
            {
                itemTemplate.SetActive(false);
            }
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (scrollRect != null)
            {
                scrollRect.onValueChanged.AddListener(OnScrollValueChanged);
            }
        }

        private void OnDisable()
        {
            if (scrollRect != null)
            {
                scrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);
            }
        }

        public void Refresh(IList items, BlueprintExecutionContext context)
        {
            _items = items ?? new List<object>();
            _context = context;
            _firstVisibleIndex = -1;
            ResolveReferences();
            EnsurePool();
            UpdateContentSize();
            RenderVisibleItems(true);
        }

        public void Clear()
        {
            _items = new List<object>();
            _firstVisibleIndex = -1;
            for (int i = 0; i < _pool.Count; i++)
            {
                if (_pool[i] != null)
                {
                    _pool[i].SetActive(false);
                }
            }

            UpdateContentSize();
        }

        private void OnScrollValueChanged(Vector2 value)
        {
            RenderVisibleItems(false);
        }

        private void ResolveReferences()
        {
            if (scrollRect == null)
            {
                scrollRect = GetComponent<ScrollRect>();
            }

            if (content == null && scrollRect != null)
            {
                content = scrollRect.content;
            }
        }

        private void EnsurePool()
        {
            if (content == null || itemTemplate == null)
            {
                return;
            }

            int desired = Mathf.Min(VisibleItemCount, Mathf.Max(0, EstimateVisibleSlotCount()));
            while (_pool.Count < desired)
            {
                GameObject item = Instantiate(itemTemplate, content);
                item.name = itemTemplate.name + "_" + _pool.Count.ToString();
                item.SetActive(false);
                _pool.Add(item);
            }

            for (int i = desired; i < _pool.Count; i++)
            {
                if (_pool[i] != null)
                {
                    _pool[i].SetActive(false);
                }
            }
        }

        private int EstimateVisibleSlotCount()
        {
            RectTransform viewport = scrollRect == null ? null : scrollRect.viewport;
            Vector2 viewportSize = viewport == null ? cellSize : viewport.rect.size;
            int constraint = GetConstraintCount();
            int visiblePrimary;
            if (direction == BlueprintLoopScrollViewDirection.VerticalGrid)
            {
                visiblePrimary = Mathf.CeilToInt(viewportSize.y / GetStrideY()) + poolPadding;
            }
            else
            {
                visiblePrimary = Mathf.CeilToInt(viewportSize.x / GetStrideX()) + poolPadding;
            }

            return Mathf.Max(constraint, visiblePrimary * constraint);
        }

        private void UpdateContentSize()
        {
            if (content == null)
            {
                return;
            }

            int constraint = GetConstraintCount();
            int primaryCount = Mathf.CeilToInt(VisibleItemCount / (float)constraint);
            if (direction == BlueprintLoopScrollViewDirection.VerticalGrid)
            {
                content.sizeDelta = new Vector2(
                    constraint * cellSize.x + Mathf.Max(0, constraint - 1) * spacing.x,
                    primaryCount * cellSize.y + Mathf.Max(0, primaryCount - 1) * spacing.y);
            }
            else
            {
                content.sizeDelta = new Vector2(
                    primaryCount * cellSize.x + Mathf.Max(0, primaryCount - 1) * spacing.x,
                    constraint * cellSize.y + Mathf.Max(0, constraint - 1) * spacing.y);
            }
        }

        private void RenderVisibleItems(bool force)
        {
            if (content == null || itemTemplate == null || _items == null)
            {
                return;
            }

            EnsurePool();
            int firstIndex = GetFirstVisibleIndex();
            if (!force && firstIndex == _firstVisibleIndex)
            {
                return;
            }

            _firstVisibleIndex = firstIndex;
            for (int i = 0; i < _pool.Count; i++)
            {
                GameObject itemObject = _pool[i];
                if (itemObject == null)
                {
                    continue;
                }

                int itemIndex = firstIndex + i;
                if (itemIndex < 0 || itemIndex >= VisibleItemCount)
                {
                    itemObject.SetActive(false);
                    continue;
                }

                itemObject.SetActive(true);
                PositionItem(itemObject, itemIndex);
                BlueprintRunner runner = itemObject.GetComponent<BlueprintRunner>();
                BlueprintUIRuntimeUtility.BindRow(runner, _items[itemIndex], itemIndex, VisibleItemCount, bindEventName);
            }
        }

        private int GetFirstVisibleIndex()
        {
            int constraint = GetConstraintCount();
            float offset = 0f;
            if (content != null)
            {
                offset = direction == BlueprintLoopScrollViewDirection.VerticalGrid
                    ? Mathf.Max(0f, content.anchoredPosition.y)
                    : Mathf.Max(0f, -content.anchoredPosition.x);
            }

            float stride = direction == BlueprintLoopScrollViewDirection.VerticalGrid ? GetStrideY() : GetStrideX();
            int primary = Mathf.Max(0, Mathf.FloorToInt(offset / Mathf.Max(1f, stride)) - Mathf.Max(0, poolPadding));
            return Mathf.Min(primary * constraint, Mathf.Max(0, VisibleItemCount - 1));
        }

        private void PositionItem(GameObject itemObject, int itemIndex)
        {
            RectTransform rect = itemObject.GetComponent<RectTransform>();
            if (rect == null)
            {
                return;
            }

            int constraint = GetConstraintCount();
            int secondary = itemIndex % constraint;
            int primary = itemIndex / constraint;
            float x;
            float y;
            if (direction == BlueprintLoopScrollViewDirection.VerticalGrid)
            {
                x = secondary * GetStrideX();
                y = -primary * GetStrideY();
            }
            else
            {
                x = primary * GetStrideX();
                y = -secondary * GetStrideY();
            }

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = cellSize;
            rect.anchoredPosition = new Vector2(x, y);
        }

        private int GetConstraintCount()
        {
            return Mathf.Max(1, constraintCount);
        }

        private float GetStrideX()
        {
            return Mathf.Max(1f, cellSize.x + spacing.x);
        }

        private float GetStrideY()
        {
            return Mathf.Max(1f, cellSize.y + spacing.y);
        }
    }
}
