using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

public class HandLayoutManager : MonoBehaviour
{
    [Header("Layout Settings")]
    [SerializeField] private float cardSpacing = 120f;
    [SerializeField] private AnimationCurve cardArcCurve = AnimationCurve.Linear(0, 0, 1, 0);
    [SerializeField] private float arcHeight = 50f;
    [SerializeField] private float maxRotationAngle = 15f; // Max rotation for arc effect
    
    [Header("Animation Settings")]
    [SerializeField] private bool useAnimation = true;
    [SerializeField] private float animationDuration = 0.5f;
    [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private float bounceScale = 0.1f; // Scale bounce effect during animation
    
    [Header("Auto-Update Settings")]
    [SerializeField] private bool autoDetectCards = true;
    [SerializeField] private bool updateOnChildChange = true;
    [SerializeField] private float updateDelay = 0.1f; // Delay before updating after child changes
    
    [Header("Constraints")]
    [SerializeField] private bool constrainToParent = true;
    [SerializeField] private float minCardSpacing = 50f;
    [SerializeField] private float maxCardSpacing = 200f;
    
    // Runtime tracking
    private List<RectTransform> cardTransforms = new List<RectTransform>();
    private List<Coroutine> activeAnimations = new List<Coroutine>();
    private RectTransform rectTransform;
    private bool isArranging = false;
    
    // Child change detection
    private int lastChildCount = 0;
    private Coroutine updateCoroutine;
    
    // Events
    public System.Action<int> OnCardCountChanged;
    public System.Action OnLayoutComplete;
    
    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }
    
    private void Start()
    {
        // Initial detection and arrangement
        if (autoDetectCards)
        {
            DetectCards();
            ArrangeCards();
        }
        
        lastChildCount = transform.childCount;
    }
    
    private void Update()
    {
        // Check for child count changes if auto-update is enabled
        if (updateOnChildChange && transform.childCount != lastChildCount)
        {
            lastChildCount = transform.childCount;
            ScheduleUpdate();
        }
    }
    
    /// <summary>
    /// Schedule a delayed update to avoid multiple rapid updates
    /// </summary>
    private void ScheduleUpdate()
    {
        if (updateCoroutine != null)
        {
            StopCoroutine(updateCoroutine);
        }
        updateCoroutine = StartCoroutine(DelayedUpdate());
    }
    
    private IEnumerator DelayedUpdate()
    {
        yield return new WaitForSeconds(updateDelay);
        DetectCards();
        ArrangeCards();
        updateCoroutine = null;
    }
    
    /// <summary>
    /// Detect all card RectTransforms in children
    /// </summary>
    public void DetectCards()
    {
        cardTransforms.Clear();
        
        // Get all child RectTransforms that should be arranged
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            RectTransform childRect = child.GetComponent<RectTransform>();
            
            if (childRect != null && child.gameObject.activeInHierarchy)
            {
                // Only include objects that have Card components or are marked for layout
                Card cardComponent = child.GetComponent<Card>();
                HandLayoutElement layoutElement = child.GetComponent<HandLayoutElement>();
                
                if (cardComponent != null || layoutElement != null)
                {
                    cardTransforms.Add(childRect);
                }
            }
        }
        
        // Remove null references
        cardTransforms.RemoveAll(rt => rt == null);
        
        OnCardCountChanged?.Invoke(cardTransforms.Count);
        
