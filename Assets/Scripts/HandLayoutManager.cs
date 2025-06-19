using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class HandLayoutManager : SingletonBehaviour<HandLayoutManager>, IGameManager
{
    [Header("References")]
    private RectTransform rectTransform;
    
    private bool _isReady = false;
    public bool IsReady => _isReady;
    
    [Header("Layout Settings")]
    [SerializeField] private float cardSpacing = 120f;
    [SerializeField] private float handScaleMultiplier = 0.75f;
    [SerializeField] private bool enableDynamicScaling = true;
    [SerializeField] private float minScaleReduction = 0.8f;
    [SerializeField] private int maxCardsForFullScale = 7;
    
    [Header("Aspect Ratio")]
    [SerializeField] private bool preserveAspectRatio = true;
    [SerializeField] private float targetCardHeight = 200f; // Ziel-Höhe für Karten
    
    [Header("Animation")]
    [SerializeField] private float animationDuration = 0.3f;
    [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    // Internal state
    private readonly List<Card> cachedHandCards = new List<Card>();
    private readonly Dictionary<Card, RectTransform> cardRectTransforms = new Dictionary<Card, RectTransform>();
    private Coroutine _currentAnimation;
    [SerializeField] private bool enableHandScaling = false;

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
            CardManager.OnCardManagerInitialized += SubscribeToCardManager;
        }
    }
    
    private void OnDisable()
    {
        if (CardManager.HasInstance)
        {
            CardManager.OnHandUpdated -= OnHandUpdated;
        }
        CardManager.OnCardManagerInitialized -= SubscribeToCardManager;
    }
    
    private void SubscribeToCardManager()
    {
        if (CardManager.HasInstance)
        {
            CardManager.OnHandUpdated += OnHandUpdated;
        }
    }
    
    private void OnHandUpdated(List<Card> handCards)
    {
        UpdateLayout();
    }
    
    // Public API
    public void UpdateLayout()
    {
        RefreshCardList();
        
        if (cachedHandCards.Count == 0)
        {
            Debug.Log("[HandLayoutManager] No cards to layout");
            return;
        }
        
        var targetLayouts = CalculateLayout();
        
        if (_currentAnimation != null)
        {
            StopCoroutine(_currentAnimation);
        }
        
        _currentAnimation = StartCoroutine(AnimateToLayout(targetLayouts));
    }
    
    // Simple immediate layout for initialization
    public void ForceImmediateLayout()
    {
        RefreshCardList();
        var layouts = CalculateLayout();
        
        foreach (var layout in layouts)
        {
            if (layout.rectTransform != null)
            {
                layout.rectTransform.localPosition = layout.targetPosition;
                layout.rectTransform.localScale = layout.targetScale;
            }
        }
    }
    
    private void RefreshCardList()
    {
        cachedHandCards.Clear();
        cardRectTransforms.Clear();
        
        if (CardManager.HasInstance)
        {
            var handCards = CardManager.Instance.GetHandCards();
            cachedHandCards.AddRange(handCards);
            
            // Pre-cache RectTransforms
            foreach (var card in cachedHandCards)
            {
                if (card != null)
                {
                    var rt = card.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        cardRectTransforms[card] = rt;
                    }
                }
            }
        }
    }
    
    private float GetHandScale()
    {
        // Master-Schalter: Wenn Skalierung komplett deaktiviert, immer 1.0
        if (!enableHandScaling) return 1f;
        
        // Basis-Skalierung ohne dynamische Anpassung
        if (!enableDynamicScaling) return handScaleMultiplier;
        
        // Dynamische Skalierung basierend auf Kartenanzahl
        int cardCount = cachedHandCards.Count;
        if (cardCount <= maxCardsForFullScale) return handScaleMultiplier;
        
        float reductionFactor = Mathf.InverseLerp(maxCardsForFullScale, 12, cardCount);
        return handScaleMultiplier * Mathf.Lerp(1f, minScaleReduction, reductionFactor);
    }
    
    private RectTransform GetCachedRectTransform(Card card)
    {
        if (cardRectTransforms.TryGetValue(card, out var rt))
            return rt;
            
        rt = card.GetComponent<RectTransform>();
        if (rt != null)
            cardRectTransforms[card] = rt;
            
        return rt;
    }
    
    private List<CardLayout> CalculateLayout()
    {
        var layouts = new List<CardLayout>(cachedHandCards.Count);
        int cardCount = cachedHandCards.Count;
        float baseScale = GetHandScale();
        
        if (cardCount == 0) return layouts;
        
        // Berechne den Spacing basierend auf der tatsächlichen Kartenbreite
        float actualSpacing = cardSpacing;
        if (preserveAspectRatio && cardCount > 0)
        {
            // Hole die erste Karte für Größenreferenz
            var firstCard = cachedHandCards[0];
            if (firstCard != null)
            {
                var cardRect = GetCachedRectTransform(firstCard);
                if (cardRect != null && cardRect.sizeDelta.y > 0) // Check für gültige Höhe
                {
                    // Berechne die skalierte Breite basierend auf der Höhe
                    float aspectRatio = cardRect.sizeDelta.x / cardRect.sizeDelta.y;
                    float scaledWidth = targetCardHeight * aspectRatio * baseScale;
                    actualSpacing = scaledWidth + 20f; // 20 Pixel Abstand zwischen Karten
                }
            }
        }
        
        float totalWidth = (cardCount - 1) * actualSpacing;
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
            
            float xPos = startX + (i * actualSpacing);
            var targetPos = new Vector3(xPos, 0, 0);
            
            // Berechne Scale basierend auf Aspect Ratio
            Vector3 targetScale;
            if (preserveAspectRatio)
            {
                // Berechne Scale um die Zielhöhe zu erreichen
                float currentHeight = rectTransform.sizeDelta.y;
                if (currentHeight > 0) // Verhindere Division durch 0
                {
                    float heightScale = targetCardHeight / currentHeight;
                    targetScale = Vector3.one * heightScale * baseScale;
                }
                else
                {
                    // Fallback wenn Höhe 0 ist
                    targetScale = Vector3.one * baseScale;
                    Debug.LogWarning($"[HandLayoutManager] Card {card.name} has zero height, using default scale");
                }
            }
            else
            {
                // Standard uniform scaling
                targetScale = Vector3.one * baseScale;
            }
            
            // Sicherheitscheck für ungültige Scale-Werte
            if (float.IsInfinity(targetScale.x) || float.IsInfinity(targetScale.y) || float.IsInfinity(targetScale.z) ||
                float.IsNaN(targetScale.x) || float.IsNaN(targetScale.y) || float.IsNaN(targetScale.z))
            {
                Debug.LogError($"[HandLayoutManager] Invalid scale calculated for {card.name}, using default");
                targetScale = Vector3.one * baseScale;
            }
            
            layouts.Add(new CardLayout(rectTransform, targetPos, targetScale));
        }
        
        return layouts;
    }
    
    private IEnumerator AnimateToLayout(List<CardLayout> targetLayouts)
    {
        float elapsedTime = 0;
        
        // Store start values
        var startLayouts = targetLayouts.Select(layout => new CardLayout(
            layout.rectTransform,
            layout.rectTransform.localPosition,
            layout.rectTransform.localScale
        )).ToList();
        
        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = elapsedTime / animationDuration;
            float curveValue = animationCurve.Evaluate(normalizedTime);
            
            for (int i = 0; i < targetLayouts.Count; i++)
            {
                var target = targetLayouts[i];
                var start = startLayouts[i];
                
                if (target.rectTransform == null) continue;
                
                target.rectTransform.localPosition = Vector3.Lerp(
                    start.targetPosition,
                    target.targetPosition,
                    curveValue
                );
                
                target.rectTransform.localScale = Vector3.Lerp(
                    start.targetScale,
                    target.targetScale,
                    curveValue
                );
            }
            
            yield return null;
        }
        
        // Ensure final positions
        foreach (var layout in targetLayouts)
        {
            if (layout.rectTransform != null)
            {
                layout.rectTransform.localPosition = layout.targetPosition;
                layout.rectTransform.localScale = layout.targetScale;
            }
        }
        
        _currentAnimation = null;
    }
    
    // Debug visualization
    public void LogCardSizes()
    {
        Debug.Log($"[HandLayoutManager] Card sizes in hand:");
        foreach (var card in cachedHandCards)
        {
            if (card != null)
            {
                var rect = card.GetComponent<RectTransform>();
                if (rect != null)
                {
                    float aspectRatio = rect.sizeDelta.x / rect.sizeDelta.y;
                    Debug.Log($"  - {card.name}: Size {rect.sizeDelta}, Aspect Ratio: {aspectRatio:F2}");
                }
            }
        }
    }
    
#if UNITY_EDITOR
    [ContextMenu("Log Card Sizes")]
    public void DebugLogCardSizes()
    {
        LogCardSizes();
    }
    
    [ContextMenu("Force Layout Update")]
    public void DebugForceLayout()
    {
        UpdateLayout();
    }
#endif
    
    private void OnDestroy()
    {
        if (_currentAnimation != null)
        {
            StopCoroutine(_currentAnimation);
        }
        cardRectTransforms.Clear();
        cachedHandCards.Clear();
    }
    
    // Helper class for layout data
    private class CardLayout
    {
        public RectTransform rectTransform;
        public Vector3 targetPosition;
        public Vector3 targetScale;
        
        public CardLayout(RectTransform rt, Vector3 pos, Vector3 scale)
        {
            rectTransform = rt;
            targetPosition = pos;
            targetScale = scale;
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
}