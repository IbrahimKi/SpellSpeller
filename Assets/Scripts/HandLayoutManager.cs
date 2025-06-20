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
    [SerializeField] private float handScale = 1f; // Geändert zu 1f für normale Größe
    [SerializeField] private Vector2 cardPreferredSize = new Vector2(120f, 180f); // Explizite Kartengröße
    
    [Header("Layout Optimization")]
    [SerializeField] private bool enableDynamicScaling = false;
    [SerializeField] private float maxHandWidth = 800f;
    [SerializeField] private bool enableHandScaling = false;
    [SerializeField] private float handScaleMultiplier = 0.8f;
    
    [Header("Layout Group Settings")]
    [SerializeField] private bool childForceExpandWidth = false;
    [SerializeField] private bool childForceExpandHeight = false;
    [SerializeField] private bool childControlWidth = true; // Wichtig für Kartengröße-Kontrolle
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
    private ContentSizeFitter _contentSizeFitter; // Für automatische Größenanpassung
    
    // Optimization
    private bool _layoutUpdatePending = false;
    private float _lastLayoutUpdate = 0f;
    private const float LAYOUT_UPDATE_THROTTLE = 0.016f; // ~60fps
    
    // State
    private bool _isReady = false;
    private List<Card> _trackedCards = new List<Card>();
    
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
        
        // ContentSizeFitter für automatische Container-Größe
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
        
        // KRITISCH: Layout Group Konfiguration für optimale Performance
        _layoutGroup.spacing = cardSpacing;
        _layoutGroup.padding = new RectOffset(paddingLeft, paddingRight, paddingTop, paddingBottom);
        
        // Child Control Settings - WICHTIG für Kartengröße
        _layoutGroup.childForceExpandWidth = childForceExpandWidth;
        _layoutGroup.childForceExpandHeight = childForceExpandHeight;
        _layoutGroup.childControlWidth = childControlWidth;
        _layoutGroup.childControlHeight = childControlHeight;
        _layoutGroup.childScaleWidth = childScaleWidth;
        _layoutGroup.childScaleHeight = childScaleHeight;
        _layoutGroup.childAlignment = childAlignment;
        
        Debug.Log("[HandLayoutManager] HorizontalLayoutGroup configured optimally");
    }
    
    private void OnEnable()
    {
        // Subscribe to CardManager events with proper error handling
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
            CardManager.OnCardDestroyed += OnCardDestroyed;
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
            CardManager.OnCardDestroyed -= OnCardDestroyed;
        }
        CardManager.OnCardManagerInitialized -= DelayedSubscribe;
    }
    
    private void DelayedSubscribe()
    {
        SubscribeToCardManager();
    }
    
    // EVENT HANDLERS - Optimiert für Performance
    private void OnHandUpdated(List<Card> handCards)
    {
        UpdateTrackedCards(handCards);
        RequestLayoutUpdate();
    }
    
    private void OnCardSpawned(Card card)
    {
        if (card != null && card.transform.parent == transform)
        {
            SetupNewCard(card);
            RequestLayoutUpdate();
        }
    }
    
    private void OnCardDestroyed(Card card)
    {
        _trackedCards.Remove(card);
        RequestLayoutUpdate();
    }
    
    // OPTIMIZED LAYOUT SYSTEM
    private void RequestLayoutUpdate()
    {
        if (_layoutUpdatePending) return;
        
        _layoutUpdatePending = true;
        StartCoroutine(DelayedLayoutUpdate());
    }
    
    private IEnumerator DelayedLayoutUpdate()
    {
        // Throttle updates für bessere Performance
        float timeSinceLastUpdate = Time.unscaledTime - _lastLayoutUpdate;
        if (timeSinceLastUpdate < LAYOUT_UPDATE_THROTTLE)
        {
            yield return new WaitForSecondsRealtime(LAYOUT_UPDATE_THROTTLE - timeSinceLastUpdate);
        }
        
        PerformLayoutUpdate();
        
        _layoutUpdatePending = false;
        _lastLayoutUpdate = Time.unscaledTime;
    }
    
    private void PerformLayoutUpdate()
    {
        if (_layoutGroup == null) return;
        
        // 1. Update card configurations FIRST
        ConfigureAllCards();
        
        // 2. Apply dynamic scaling if enabled
        if (enableDynamicScaling)
        {
            ApplyDynamicScaling();
        }
        
        // 3. Force layout recalculation
        LayoutRebuilder.ForceRebuildLayoutImmediate(_rectTransform);
        
        Debug.Log($"[HandLayoutManager] Layout updated - {_trackedCards.Count} cards");
    }
    
    // CARD CONFIGURATION - Das löst das Größenproblem!
    private void SetupNewCard(Card card)
    {
        if (card == null) return;
        
        var rectTransform = card.GetComponent<RectTransform>();
        if (rectTransform == null) return;
        
        // KRITISCH: Setze explizite Kartengröße
        var layoutElement = card.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = card.gameObject.AddComponent<LayoutElement>();
        }
        
        // LÖSUNG: Explizite Größe statt Scale
        layoutElement.preferredWidth = cardPreferredSize.x * handScale;
        layoutElement.preferredHeight = cardPreferredSize.y * handScale;
        layoutElement.flexibleWidth = 0f;
        layoutElement.flexibleHeight = 0f;
        
        // WICHTIG: Anchors für UI-Layout
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        
        // Reset Position und Scale
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.localScale = Vector3.one; // Keine Scale-Manipulation!
        
        // Track the card
        if (!_trackedCards.Contains(card))
        {
            _trackedCards.Add(card);
        }
        
        Debug.Log($"[HandLayoutManager] Card setup: {card.name} - Size: {layoutElement.preferredWidth}x{layoutElement.preferredHeight}");
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
    }
    
    // DYNAMIC SCALING - Optional für große Hands
    private void ApplyDynamicScaling()
    {
        if (!enableHandScaling || _trackedCards.Count == 0) return;
        
        float requiredWidth = CalculateRequiredWidth();
        float containerWidth = _rectTransform.rect.width;
        
        if (requiredWidth > maxHandWidth)
        {
            float scaleMultiplier = maxHandWidth / requiredWidth;
            scaleMultiplier = Mathf.Max(scaleMultiplier, 0.5f); // Minimum scale
            
            ApplyScaleToAllCards(scaleMultiplier * handScaleMultiplier);
            
            Debug.Log($"[HandLayoutManager] Applied dynamic scaling: {scaleMultiplier:F2}");
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
        RequestLayoutUpdate();
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
            RequestLayoutUpdate();
        }
    }
    
    public void SetHandScale(float scale)
    {
        handScale = Mathf.Max(0.1f, scale);
        RequestLayoutUpdate();
    }
    
    public void SetCardSize(Vector2 size)
    {
        cardPreferredSize = size;
        RequestLayoutUpdate();
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
            RequestLayoutUpdate();
        }
    }
    
    public void CleanupCardReference(Card card)
    {
        _trackedCards.Remove(card);
    }
    
    // PERFORMANCE MONITORING
    private void Update()
    {
        // Occasional validation in editor
        if (Application.isEditor && Time.frameCount % 60 == 0)
        {
            ValidateCardSetup();
        }
    }
    
    private void ValidateCardSetup()
    {
        int childCount = transform.childCount;
        int trackedCount = _trackedCards.Count;
        
        if (childCount != trackedCount)
        {
            Debug.LogWarning($"[HandLayoutManager] Mismatch - Children: {childCount}, Tracked: {trackedCount}");
            // Auto-fix
            RequestLayoutUpdate();
        }
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
        
        foreach (Transform child in transform)
        {
            var layoutElement = child.GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                Debug.Log($"    {child.name}: {layoutElement.preferredWidth}x{layoutElement.preferredHeight}");
            }
        }
    }
    
    private void OnValidate()
    {
        if (Application.isPlaying && _layoutGroup != null)
        {
            ConfigureLayoutGroup();
            RequestLayoutUpdate();
        }
        
        // Clamp values
        handScale = Mathf.Max(0.1f, handScale);
        cardSpacing = Mathf.Max(0f, cardSpacing);
        cardPreferredSize.x = Mathf.Max(50f, cardPreferredSize.x);
        cardPreferredSize.y = Mathf.Max(50f, cardPreferredSize.y);
    }
#endif
}