        Debug.Log($"[HandLayoutManager] Detected {cardTransforms.Count} cards in {gameObject.name}");
    }
    
    /// <summary>
    /// Arrange all detected cards with spacing and arc
    /// </summary>
    public void ArrangeCards()
    {
        if (isArranging || cardTransforms.Count == 0) return;
        
        isArranging = true;
        
        // Stop any active animations
        StopAllAnimations();
        
        // Calculate optimal spacing
        float optimalSpacing = CalculateOptimalSpacing();
        
        // Calculate positions for all cards
        List<CardLayoutData> layoutData = CalculateCardPositions(optimalSpacing);
        
        // Apply positions (with or without animation)
        if (useAnimation && Application.isPlaying)
        {
            StartCoroutine(AnimateCardsToPositions(layoutData));
        }
        else
        {
            ApplyCardPositionsImmediate(layoutData);
            isArranging = false;
            OnLayoutComplete?.Invoke();
        }
    }
    
    /// <summary>
    /// Calculate optimal spacing based on available space and card count
    /// </summary>
    private float CalculateOptimalSpacing()
    {
        if (cardTransforms.Count <= 1) return cardSpacing;
        
        if (constrainToParent && rectTransform != null)
        {
            float availableWidth = rectTransform.rect.width;
            float totalRequiredWidth = (cardTransforms.Count - 1) * cardSpacing;
            
            // If cards would overflow, reduce spacing
            if (totalRequiredWidth > availableWidth * 0.9f) // Leave 10% margin
            {
                float maxPossibleSpacing = (availableWidth * 0.9f) / (cardTransforms.Count - 1);
                return Mathf.Clamp(maxPossibleSpacing, minCardSpacing, cardSpacing);
            }
        }
        
        return Mathf.Clamp(cardSpacing, minCardSpacing, maxCardSpacing);
    }
    
    /// <summary>
    /// Calculate positions and rotations for all cards
    /// </summary>
    private List<CardLayoutData> CalculateCardPositions(float spacing)
    {
        List<CardLayoutData> layoutData = new List<CardLayoutData>();
        int cardCount = cardTransforms.Count;
        
        if (cardCount == 0) return layoutData;
        
        // Calculate total width and starting position
        float totalWidth = (cardCount - 1) * spacing;
        float startX = -totalWidth * 0.5f;
        
        for (int i = 0; i < cardCount; i++)
        {
            if (cardTransforms[i] == null) continue;
            
            // Calculate normalized position (0 to 1)
            float normalizedPosition = cardCount > 1 ? (float)i / (cardCount - 1) : 0.5f;
            
            // Calculate position
            float xPos = startX + (i * spacing);
            float yPos = cardArcCurve.Evaluate(normalizedPosition) * arcHeight;
            Vector3 targetPosition = new Vector3(xPos, yPos, 0);
            
            // Calculate rotation for arc effect
            float rotation = (normalizedPosition - 0.5f) * maxRotationAngle * 2f;
            Quaternion targetRotation = Quaternion.Euler(0, 0, rotation);
            
            // Store layout data
            CardLayoutData data = new CardLayoutData
            {
                rectTransform = cardTransforms[i],
                targetPosition = targetPosition,
                targetRotation = targetRotation,
                normalizedPosition = normalizedPosition,
                index = i
            };
            
            layoutData.Add(data);
        }
        
        return layoutData;
    }
    
    /// <summary>
    /// Apply positions immediately without animation
    /// </summary>
    private void ApplyCardPositionsImmediate(List<CardLayoutData> layoutData)
    {
        foreach (var data in layoutData)
        {
            if (data.rectTransform != null)
            {
                data.rectTransform.localPosition = data.targetPosition;
                data.rectTransform.localRotation = data.targetRotation;
                
                // Reset scale if it was modified
                data.rectTransform.localScale = Vector3.one;
            }
        }
    }
    
    /// <summary>
    /// Animate cards to their target positions
    /// </summary>
    private IEnumerator AnimateCardsToPositions(List<CardLayoutData> layoutData)
    {
        // Start all animations simultaneously
        foreach (var data in layoutData)
        {
            if (data.rectTransform != null)
            {
                Coroutine animation = StartCoroutine(AnimateSingleCard(data));
                activeAnimations.Add(animation);
            }
        }
        
        // Wait for animation duration
        yield return new WaitForSeconds(animationDuration);
        
        // Clean up
        activeAnimations.Clear();
        isArranging = false;
        OnLayoutComplete?.Invoke();
    }
    
    /// <summary>
    /// Animate a single card to its target position
    /// </summary>
    private IEnumerator AnimateSingleCard(CardLayoutData data)
    {
        RectTransform rt = data.rectTransform;
        Vector3 startPosition = rt.localPosition;
        Quaternion startRotation = rt.localRotation;
        Vector3 originalScale = rt.localScale;
        
        float elapsed = 0f;
        
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / animationDuration;
            float smoothProgress = animationCurve.Evaluate(progress);
            
            // Position and rotation interpolation
            rt.localPosition = Vector3.Lerp(startPosition, data.targetPosition, smoothProgress);
            rt.localRotation = Quaternion.Lerp(startRotation, data.targetRotation, smoothProgress);
            
            // Scale bounce effect
            if (bounceScale > 0)
            {
                float bounceEffect = 1f + Mathf.Sin(progress * Mathf.PI) * bounceScale;
                rt.localScale = originalScale * bounceEffect;
            }
            
            yield return null;
        }
        
        // Ensure final position is exact
        rt.localPosition = data.targetPosition;
        rt.localRotation = data.targetRotation;
        rt.localScale = originalScale;
    }
    
    /// <summary>
    /// Stop all active animations
    /// </summary>
    private void StopAllAnimations()
    {
        foreach (var animation in activeAnimations)
        {
            if (animation != null)
            {
                StopCoroutine(animation);
            }
        }
        activeAnimations.Clear();
    }
    
    /// <summary>
    /// Add a new card to the layout
    /// </summary>
    public void AddCard(RectTransform cardTransform)
    {
        if (cardTransform != null && !cardTransforms.Contains(cardTransform))
        {
            cardTransforms.Add(cardTransform);
            
            if (!autoDetectCards) // Only auto-arrange if not using auto-detection
            {
                ArrangeCards();
            }
        }
    }
    
    /// <summary>
    /// Remove a card from the layout
    /// </summary>
    public void RemoveCard(RectTransform cardTransform)
    {
        if (cardTransforms.Contains(cardTransform))
        {
            cardTransforms.Remove(cardTransform);
            
            if (!autoDetectCards) // Only auto-arrange if not using auto-detection
            {
                ArrangeCards();
            }
        }
    }
    
    /// <summary>
    /// Force immediate update of card detection and layout
    /// </summary>
    [ContextMenu("Update Layout")]
    public void UpdateLayout()
    {
        DetectCards();
        ArrangeCards();
    }
    
    /// <summary>
    /// Set new spacing and update layout
    /// </summary>
    public void SetCardSpacing(float newSpacing)
    {
        cardSpacing = Mathf.Clamp(newSpacing, minCardSpacing, maxCardSpacing);
        ArrangeCards();
    }
    
    /// <summary>
    /// Set new arc height and update layout
    /// </summary>
    public void SetArcHeight(float newHeight)
    {
        arcHeight = newHeight;
        ArrangeCards();
    }
    
    /// <summary>
    /// Get current card count
    /// </summary>
    public int GetCardCount()
    {
        return cardTransforms.Count;
    }
    
    /// <summary>
    /// Check if layout is currently animating
    /// </summary>
    public bool IsAnimating()
    {
        return isArranging || activeAnimations.Count > 0;
    }
    
    /// <summary>
    /// Get all card transforms currently managed
    /// </summary>
    public List<RectTransform> GetManagedCards()
    {
        return new List<RectTransform>(cardTransforms);
    }
    
    private void OnValidate()
    {
        // Clamp values in editor
        cardSpacing = Mathf.Max(0, cardSpacing);
        arcHeight = Mathf.Max(0, arcHeight);
        maxRotationAngle = Mathf.Clamp(maxRotationAngle, 0, 45);
        animationDuration = Mathf.Max(0.1f, animationDuration);
        minCardSpacing = Mathf.Max(10f, minCardSpacing);
        maxCardSpacing = Mathf.Max(minCardSpacing, maxCardSpacing);
        
        // Update layout in editor if playing
        if (Application.isPlaying && autoDetectCards)
        {
            UpdateLayout();
        }
    }
    
    private void OnDestroy()
    {
        StopAllAnimations();
        
        if (updateCoroutine != null)
        {
            StopCoroutine(updateCoroutine);
        }
    }
}

/// <summary>
/// Data structure for card layout calculations
/// </summary>
[System.Serializable]
public class CardLayoutData
{
    public RectTransform rectTransform;
    public Vector3 targetPosition;
    public Quaternion targetRotation;
    public float normalizedPosition;
    public int index;
}

/// <summary>
/// Optional component to mark objects for hand layout inclusion
/// </summary>
public class HandLayoutElement : MonoBehaviour
{
    [Header("Layout Settings")]
    [SerializeField] private bool includeInLayout = true;
    [SerializeField] private int layoutPriority = 0; // Higher priority = arranged first
    
    public bool IncludeInLayout => includeInLayout;
    public int LayoutPriority => layoutPriority;
    
    /// <summary>
    /// Toggle inclusion in layout
    /// </summary>
    public void SetIncludeInLayout(bool include)
    {
        includeInLayout = include;
        
        // Notify parent HandLayoutManager
        HandLayoutManager layoutManager = GetComponentInParent<HandLayoutManager>();
        if (layoutManager != null)
        {
            layoutManager.UpdateLayout();
        }
    }
}
