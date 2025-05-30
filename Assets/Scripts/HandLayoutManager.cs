using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class HandLayoutManager : MonoBehaviour
{
    public static HandLayoutManager Instance { get; private set; }
    
    [Header("Layout Settings")]
    [SerializeField] private float cardSpacing = 120f;
    [SerializeField] private float arcHeight = 50f;
    [SerializeField] private float maxRotationAngle = 15f;
    
    [Header("Animation")]
    [SerializeField] private float animationDuration = 0.5f;
    [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Scale Settings")]
    [SerializeField] private Vector3 baseCardScale = Vector3.one;
    [SerializeField] private bool enableDynamicScaling = false;
    [SerializeField] private float minScaleMultiplier = 0.8f;
    [SerializeField] private int maxCardsForFullScale = 5;
    
    private RectTransform rectTransform;
    private bool isAnimating = false;
    private List<Card> cachedHandCards = new List<Card>();
    private HashSet<DragObject> draggingCards = new HashSet<DragObject>();
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            rectTransform = GetComponent<RectTransform>();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    public void UpdateLayout()
    {
        if (isAnimating) return;
        
        var handCards = CardManager.Instance?.GetHandCards() ?? new List<Card>();
        
        if (HasHandChanged(handCards))
        {
            cachedHandCards = new List<Card>(handCards);
            StartCoroutine(AnimateCardsToPositions(handCards));
        }
    }
    
    public void SetCardDragging(DragObject dragObject, bool isDragging)
    {
        if (isDragging)
            draggingCards.Add(dragObject);
        else
            draggingCards.Remove(dragObject);
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
    
    private IEnumerator AnimateCardsToPositions(List<Card> handCards)
    {
        if (handCards.Count == 0) yield break;
        
        isAnimating = true;
        
        var layoutData = CalculateLayoutData(handCards);
        
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
    
    private struct CardLayoutData
    {
        public Card card;
        public RectTransform rectTransform;
        public Vector3 startPosition;
        public Quaternion startRotation;
        public Vector3 startScale;
        public Vector3 targetPosition;
        public Quaternion targetRotation;
        public Vector3 targetScale;
        public bool isDragging;
        
        public CardLayoutData(Card card, RectTransform rect, bool dragging)
        {
            this.card = card;
            this.rectTransform = rect;
            this.startPosition = rect.localPosition;
            this.startRotation = rect.localRotation;
            this.startScale = rect.localScale;
            this.targetPosition = Vector3.zero;
            this.targetRotation = Quaternion.identity;
            this.targetScale = Vector3.one;
            this.isDragging = dragging;
        }
    }
    
    private List<CardLayoutData> CalculateLayoutData(List<Card> handCards)
    {
        var layoutData = new List<CardLayoutData>();
        int cardCount = handCards.Count;
        
        Vector3 calculatedScale = CalculateCardScale(cardCount);
        float totalWidth = (cardCount - 1) * cardSpacing;
        float startX = -totalWidth * 0.5f;
        
        for (int i = 0; i < cardCount; i++)
        {
            Card card = handCards[i];
            if (card == null) continue;
            
            RectTransform cardRect = card.GetComponent<RectTransform>();
            if (cardRect == null) continue;
            
            DragObject dragObj = card.GetComponent<DragObject>();
            bool isDragging = dragObj != null && draggingCards.Contains(dragObj);
            
            var data = new CardLayoutData(card, cardRect, isDragging);
            
            // Calculate target position
            float normalizedPos = cardCount > 1 ? (float)i / (cardCount - 1) : 0.5f;
            float xPos = startX + (i * cardSpacing);
            float yPos = Mathf.Sin(normalizedPos * Mathf.PI) * arcHeight;
            data.targetPosition = new Vector3(xPos, yPos, 0);
            
            // Calculate target rotation
            float rotation = (normalizedPos - 0.5f) * maxRotationAngle * 2f;
            data.targetRotation = Quaternion.Euler(0, 0, rotation);
            
            data.targetScale = calculatedScale;
            
            layoutData.Add(data);
        }
        
        return layoutData;
    }
    
    private Vector3 CalculateCardScale(int cardCount)
    {
        if (!enableDynamicScaling || cardCount <= maxCardsForFullScale)
            return baseCardScale;
        
        float scaleMultiplier = Mathf.Lerp(1f, minScaleMultiplier, 
            (float)(cardCount - maxCardsForFullScale) / (10 - maxCardsForFullScale));
        
        return baseCardScale * scaleMultiplier;
    }
    
    private void ApplyLayoutProgress(List<CardLayoutData> layoutData, float progress)
    {
        foreach (var data in layoutData)
        {
            if (data.rectTransform == null || data.isDragging) continue;
            
            data.rectTransform.localPosition = Vector3.Lerp(data.startPosition, data.targetPosition, progress);
            data.rectTransform.localRotation = Quaternion.Lerp(data.startRotation, data.targetRotation, progress);
            data.rectTransform.localScale = Vector3.Lerp(data.startScale, data.targetScale, progress);
        }
    }
    
    private void ApplyFinalLayout(List<CardLayoutData> layoutData)
    {
        foreach (var data in layoutData)
        {
            if (data.rectTransform == null || data.isDragging) continue;
            
            data.rectTransform.localPosition = data.targetPosition;
            data.rectTransform.localRotation = data.targetRotation;
            data.rectTransform.localScale = data.targetScale;
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
        if (handCards.Count == 0) return;
        
        var layoutData = CalculateLayoutData(handCards);
        ApplyFinalLayout(layoutData);
        
        cachedHandCards = new List<Card>(handCards);
    }
    
    // Public properties
    public float CardSpacing 
    { 
        get => cardSpacing; 
        set { cardSpacing = value; UpdateLayout(); }
    }
    
    public Vector3 BaseCardScale 
    { 
        get => baseCardScale; 
        set { baseCardScale = value; UpdateLayout(); }
    }
    
    public bool DynamicScaling 
    { 
        get => enableDynamicScaling; 
        set { enableDynamicScaling = value; UpdateLayout(); }
    }
    
    public int GetHandSize() => cachedHandCards.Count;
    public bool IsAnimating => isAnimating;
}