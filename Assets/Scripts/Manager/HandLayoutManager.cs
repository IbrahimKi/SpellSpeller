
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

[RequireComponent(typeof(HorizontalLayoutGroup))]
[RequireComponent(typeof(RectTransform))]
public class HandLayoutManager : SingletonBehaviour<HandLayoutManager>, IGameManager
{
    [Header("Card Settings")]
    [SerializeField] private float cardSpacing = 20f;
    [SerializeField] private float handScale = 1f;
    [SerializeField] private Vector2 cardPreferredSize = new Vector2(120f, 180f);
    
    [Header("Layout Optimization")]
    [SerializeField] private bool enableDynamicScaling = false;
    [SerializeField] private float maxHandWidth = 800f;
    [SerializeField] private bool enableHandScaling = false;
    [SerializeField] private float handScaleMultiplier = 0.8f;
    
    [Header("Layout Group Settings")]
    [SerializeField] private bool childForceExpandWidth = false;
    [SerializeField] private bool childForceExpandHeight = false;
    [SerializeField] private bool childControlWidth = true;
    [SerializeField] private bool childControlHeight = true;
    [SerializeField] private bool childScaleWidth = false;
    [SerializeField] private bool childScaleHeight = false;
    [SerializeField] private TextAnchor childAlignment = TextAnchor.MiddleCenter;
    
    [Header("Padding")]
    [SerializeField] private int paddingLeft = 10;
    [SerializeField] private int paddingRight = 10;
    [SerializeField] private int paddingTop = 10;
    [SerializeField] private int paddingBottom = 10;
    
    // Components
    private HorizontalLayoutGroup _layoutGroup;
    private RectTransform _rectTransform;
    private ContentSizeFitter _contentSizeFitter;
    
    // PERFORMANCE FIX: Batch Layout Updates statt redundante Updates
    private bool _layoutDirty = false;
    private List<Card> _trackedCards = new List<Card>();
    
    // Card Ordering & Drop Preview
    private Dictionary<Card, int> _cardIndices = new Dictionary<Card, int>();
    private GameObject _dropPreview;
    private int _dropPreviewIndex = -1;
    
    // State
    private bool _isReady = false;
    
    public bool IsReady => _isReady;
    
    protected override void OnAwakeInitialize()
    {
        InitializeComponents();
        ConfigureLayoutGroup();
        _isReady = true;
    }
    
    private void InitializeComponents()
    {
        _rectTransform = GetComponent<RectTransform>();
        _layoutGroup = GetComponent<HorizontalLayoutGroup>();
        
        if (_layoutGroup == null)
        {
            _layoutGroup = gameObject.AddComponent<HorizontalLayoutGroup>();
            Debug.Log("[HandLayoutManager] Added HorizontalLayoutGroup component");
        }
        
        _contentSizeFitter = GetComponent<ContentSizeFitter>();
        if (_contentSizeFitter == null)
        {
            _contentSizeFitter = gameObject.AddComponent<ContentSizeFitter>();
            _contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            _contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        }
    }
    
    private void ConfigureLayoutGroup()
    {
        if (_layoutGroup == null) return;
        
        _layoutGroup.spacing = cardSpacing;
        _layoutGroup.padding = new RectOffset(paddingLeft, paddingRight, paddingTop, paddingBottom);
        _layoutGroup.childForceExpandWidth = childForceExpandWidth;
        _layoutGroup.childForceExpandHeight = childForceExpandHeight;
        _layoutGroup.childControlWidth = childControlWidth;
        _layoutGroup.childControlHeight = childControlHeight;
        _layoutGroup.childScaleWidth = childScaleWidth;
        _layoutGroup.childScaleHeight = childScaleHeight;
        _layoutGroup.childAlignment = childAlignment;
        
        Debug.Log("[HandLayoutManager] HorizontalLayoutGroup configured optimally");
    }
    
    // PERFORMANCE FIX: LateUpdate statt Coroutine für Layout-Updates
    private void LateUpdate()
    {
        if (_layoutDirty)
        {
            PerformLayoutUpdate();
            _layoutDirty = false;
        }
    }
    
    private void OnEnable()
    {
        SubscribeToCardManager();
    }
    
    private void OnDisable()
    {
        UnsubscribeFromCardManager();
    }
    
    private void SubscribeToCardManager()
    {
        if (CardManager.HasInstance)
        {
            CardManager.OnHandUpdated += OnHandUpdated;
            CardManager.OnCardSpawned += OnCardSpawned;
        }
        else
        {
            CardManager.OnCardManagerInitialized += DelayedSubscribe;
        }
    }
    
    private void UnsubscribeFromCardManager()
    {
        if (CardManager.HasInstance)
        {
            CardManager.OnHandUpdated -= OnHandUpdated;
            CardManager.OnCardSpawned -= OnCardSpawned;
        }
        CardManager.OnCardManagerInitialized -= DelayedSubscribe;
    }
    
    private void DelayedSubscribe()
    {
        SubscribeToCardManager();
    }
    
