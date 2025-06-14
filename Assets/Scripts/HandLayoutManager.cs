using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class HandLayoutManager : SingletonBehaviour<HandLayoutManager>, IGameManager
{
    [Header("Layout Settings")]
    [SerializeField] private float cardSpacing = 120f;
    [SerializeField] private float handScaleMultiplier = 0.75f;
    [SerializeField] private bool enableDynamicScaling = true;
    [SerializeField] private float minScaleReduction = 0.8f;
    [SerializeField] private int maxCardsForFullScale = 7;
    
    [Header("Animation")]
    [SerializeField] private float animationDuration = 0.3f;
    [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    private bool _isReady = false;
    public bool IsReady => _isReady;
    
    private RectTransform rectTransform;
    private bool isAnimating = false;
    private List<Card> cachedHandCards = new();
    private readonly Dictionary<Card, RectTransform> cardRectTransforms = new();
    
    // Animation queueing to prevent conflicts
    private bool _pendingUpdate = false;
    private Coroutine _currentAnimation = null;
    
    // Properties matching GameUIHandler expectations
    public bool IsAnimating => isAnimating;
    public int HandSize => cachedHandCards.Count;
    
    protected override void OnAwakeInitialize()
    {
        rectTransform = GetComponent<RectTransform>();
        _isReady = true;
    }
    
    private void OnEnable()
    {
        // Listen to CardManager events
        if (CardManager.HasInstance)
        {
            CardManager.OnHandUpdated += OnHandUpdated;
        }
        else
        {
            CardManager.OnCardManagerInitialized += () => {
                CardManager.OnHandUpdated += OnHandUpdated;
            };
        }
    }
    
    private void OnDisable()
    {
        if (CardManager.HasInstance)
        {
            CardManager.OnHandUpdated -= OnHandUpdated;
        }
    }
    
    private void OnHandUpdated(List<Card> handCards)
    {
        UpdateLayout();
    }
    
    public void UpdateLayout()
    {
        if (!_isReady) return;
        
        var handCards = CardManager.HasInstance ? CardManager.Instance.GetHandCards() : new List<Card>();
        
        if (HasHandChanged(handCards))
        {
            cachedHandCards.Clear();
            cachedHandCards.AddRange(handCards);
            
            if (isAnimating)
            {
                // Queue update for after current animation
                _pendingUpdate = true;
            }
            else
            {
                _currentAnimation = StartCoroutine(AnimateLayout());
            }
        }
    }
    
    public void SetLayoutImmediate()
    {
        if (!_isReady) return;
        
        // Stop any ongoing animation
        if (_currentAnimation != null)
        {
            StopCoroutine(_currentAnimation);
            _currentAnimation = null;
        }
        isAnimating = false;
        _pendingUpdate = false;
        
        var handCards = CardManager.HasInstance ? CardManager.Instance.GetHandCards() : new List<Card>();
        cachedHandCards.Clear();
        cachedHandCards.AddRange(handCards);
        
        var layouts = CalculateLayout();
        ApplyFinalLayout(layouts);
    }
    
    private bool HasHandChanged(List<Card> newHandCards)
    {
        if (cachedHandCards.Count != newHandCards.Count) return true;
        
        for (int i = 0; i < cachedHandCards.Count; i++)
        {
            if (i >= newHandCards.Count || cachedHandCards[i] != newHandCards[i])
                return true;
        }
        
        return false;
    }
    
    private IEnumerator AnimateLayout()
    {
        if (cachedHandCards.Count == 0)
        {
            isAnimating = false;
            yield break;
        }
        
        isAnimating = true;
        
        var layouts = CalculateLayout();
        float elapsed = 0f;
        
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float progress = animationCurve.Evaluate(elapsed / animationDuration);
            
            ApplyLayoutProgress(layouts, progress);
            yield return null;
        }
        
        ApplyFinalLayout(layouts);
        isAnimating = false;
        _currentAnimation = null;
        
        // Process pending update if one was queued
        if (_pendingUpdate)
        {
            _pendingUpdate = false;
            UpdateLayout();
        }
    }
    
    private struct CardLayout
    {
        public RectTransform rectTransform;
        public Vector3 startPos;
        public Vector3 startScale;
        public Vector3 targetPos;
        public Vector3 targetScale;
        
        public CardLayout(RectTransform rt, Vector3 tPos, Vector3 tScale)
        {
            rectTransform = rt;
            startPos = rt.localPosition;
            startScale = rt.localScale;
            targetPos = tPos;
            targetScale = tScale;
        }
    }
    
    private List<CardLayout> CalculateLayout()
    {
        var layouts = new List<CardLayout>(cachedHandCards.Count);
        int cardCount = cachedHandCards.Count;
        float handScale = GetHandScale();
        
        if (cardCount == 0) return layouts;
        
        float totalWidth = (cardCount - 1) * cardSpacing;
        float startX = -totalWidth * 0.5f;
        
        for (int i = 0; i < cardCount; i++)
        {
            var card = cachedHandCards[i];
            if (card == null) continue;
            
            var rectTransform = GetCachedRectTransform(card);
            if (rectTransform == null) continue;
            
            // Ensure card is properly parented before calculating layout
            if (rectTransform.parent != this.rectTransform)
            {
                rectTransform.SetParent(this.rectTransform, false);
                // Reset transform when reparenting
                rectTransform.localPosition = Vector3.zero;
                rectTransform.localRotation = Quaternion.identity;
                rectTransform.anchoredPosition = Vector2.zero;
            }
            
            float xPos = startX + (i * cardSpacing);
            var targetPos = new Vector3(xPos, 0, 0);
            var targetScale = Vector3.one * handScale;
            
            layouts.Add(new CardLayout(rectTransform, targetPos, targetScale));
        }
        
        return layouts;
    }
    
    private float GetHandScale()
    {
        if (!enableDynamicScaling) return handScaleMultiplier;
        
        int cardCount = cachedHandCards.Count;
        if (cardCount <= maxCardsForFullScale) return handScaleMultiplier;
        
        float reductionFactor = Mathf.InverseLerp(maxCardsForFullScale, 12, cardCount);
        return handScaleMultiplier * Mathf.Lerp(1f, minScaleReduction, reductionFactor);
    }
    
    private RectTransform GetCachedRectTransform(Card card)
    {
        if (!cardRectTransforms.TryGetValue(card, out var rectTransform) || rectTransform == null)
        {
            rectTransform = card.GetComponent<RectTransform>();
            if (rectTransform != null)
                cardRectTransforms[card] = rectTransform;
        }
        return rectTransform;
    }
    
    private void ApplyLayoutProgress(List<CardLayout> layouts, float progress)
    {
        foreach (var layout in layouts)
        {
            if (layout.rectTransform == null) continue;
            
            layout.rectTransform.localPosition = Vector3.Lerp(layout.startPos, layout.targetPos, progress);
            layout.rectTransform.localScale = Vector3.Lerp(layout.startScale, layout.targetScale, progress);
        }
    }
    
    private void ApplyFinalLayout(List<CardLayout> layouts)
    {
        foreach (var layout in layouts)
        {
            if (layout.rectTransform == null) continue;
            
            layout.rectTransform.localPosition = layout.targetPos;
            layout.rectTransform.localScale = layout.targetScale;
        }
    }
    
    public void CleanupCardReference(Card card)
    {
        if (card != null)
        {
            cardRectTransforms.Remove(card);
            cachedHandCards.Remove(card);
        }
    }
    
    private void OnDestroy()
    {
        if (_currentAnimation != null)
        {
            StopCoroutine(_currentAnimation);
        }
        cardRectTransforms.Clear();
        cachedHandCards.Clear();
    }
}