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
    
    [Header("Drop Preview")]
    [SerializeField] private float dropPreviewWidth = 5f;
    [SerializeField] private Color dropPreviewColor = new Color(0, 1, 0, 0.6f);
    [SerializeField] private bool smoothDropPreview = true;
    
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
    
    // NEU: Position management
    private List<Card> _orderedCards = new List<Card>();
    private Dictionary<Card, int> _cardPositions = new Dictionary<Card, int>();
    private bool _layoutDirty = false;
    private bool _positionsNeedUpdate = false;
    
    // Drop Preview
    private GameObject _dropPreview;
    private int _dropPreviewIndex = -1;
    private bool _dropPreviewActive = false;
    
    // Reordering state
    private bool _isReordering = false;
    
    // State
    private bool _isReady = false;
    
    public bool IsReady => _isReady;
    
    // NEU: Events for position changes
    public static event System.Action<List<Card>> OnHandLayoutUpdated;
    public static event System.Action<Card, int, int> OnCardIndexChanged; // card, oldIndex, newIndex
    
    protected override void OnAwakeInitialize()
    {
        InitializeComponents();
        ConfigureLayoutGroup();
        CreateDropPreview();
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
    
    // NEU: Create drop preview object
    private void CreateDropPreview()
    {
        _dropPreview = new GameObject("DropPreview");
        _dropPreview.transform.SetParent(transform, false);
        
        var image = _dropPreview.AddComponent<Image>();
        image.color = dropPreviewColor;
        image.raycastTarget = false;
        
        var rect = _dropPreview.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(dropPreviewWidth, cardPreferredSize.y * handScale);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        
        _dropPreview.SetActive(false);
    }
    
    private void LateUpdate()
    {
        if (_layoutDirty || _positionsNeedUpdate)
        {
            PerformLayoutUpdate();
            _layoutDirty = false;
            _positionsNeedUpdate = false;
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
    
    // EVENT HANDLERS
    private void OnHandUpdated(List<Card> handCards)
    {
        SetCardOrder(handCards);
    }
    
    private void OnCardSpawned(Card card)
    {
        if (card != null && card.transform.parent == transform)
        {
            AddCard(card, 0); // Add new cards to the left
        }
    }
    
    // NEU: Position management methods
    public void SetCardOrder(List<Card> cards)
    {
        _orderedCards.Clear();
        _cardPositions.Clear();
        
        if (cards == null) return;
        
        _orderedCards = FilterValidCards(cards);
        UpdateCardPositions();
    }
    
    public void AddCard(Card card, int position = -1)
    {
        if (card == null || ContainsCard(_orderedCards, card)) return;
        
        if (position < 0 || position >= _orderedCards.Count)
            _orderedCards.Add(card);
        else
            _orderedCards.Insert(position, card);
        
        UpdateCardPositions();
    }
    
    public void RemoveCard(Card card)
    {
        if (RemoveCardFromList(_orderedCards, card))
        {
            RemoveFromDictionary(_cardPositions, card);
            UpdateCardPositions();
        }
    }
    
    // NEU: Move single card to specific position
    public void MoveCardToPosition(Card card, int newPosition)
    {
        if (card == null || !ContainsCard(_orderedCards, card)) return;
        
        int oldPosition = GetCardPosition(card);
        
        RemoveCardFromList(_orderedCards, card);
        newPosition = Mathf.Clamp(newPosition, 0, _orderedCards.Count);
        _orderedCards.Insert(newPosition, card);
        
        UpdateCardPositions();
        OnCardIndexChanged?.Invoke(card, oldPosition, newPosition);
    }
    
    // NEU: Move multiple cards to specific position
    public void MoveCardsToPosition(List<Card> cards, int targetPosition)
    {
        if (cards == null || cards.Count == 0) return;
        
        // Store old positions for events
        var oldPositions = new Dictionary<Card, int>();
        foreach (var card in cards)
        {
            oldPositions[card] = GetCardPosition(card);
        }
        
        // Remove all cards first
        foreach (var card in cards)
        {
            RemoveCardFromList(_orderedCards, card);
        }
        
        // Insert at target position
        targetPosition = Mathf.Clamp(targetPosition, 0, _orderedCards.Count);
        
        foreach (var card in cards)
        {
            _orderedCards.Insert(targetPosition, card);
            targetPosition++;
        }
        
        UpdateCardPositions();
        
        // Fire events
        foreach (var card in cards)
        {
            int newPosition = GetCardPosition(card);
            if (TryGetValue(oldPositions, card, out int oldPos) && oldPos != newPosition)
            {
                OnCardIndexChanged?.Invoke(card, oldPos, newPosition);
            }
        }
    }
    
    // NEU: Get drop index from screen position
    public int GetDropIndexFromScreenPosition(Vector2 screenPosition)
    {
        if (_orderedCards.Count == 0) return 0;
        
        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rectTransform,
                screenPosition,
                null,
                out localPoint))
        {
            return -1;
        }
        
        // Account for padding and convert to relative position
        float padding = _layoutGroup.padding.left;
        localPoint.x += _rectTransform.rect.width * 0.5f - padding;
        
        // Calculate position based on card spacing
        float cardWidth = cardPreferredSize.x * handScale;
        float totalCardSpace = cardWidth + cardSpacing;
        
        // Find insertion point
        int insertIndex = 0;
        float currentX = 0;
        
        for (int i = 0; i < _orderedCards.Count; i++)
        {
            float cardCenterX = currentX + cardWidth * 0.5f;
            
            if (localPoint.x < cardCenterX)
            {
                insertIndex = i;
                break;
            }
            
            currentX += totalCardSpace;
            insertIndex = i + 1;
        }
        
        return Mathf.Clamp(insertIndex, 0, _orderedCards.Count);
    }
    
    // NEU: Drop preview management
    public void ShowDropPreview(int index)
    {
        if (_dropPreview == null || _isReordering) return;
        
        if (_dropPreviewIndex == index && _dropPreviewActive) return;
        
        _dropPreviewIndex = index;
        _dropPreviewActive = true;
        
        _dropPreview.SetActive(true);
        PositionDropPreview(index);
        
        // Move drop preview to correct sibling index
        _dropPreview.transform.SetSiblingIndex(index);
        
        if (smoothDropPreview)
        {
            StartCoroutine(AnimateDropPreview());
        }
    }
    
    public void HideDropPreview()
    {
        if (_dropPreview == null) return;
        
        _dropPreviewIndex = -1;
        _dropPreviewActive = false;
        _dropPreview.SetActive(false);
        
        StopAllCoroutines();
    }
    
    private void PositionDropPreview(int index)
    {
        if (_dropPreview == null) return;
        
        var rect = _dropPreview.GetComponent<RectTransform>();
        var image = _dropPreview.GetComponent<Image>();
        
        // Update size to match current scale
        rect.sizeDelta = new Vector2(dropPreviewWidth, cardPreferredSize.y * handScale);
        
        // Position will be handled by layout group automatically when sibling index is set
        rect.anchoredPosition = Vector2.zero;
        
        // Update color intensity based on validity
        Color color = dropPreviewColor;
        color.a = 0.6f;
        image.color = color;
    }
    
    private IEnumerator AnimateDropPreview()
    {
        if (_dropPreview == null) yield break;
        
        var image = _dropPreview.GetComponent<Image>();
        Color startColor = dropPreviewColor;
        startColor.a = 0f;
        
        Color endColor = dropPreviewColor;
        endColor.a = 0.6f;
        
        float duration = 0.2f;
        float elapsed = 0f;
        
        while (elapsed < duration && _dropPreviewActive)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            image.color = Color.Lerp(startColor, endColor, t);
            yield return null;
        }
        
        if (_dropPreviewActive)
        {
            image.color = endColor;
        }
    }
    
    // NEU: Update card positions and indices
    private void UpdateCardPositions()
    {
        _cardPositions.Clear();
        
        for (int i = 0; i < _orderedCards.Count; i++)
        {
            var card = _orderedCards[i];
            if (card != null)
            {
                _cardPositions[card] = i;
                SetCardHandIndex(card, i);
            }
        }
        
        _positionsNeedUpdate = true;
        MarkLayoutDirty();
        OnHandLayoutUpdated?.Invoke(_orderedCards);
    }
    
    private void PerformLayoutUpdate()
    {
        if (_layoutGroup == null) return;
        
        // Apply visual ordering based on positions
        ApplyCardOrdering();
        ConfigureAllCards();
        
        if (enableDynamicScaling)
            ApplyDynamicScaling();
        
        LayoutRebuilder.ForceRebuildLayoutImmediate(_rectTransform);
    }
    
    // Ensure visual order matches logical order
    private void ApplyCardOrdering()
    {
        for (int i = 0; i < _orderedCards.Count; i++)
        {
            var card = _orderedCards[i];
            if (card != null && card.transform.parent == transform)
            {
                card.transform.SetSiblingIndex(i);
            }
        }
    }
    
    private void ConfigureAllCards()
    {
        foreach (var card in _orderedCards)
        {
            if (card == null) continue;
            
            var rectTransform = card.GetComponent<RectTransform>();
            if (rectTransform == null) continue;
            
            var layoutElement = card.GetComponent<LayoutElement>();
            if (layoutElement == null)
                layoutElement = card.gameObject.AddComponent<LayoutElement>();
            
            layoutElement.preferredWidth = cardPreferredSize.x * handScale;
            layoutElement.preferredHeight = cardPreferredSize.y * handScale;
            layoutElement.flexibleWidth = 0f;
            layoutElement.flexibleHeight = 0f;
        }
    }
    
    private void ApplyDynamicScaling()
    {
        if (!enableHandScaling || _orderedCards.Count == 0) return;
        
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
        float totalSpacing = cardSpacing * Mathf.Max(0, _orderedCards.Count - 1);
        float padding = paddingLeft + paddingRight;
        
        return (_orderedCards.Count * cardWidth) + totalSpacing + padding;
    }
    
    private void ApplyScaleToAllCards(float scale)
    {
        foreach (var card in _orderedCards)
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
        
        // Update drop preview size too
        if (_dropPreview != null)
        {
            var rect = _dropPreview.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(dropPreviewWidth, cardPreferredSize.y * scale);
        }
    }
    
    // Helper Methods - Standalone implementation to avoid Linq dependencies
    private List<Card> FilterValidCards(List<Card> cards)
    {
        var result = new List<Card>();
        foreach (var card in cards)
        {
            if (card != null && card.gameObject != null)
                result.Add(card);
        }
        return result;
    }
    
    private bool ContainsCard(List<Card> cards, Card card)
    {
        foreach (var c in cards)
        {
            if (c == card) return true;
        }
        return false;
    }
    
    private bool RemoveCardFromList(List<Card> cards, Card card)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] == card)
            {
                cards.RemoveAt(i);
                return true;
            }
        }
        return false;
    }
    
    private void RemoveFromDictionary(Dictionary<Card, int> dict, Card card)
    {
        if (dict.ContainsKey(card))
            dict.Remove(card);
    }
    
    private bool TryGetValue(Dictionary<Card, int> dict, Card card, out int value)
    {
        if (dict.ContainsKey(card))
        {
            value = dict[card];
            return true;
        }
        value = -1;
        return false;
    }
    
    // Card Helper Methods - Compatible with extension methods
    private void SetCardHandIndex(Card card, int index)
    {
        if (card == null) return;
        
        // Try direct property first
        var handIndexProperty = card.GetType().GetProperty("HandIndex");
        if (handIndexProperty != null && handIndexProperty.CanWrite)
        {
            handIndexProperty.SetValue(card, index);
            return;
        }
        
        // Fallback: Extension method compatible approach
        var setHandIndexMethod = card.GetType().GetMethod("SetHandIndex");
        if (setHandIndexMethod != null)
        {
            setHandIndexMethod.Invoke(card, new object[] { index });
        }
    }
    
    // Public API
    public void UpdateLayout() => MarkLayoutDirty();
    public void ForceImmediateLayout() => PerformLayoutUpdate();
    private void MarkLayoutDirty() => _layoutDirty = true;
    
    // Getters for current state
    public List<Card> GetOrderedCards() => new List<Card>(_orderedCards);
    public int GetCardPosition(Card card) 
    {
        if (TryGetValue(_cardPositions, card, out int position))
            return position;
        return -1;
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
        RemoveCard(card);
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
        Debug.Log($"  Tracked Cards: {_orderedCards.Count}");
        Debug.Log($"  Child Count: {transform.childCount}");
    }
    
    [ContextMenu("Debug Card Positions")]
    public void DebugCardPositions()
    {
        Debug.Log($"[HandLayoutManager] Card Positions:");
        for (int i = 0; i < _orderedCards.Count; i++)
        {
            var card = _orderedCards[i];
            Debug.Log($"  {i}: {(card?.GetCardName() ?? "NULL")} (Index: {GetCardHandIndex(card)})");
        }
    }
    
    [ContextMenu("Test Drop Preview")]
    public void TestDropPreview()
    {
        if (_orderedCards.Count > 0)
        {
            ShowDropPreview(_orderedCards.Count / 2);
            StartCoroutine(HidePreviewAfterDelay());
        }
    }
    
    private IEnumerator HidePreviewAfterDelay()
    {
        yield return new WaitForSeconds(2f);
        HideDropPreview();
    }
    
    private int GetCardHandIndex(Card card)
    {
        if (card == null) return -1;
        
        // Try direct property first
        var handIndexProperty = card.GetType().GetProperty("HandIndex");
        if (handIndexProperty != null && handIndexProperty.CanRead)
        {
            var value = handIndexProperty.GetValue(card);
            return value is int intValue ? intValue : -1;
        }
        
        // Fallback: Use position in ordered cards
        for (int i = 0; i < _orderedCards.Count; i++)
        {
            if (_orderedCards[i] == card)
                return i;
        }
        
        return -1;
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
        dropPreviewWidth = Mathf.Max(1f, dropPreviewWidth);
    }
#endif
}