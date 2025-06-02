using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class HandLayoutManager : SingletonBehaviour<HandLayoutManager>
{
    [Header("Layout Settings")]
    [SerializeField] private float cardSpacing = 120f;
    [SerializeField] private float arcHeight = 50f;
    [SerializeField] private float maxRotationAngle = 15f;
    
    [Header("Scale Settings")]
    [SerializeField] private float handScaleMultiplier = 0.75f;
    [SerializeField] private bool enableDynamicScaling = true;
    [SerializeField] private float minScaleReduction = 0.8f;
    [SerializeField] private int maxCardsForFullScale = 7;
    
    [Header("Animation")]
    [SerializeField] private float animationDuration = 0.3f;
    [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    private RectTransform rectTransform;
    private bool isAnimating = false;
    private List<Card> cachedHandCards = new();
    private readonly Dictionary<Card, Vector3> targetPositions = new();
    
    // Cached component references for performance
    private readonly Dictionary<Card, RectTransform> cardRectTransforms = new();
    
    protected override void OnAwakeInitialize()
    {
        rectTransform = GetComponent<RectTransform>();
    }
    
    public void UpdateLayout()
    {
        if (isAnimating) return;
        
        var handCards = CardManager.Instance?.GetHandCards() ?? new List<Card>();
        
        if (HasHandChanged(handCards))
        {
            cachedHandCards.Clear();
            cachedHandCards.AddRange(handCards);
            StartCoroutine(AnimateLayout());
        }
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
        if (cachedHandCards.Count == 0) yield break;
        
        isAnimating = true;
        
        var layoutData = CalculateLayout();
        float elapsed = 0f;
        
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float progress = animationCurve.Evaluate(elapsed / animationDuration);
            
            ApplyLayoutProgress(layoutData, progress);
            yield return null;
        }
        
        ApplyFinalLayout(layoutData);
        isAnimating = false;
    }
    
    private struct CardLayout
    {
        public RectTransform rectTransform;
        public Vector3 startPos;
        public Quaternion startRot;
        public Vector3 startScale;
        public Vector3 targetPos;
        public Quaternion targetRot;
        public Vector3 targetScale;
        
        public CardLayout(RectTransform rt, Vector3 tPos, Quaternion tRot, Vector3 tScale)
        {
            rectTransform = rt;
            startPos = rt.localPosition;
            startRot = rt.localRotation;
            startScale = rt.localScale;
            targetPos = tPos;
            targetRot = tRot;
            targetScale = tScale;
        }
    }
    
    private List<CardLayout> CalculateLayout()
    {
        var layouts = new List<CardLayout>(cachedHandCards.Count);
        int cardCount = cachedHandCards.Count;
        float handScale = GetHandScale();
        
        float totalWidth = (cardCount - 1) * cardSpacing;
        float startX = -totalWidth * 0.5f;
        
        targetPositions.Clear();
        
        for (int i = 0; i < cardCount; i++)
        {
            var card = cachedHandCards[i];
            if (card == null) continue;
            
            var rectTransform = GetCachedRectTransform(card);
            if (rectTransform == null || rectTransform.parent != this.rectTransform) continue;
            
            // Calculate arc position
            float normalizedPos = cardCount > 1 ? (float)i / (cardCount - 1) : 0.5f;
            float xPos = startX + (i * cardSpacing);
            float yPos = Mathf.Sin(normalizedPos * Mathf.PI) * arcHeight;
            var targetPos = new Vector3(xPos, yPos, 0);
            
            // Calculate rotation
            float rotationAngle = (normalizedPos - 0.5f) * maxRotationAngle * 2f;
            var targetRot = Quaternion.Euler(0, 0, rotationAngle);
            
            // Calculate scale
            Vector3 targetScale = Vector3.one * handScale;
            
            layouts.Add(new CardLayout(rectTransform, targetPos, targetRot, targetScale));
            targetPositions[card] = targetPos;
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
            layout.rectTransform.localRotation = Quaternion.Lerp(layout.startRot, layout.targetRot, progress);
            layout.rectTransform.localScale = Vector3.Lerp(layout.startScale, layout.targetScale, progress);
        }
    }
    
    private void ApplyFinalLayout(List<CardLayout> layouts)
    {
        foreach (var layout in layouts)
        {
            if (layout.rectTransform == null) continue;
            
            layout.rectTransform.localPosition = layout.targetPos;
            layout.rectTransform.localRotation = layout.targetRot;
            layout.rectTransform.localScale = layout.targetScale;
        }
    }
    
    public void SetLayoutImmediate()
    {
        if (isAnimating)
        {
            StopAllCoroutines();
            isAnimating = false;
        }
        
        var handCards = CardManager.Instance?.GetHandCards() ?? new List<Card>();
        cachedHandCards.Clear();
        cachedHandCards.AddRange(handCards);
        
        var layouts = CalculateLayout();
        ApplyFinalLayout(layouts);
    }
    
    public Vector3 GetTargetPositionForCard(Card card)
    {
        return targetPositions.TryGetValue(card, out var pos) ? pos : Vector3.zero;
    }
    
    // Cleanup cached references when cards are removed
    public void CleanupCardReference(Card card)
    {
        cardRectTransforms.Remove(card);
        targetPositions.Remove(card);
    }
    
    // Properties
    public float CardSpacing 
    { 
        get => cardSpacing; 
        set { cardSpacing = value; UpdateLayout(); }
    }
    
    public float HandScaleMultiplier 
    { 
        get => handScaleMultiplier; 
        set { handScaleMultiplier = value; UpdateLayout(); }
    }
    
    public bool IsAnimating => isAnimating;
    public int HandSize => cachedHandCards.Count;
}