    // EVENT HANDLERS - OPTIMIERT: Nur dirty flag setzen
    private void OnHandUpdated(List<Card> handCards)
    {
        UpdateTrackedCards(handCards);
        MarkLayoutDirty();
    }
    
    private void OnCardSpawned(Card card)
    {
        if (card != null && card.transform.parent == transform)
        {
            SetupNewCard(card);
            InsertCardAtPosition(card, HandPosition.Left); // Cards spawn on left
        }
    }
    
    // PERFORMANCE FIX: Dirty Flag Pattern
    private void MarkLayoutDirty() => _layoutDirty = true;
    
    private void PerformLayoutUpdate()
    {
        if (_layoutGroup == null) return;
        
        ConfigureAllCards();
        
        if (enableDynamicScaling)
        {
            ApplyDynamicScaling();
        }
        
        LayoutRebuilder.ForceRebuildLayoutImmediate(_rectTransform);
    }
    
    // CARD CONFIGURATION - Verbessert ohne LayoutElement Manipulation
    private void SetupNewCard(Card card)
    {
        if (card == null) return;
        
        var rectTransform = card.GetComponent<RectTransform>();
        if (rectTransform == null) return;
        
        var layoutElement = card.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = card.gameObject.AddComponent<LayoutElement>();
        }
        
        layoutElement.preferredWidth = cardPreferredSize.x * handScale;
        layoutElement.preferredHeight = cardPreferredSize.y * handScale;
        layoutElement.flexibleWidth = 0f;
        layoutElement.flexibleHeight = 0f;
        
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.localScale = Vector3.one;
        
