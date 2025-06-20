using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

[RequireComponent(typeof(HorizontalLayoutGroup))]
[RequireComponent(typeof(RectTransform))]
public class HandLayoutManager : SingletonBehaviour<HandLayoutManager>, IGameManager
{
    [Header("Layout Settings")]
    [SerializeField] private float cardSpacing = 20f;
    [SerializeField] private float handScale = 0.75f;
    
    [Header("Layout Group Settings")]
    [SerializeField] private bool childForceExpandWidth = false;
    [SerializeField] private bool childForceExpandHeight = false;
    [SerializeField] private bool childControlWidth = false;
    [SerializeField] private bool childControlHeight = false;
    [SerializeField] private bool childScaleWidth = false;
    [SerializeField] private bool childScaleHeight = false;
    [SerializeField] private TextAnchor childAlignment = TextAnchor.MiddleCenter;
    
    [Header("Padding")]
    [SerializeField] private int paddingLeft = 0;
    [SerializeField] private int paddingRight = 0;
    [SerializeField] private int paddingTop = 0;
    [SerializeField] private int paddingBottom = 0;
    
    // Components
    private HorizontalLayoutGroup _layoutGroup;
    private RectTransform _rectTransform;
    
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
    }
    
    private void ConfigureLayoutGroup()
    {
        if (_layoutGroup == null) return;
        
        // Configure spacing
        _layoutGroup.spacing = cardSpacing;
        
        // Configure padding
        _layoutGroup.padding = new RectOffset(paddingLeft, paddingRight, paddingTop, paddingBottom);
        
        // Configure child controls
        _layoutGroup.childForceExpandWidth = childForceExpandWidth;
        _layoutGroup.childForceExpandHeight = childForceExpandHeight;
        _layoutGroup.childControlWidth = childControlWidth;
        _layoutGroup.childControlHeight = childControlHeight;
        _layoutGroup.childScaleWidth = childScaleWidth;
        _layoutGroup.childScaleHeight = childScaleHeight;
        
        // Configure alignment
        _layoutGroup.childAlignment = childAlignment;
        
        Debug.Log("[HandLayoutManager] HorizontalLayoutGroup configured");
    }
    
    private void OnEnable()
    {
        // Listen to CardManager events
        if (CardManager.HasInstance)
        {
            CardManager.OnHandUpdated += OnHandUpdated;
            CardManager.OnCardSpawned += OnCardSpawned;
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
            CardManager.OnCardSpawned -= OnCardSpawned;
        }
        CardManager.OnCardManagerInitialized -= SubscribeToCardManager;
    }
    
    private void SubscribeToCardManager()
    {
        if (CardManager.HasInstance)
        {
            CardManager.OnHandUpdated += OnHandUpdated;
            CardManager.OnCardSpawned += OnCardSpawned;
        }
    }
    
    private void OnHandUpdated(List<Card> handCards)
    {
        // HorizontalLayoutGroup handles positioning automatically
        // We just need to ensure scale is correct
        UpdateCardScales();
    }
    
    private void OnCardSpawned(Card card)
    {
        // When a card is spawned and added to hand, ensure its scale is correct
        if (card != null && card.transform.parent == transform)
        {
            SetCardScale(card);
        }
    }
    
    // Public API
    public void UpdateLayout()
    {
        // Force the layout group to recalculate
        if (_layoutGroup != null)
        {
            _layoutGroup.CalculateLayoutInputHorizontal();
            _layoutGroup.SetLayoutHorizontal();
            
            // Update card scales
            UpdateCardScales();
        }
    }
    
    public void ForceImmediateLayout()
    {
        // Force immediate layout rebuild
        LayoutRebuilder.ForceRebuildLayoutImmediate(_rectTransform);
        UpdateCardScales();
    }
    
    private void UpdateCardScales()
    {
        // Apply scale to all child cards
        foreach (Transform child in transform)
        {
            if (child.TryGetComponent<Card>(out Card card))
            {
                SetCardScale(card);
            }
        }
    }
    
    private void SetCardScale(Card card)
    {
        if (card != null)
        {
            var rectTransform = card.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.localScale = Vector3.one * handScale;
            }
        }
    }
    
    // Configuration methods
    public void SetSpacing(float spacing)
    {
        cardSpacing = spacing;
        if (_layoutGroup != null)
        {
            _layoutGroup.spacing = spacing;
            UpdateLayout();
        }
    }
    
    public void SetHandScale(float scale)
    {
        handScale = scale;
        UpdateCardScales();
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
            UpdateLayout();
        }
    }
    
    public void SetChildAlignment(TextAnchor alignment)
    {
        childAlignment = alignment;
        if (_layoutGroup != null)
        {
            _layoutGroup.childAlignment = alignment;
            UpdateLayout();
        }
    }
    
    // Cleanup method for CardManager
    public void CleanupCardReference(Card card)
    {
        // Nothing special needed - HorizontalLayoutGroup handles everything
        // This method exists for compatibility
    }
    
#if UNITY_EDITOR
    [ContextMenu("Force Layout Update")]
    public void DebugForceLayout()
    {
        UpdateLayout();
    }
    
    [ContextMenu("Log Layout Settings")]
    public void DebugLogSettings()
    {
        if (_layoutGroup != null)
        {
            Debug.Log($"[HandLayoutManager] Layout Settings:");
            Debug.Log($"  Spacing: {_layoutGroup.spacing}");
            Debug.Log($"  Padding: L={_layoutGroup.padding.left}, R={_layoutGroup.padding.right}, T={_layoutGroup.padding.top}, B={_layoutGroup.padding.bottom}");
            Debug.Log($"  Child Alignment: {_layoutGroup.childAlignment}");
            Debug.Log($"  Force Expand Width: {_layoutGroup.childForceExpandWidth}");
            Debug.Log($"  Force Expand Height: {_layoutGroup.childForceExpandHeight}");
            Debug.Log($"  Hand Scale: {handScale}");
            Debug.Log($"  Child Count: {transform.childCount}");
        }
    }
    
    private void OnValidate()
    {
        // Apply changes in editor
        if (_layoutGroup != null && Application.isPlaying)
        {
            ConfigureLayoutGroup();
            UpdateLayout();
        }
    }
#endif
}