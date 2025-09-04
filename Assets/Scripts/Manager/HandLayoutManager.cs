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
    
    // Components
    private HorizontalLayoutGroup _layoutGroup;
    private RectTransform _rectTransform;
    private ContentSizeFitter _contentSizeFitter;
    
    // FIX 2&3: Better position management
    private List<Card> _orderedCards = new List<Card>();
    private Dictionary<Card, int> _cardPositions = new Dictionary<Card, int>();
    private bool _layoutDirty = false;
    private bool _positionsNeedUpdate = false;
    
    // Drop Preview
    private GameObject _dropPreview;
    private int _dropPreviewIndex = -1;
    
    public bool IsReady { get; private set; }
    
    protected override void OnAwakeInitialize()
    {
        InitializeComponents();
        ConfigureLayoutGroup();
        IsReady = true;
    }
    
    private void InitializeComponents()
    {
        _rectTransform = GetComponent<RectTransform>();
        _layoutGroup = GetComponent<HorizontalLayoutGroup>();
        
        if (_layoutGroup == null)
            _layoutGroup = gameObject.AddComponent<HorizontalLayoutGroup>();
        
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
        _layoutGroup.padding = new RectOffset(10, 10, 10, 10);
        _layoutGroup.childForceExpandWidth = false;
        _layoutGroup.childForceExpandHeight = false;
        _layoutGroup.childControlWidth = true;
        _layoutGroup.childControlHeight = true;
        _layoutGroup.childScaleWidth = false;
        _layoutGroup.childScaleHeight = false;
        _layoutGroup.childAlignment = TextAnchor.MiddleCenter;
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
    
    // FIX 2: Proper position management
    public void SetCardOrder(List<Card> cards)
    {
        _orderedCards.Clear();
        _cardPositions.Clear();
        
        if (cards == null) return;
        
        _orderedCards = cards.Where(c => c.IsValid()).ToList();
        UpdateCardPositions();
    }
    
    public void AddCard(Card card, int position = -1)
    {
        if (card == null || _orderedCards.Contains(card)) return;
        
        if (position < 0 || position >= _orderedCards.Count)
            _orderedCards.Add(card);
        else
            _orderedCards.Insert(position, card);
        
        UpdateCardPositions();
    }
    
    public void RemoveCard(Card card)
    {
        if (_orderedCards.Remove(card))
        {
            _cardPositions.Remove(card);
            UpdateCardPositions();
        }
    }
    
    // FIX 3: Proper card movement with visual update
    public void MoveCardToPosition(Card card, int newPosition)
    {
        if (card == null || !_orderedCards.Contains(card)) return;
        
        _orderedCards.Remove(card);
        
        newPosition = Mathf.Clamp(newPosition, 0, _orderedCards.Count);
        _orderedCards.Insert(newPosition, card);
        
        UpdateCardPositions();
    }
    
    public void MoveCardsToPosition(List<Card> cards, int targetPosition)
    {
        if (cards == null || cards.Count == 0) return;
        
        // Remove all cards first
        foreach (var card in cards)
        {
            _orderedCards.Remove(card);
        }
        
        // Insert at target position
        targetPosition = Mathf.Clamp(targetPosition, 0, _orderedCards.Count);
        
        foreach (var card in cards)
        {
            _orderedCards.Insert(targetPosition, card);
            targetPosition++;
        }
        
        UpdateCardPositions();
    }
    
    // FIX 2&3: Update positions and trigger visual update
    private void UpdateCardPositions()
    {
        _cardPositions.Clear();
        
        for (int i = 0; i < _orderedCards.Count; i++)
        {
            var card = _orderedCards[i];
            if (card != null)
            {
                _cardPositions[card] = i;
                card.SetHandIndex(i);
            }
        }
        
        _positionsNeedUpdate = true;
        MarkLayoutDirty();
    }
    
    private void PerformLayoutUpdate()
    {
        if (_layoutGroup == null) return;
        
        // FIX 3: Apply visual ordering based on positions
        ApplyCardOrdering();
        ConfigureAllCards();
        
        if (enableDynamicScaling)
            ApplyDynamicScaling();
        
        LayoutRebuilder.ForceRebuildLayoutImmediate(_rectTransform);
    }
    
    // FIX 3: Ensure visual order matches logical order
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
    
    // Get drop index from screen position
    public int GetDropIndexFromScreenPosition(Vector2 screenPosition)
    {
        if (_orderedCards.Count == 0) return 0;
        
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rectTransform,
            screenPosition,
            null,
            out localPoint
        );
        
        // Find closest position
        for (int i = 0; i < _orderedCards.Count; i++)
        {
            var card = _orderedCards[i];
            if (card == null) continue;
            
            var cardRect = card.GetComponent<RectTransform>();
            if (cardRect == null) continue;
            
            if (localPoint.x < cardRect.anchoredPosition.x)
                return i;
        }
        
        return _orderedCards.Count;
    }
    
    public void ShowDropPreview(int index)
    {
        if (_dropPreviewIndex == index) return;
        _dropPreviewIndex = index;
        
        if (_dropPreview == null)
            CreateDropPreview();
        
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
        
        var image = _dropPreview.AddComponent<Image>();
        image.color = new Color(0, 1, 0, 0.3f);
        
        var rect = _dropPreview.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(5, cardPreferredSize.y);
    }
    
    private void PositionDropPreview(int index)
    {
        if (_dropPreview == null) return;
        
        var rect = _dropPreview.GetComponent<RectTransform>();
        rect.SetSiblingIndex(Mathf.Min(index, transform.childCount - 1));
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
        return (_orderedCards.Count * cardWidth) + totalSpacing + 20;
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
    }
    
    // Public API
    public void UpdateLayout() => MarkLayoutDirty();
    public void ForceImmediateLayout() => PerformLayoutUpdate();
    private void MarkLayoutDirty() => _layoutDirty = true;
    
    // Event handlers
    private void OnEnable()
    {
        if (CardManager.HasInstance)
        {
            CardManager.OnHandUpdated += OnHandUpdated;
            CardManager.OnCardSpawned += OnCardSpawned;
        }
    }
    
    private void OnDisable()
    {
        if (CardManager.HasInstance)
        {
            CardManager.OnHandUpdated -= OnHandUpdated;
            CardManager.OnCardSpawned -= OnCardSpawned;
        }
    }
    
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
    
    public void CleanupCardReference(Card card)
    {
        RemoveCard(card);
    }
    
    // Get current hand order
    public List<Card> GetOrderedCards() => new List<Card>(_orderedCards);
    public int GetCardPosition(Card card) => _cardPositions.GetValueOrDefault(card, -1);

#if UNITY_EDITOR
    [ContextMenu("Debug Card Positions")]
    public void DebugCardPositions()
    {
        Debug.Log($"[HandLayoutManager] Card Positions:");
        for (int i = 0; i < _orderedCards.Count; i++)
        {
            var card = _orderedCards[i];
            Debug.Log($"  {i}: {card?.GetCardName()} (Index: {card?.HandIndex})");
        }
    }
#endif
}