        if (!_trackedCards.Contains(card))
        {
            _trackedCards.Add(card);
        }
    }
    
    private void ConfigureAllCards()
    {
        foreach (Transform child in transform)
        {
            var card = child.GetComponent<Card>();
            if (card != null)
            {
                SetupNewCard(card);
            }
        }
    }
    
    private void UpdateTrackedCards(List<Card> newCards)
    {
        _trackedCards.Clear();
        if (newCards != null)
        {
            _trackedCards.AddRange(newCards.Where(c => c != null && c.transform.parent == transform));
        }
        UpdateCardIndices();
    }
    
    // CARD ORDERING METHODS
    public void MoveCardsToIndex(List<Card> cards, int targetIndex)
    {
        if (cards == null || cards.Count == 0) return;
        
        // Remove cards from current positions
        foreach (var card in cards)
        {
            _trackedCards.Remove(card);
        }
        
        // Insert at target position
        targetIndex = Mathf.Clamp(targetIndex, 0, _trackedCards.Count);
        _trackedCards.InsertRange(targetIndex, cards);
        
        // Update indices
        UpdateCardIndices();
        MarkLayoutDirty();
    }
    
    public int GetDropIndex(Vector2 screenPosition)
    {
        if (_trackedCards.Count == 0) return 0;
        
        // Convert screen position to local position
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rectTransform,
            screenPosition,
            null,
            out Vector2 localPoint
        );
        
        // Find closest card position
        float closestDistance = float.MaxValue;
        int closestIndex = 0;
        
        for (int i = 0; i < _trackedCards.Count; i++)
        {
            var card = _trackedCards[i];
            if (card == null) continue;
            
            var cardRect = card.GetComponent<RectTransform>();
            if (cardRect == null) continue;
            
            float distance = Mathf.Abs(cardRect.anchoredPosition.x - localPoint.x);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
                
                // Check if we're past the midpoint
                if (localPoint.x > cardRect.anchoredPosition.x)
                    closestIndex = i + 1;
            }
        }
        
        return Mathf.Clamp(closestIndex, 0, _trackedCards.Count);
    }
    
    public void ShowDropPreview(int index)
    {
        if (_dropPreviewIndex == index) return;
        
        _dropPreviewIndex = index;
        
        if (_dropPreview == null)
        {
            CreateDropPreview();
        }
        
        if (_dropPreview != null)
        {
            _dropPreview.SetActive(true);
            PositionDropPreview(index);
        }
    }
    
    public void HideDropPreview()
    {
        _dropPreviewIndex = -1;
        if (_dropPreview != null)
            _dropPreview.SetActive(false);
    }
    
    private void CreateDropPreview()
    {
        _dropPreview = new GameObject("DropPreview");
        _dropPreview.transform.SetParent(transform, false);
        
        var image = _dropPreview.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(0, 1, 0, 0.3f);
        
        var rect = _dropPreview.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(5, cardPreferredSize.y);
    }
    
    private void PositionDropPreview(int index)
    {
        if (_dropPreview == null) return;
        
        var rect = _dropPreview.GetComponent<RectTransform>();
        
        if (index >= _trackedCards.Count)
        {
            // Position at end
            if (_trackedCards.Count > 0)
            {
                var lastCard = _trackedCards[_trackedCards.Count - 1];
                var lastRect = lastCard.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(
                    lastRect.anchoredPosition.x + cardPreferredSize.x + cardSpacing,
                    0
                );
            }
            else
            {
                rect.anchoredPosition = Vector2.zero;
            }
        }
        else if (index > 0)
        {
            // Position between cards
            var prevCard = _trackedCards[index - 1];
            var prevRect = prevCard.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(
                prevRect.anchoredPosition.x + (cardPreferredSize.x + cardSpacing) / 2,
                0
            );
        }
        else
        {
            // Position at start
            rect.anchoredPosition = new Vector2(-cardSpacing / 2, 0);
        }
    }
    
    private void UpdateCardIndices()
    {
        _cardIndices.Clear();
        for (int i = 0; i < _trackedCards.Count; i++)
        {
            var card = _trackedCards[i];
            if (card != null)
            {
                _cardIndices[card] = i;
                card.SetHandIndex(i);
            }
        }
    }
    
    public void InsertCardAtPosition(Card card, HandPosition position)
    {
        if (card == null) return;
        
        int targetIndex = position switch
        {
            HandPosition.Left => 0,
            HandPosition.Right => _trackedCards.Count,
            HandPosition.Center => _trackedCards.Count / 2,
            _ => _trackedCards.Count
        };
        
        _trackedCards.Insert(targetIndex, card);
        UpdateCardIndices();
        MarkLayoutDirty();
    }
    
    private void ApplyDynamicScaling()
    {
        if (!enableHandScaling || _trackedCards.Count == 0) return;
        
        float requiredWidth = CalculateRequiredWidth();
        
        if (requiredWidth > maxHandWidth)
        {
            float scaleMultiplier = maxHandWidth / requiredWidth;
            scaleMultiplier = Mathf.Max(scaleMultiplier, 0.5f);
            
            ApplyScaleToAllCards(scaleMultiplier * handScaleMultiplier);
        }
        else
        {
            ApplyScaleToAllCards(handScale);
        }
    }
    
    private float CalculateRequiredWidth()
    {
        float cardWidth = cardPreferredSize.x * handScale;
        float totalSpacing = cardSpacing * Mathf.Max(0, _trackedCards.Count - 1);
        float padding = paddingLeft + paddingRight;
        
        return (_trackedCards.Count * cardWidth) + totalSpacing + padding;
    }
    
    private void ApplyScaleToAllCards(float scale)
    {
        foreach (var card in _trackedCards)
        {
            if (card != null)
            {
                var layoutElement = card.GetComponent<LayoutElement>();
                if (layoutElement != null)
                {
                    layoutElement.preferredWidth = cardPreferredSize.x * scale;
                    layoutElement.preferredHeight = cardPreferredSize.y * scale;
                }
            }
        }
    }
    
    // PUBLIC API
    public void UpdateLayout()
    {
        MarkLayoutDirty();
    }
    
    public void ForceImmediateLayout()
    {
        PerformLayoutUpdate();
    }
    
    public void SetSpacing(float spacing)
    {
        cardSpacing = spacing;
        if (_layoutGroup != null)
        {
            _layoutGroup.spacing = spacing;
            MarkLayoutDirty();
        }
    }
    
    public void SetHandScale(float scale)
    {
        handScale = Mathf.Max(0.1f, scale);
        MarkLayoutDirty();
    }
    
    public void SetCardSize(Vector2 size)
    {
        cardPreferredSize = size;
        MarkLayoutDirty();
    }
    
    public void SetPadding(int left, int right, int top, int bottom)
    {
        paddingLeft = left;
        paddingRight = right;
        paddingTop = top;
        paddingBottom = bottom;
        
        if (_layoutGroup != null)
        {
            _layoutGroup.padding = new RectOffset(left, right, top, bottom);
            MarkLayoutDirty();
        }
    }
    
    public void CleanupCardReference(Card card)
    {
        _trackedCards.Remove(card);
    }

#if UNITY_EDITOR
    [ContextMenu("Force Layout Update")]
    public void DebugForceLayout()
    {
        ForceImmediateLayout();
    }
    
    [ContextMenu("Reset Card Sizes")]
    public void DebugResetCardSizes()
    {
        handScale = 1f;
        cardPreferredSize = new Vector2(120f, 180f);
        ForceImmediateLayout();
    }
    
    [ContextMenu("Log Layout Settings")]
    public void DebugLogSettings()
    {
        Debug.Log($"[HandLayoutManager] Settings:");
        Debug.Log($"  Card Scale: {handScale}");
        Debug.Log($"  Card Size: {cardPreferredSize}");
        Debug.Log($"  Spacing: {cardSpacing}");
        Debug.Log($"  Dynamic Scaling: {enableDynamicScaling}");
        Debug.Log($"  Tracked Cards: {_trackedCards.Count}");
        Debug.Log($"  Child Count: {transform.childCount}");
    }
    
    private void OnValidate()
    {
        if (Application.isPlaying && _layoutGroup != null)
        {
            ConfigureLayoutGroup();
            MarkLayoutDirty();
        }
        
        handScale = Mathf.Max(0.1f, handScale);
        cardSpacing = Mathf.Max(0f, cardSpacing);
        cardPreferredSize.x = Mathf.Max(50f, cardPreferredSize.x);
        cardPreferredSize.y = Mathf.Max(50f, cardPreferredSize.y);
    }
#endif
}