using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Linq; // CRITICAL: LINQ für Any/All operations
using GameCore.Enums;
using GameCore.Data;

public class CardSlotBehaviour : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Slot Configuration")]
    [SerializeField] private CardSlotAsset slotAsset;
    [SerializeField] private int slotIndex = 0;
    
    [Header("UI Components")]
    [SerializeField] private Image slotImage;
    [SerializeField] private TextMeshProUGUI slotNumberText;
    [SerializeField] private TextMeshProUGUI slotNameText;
    [SerializeField] private GameObject effectIndicator;
    [SerializeField] private RectTransform cardContainer;
    
    [Header("Visual Feedback")]
    [SerializeField] private GameObject hoverEffect;
    [SerializeField] private GameObject disabledOverlay;
    [SerializeField] private ParticleSystem slotEffectParticles;
    
    // State
    private Card _occupyingCard;
    private bool _isHighlighted = false;
    private bool _isEnabled = true;
    private Coroutine _autoPlayCoroutine;
    
    // Properties
    public CardSlotAsset SlotAsset => slotAsset;
    public int SlotIndex => slotIndex;
    public bool IsEmpty => _occupyingCard == null;
    public bool IsFilled => _occupyingCard != null;
    public Card OccupyingCard => _occupyingCard;
    public bool IsEnabled => _isEnabled && (slotAsset?.isActive ?? true);
    
    // Events
    public static event System.Action<CardSlotBehaviour, Card> OnCardPlacedInSlot;
    public static event System.Action<CardSlotBehaviour, Card> OnCardRemovedFromSlot;
    public static event System.Action<CardSlotBehaviour, Card> OnCardPlayedFromSlot;
    public static event System.Action<CardSlotBehaviour> OnSlotStateChanged;
    
    private void Awake()
    {
        InitializeComponents();
    }
    
    private void Start()
    {
        InitializeSlot();
        UpdateVisuals();
    }
    
    private void InitializeComponents()
    {
        if (slotImage == null)
            slotImage = GetComponent<Image>();
            
        if (cardContainer == null)
        {
            var containerGO = new GameObject("Card Container");
            cardContainer = containerGO.AddComponent<RectTransform>();
            cardContainer.SetParent(transform, false);
            cardContainer.anchorMin = Vector2.zero;
            cardContainer.anchorMax = Vector2.one;
            cardContainer.offsetMin = Vector2.zero;
            cardContainer.offsetMax = Vector2.zero;
        }
    }
    
    private void InitializeSlot()
    {
        if (slotAsset == null)
        {
            Debug.LogWarning($"[CardSlotBehaviour] No SlotAsset assigned to slot {slotIndex}");
            return;
        }
        
        // Set up UI text
        if (slotNumberText != null)
            slotNumberText.text = (slotIndex + 1).ToString();
            
        if (slotNameText != null)
            slotNameText.text = slotAsset.slotName;
        
        // Enable/disable based on asset
        SetEnabled(slotAsset.isActive);
        
        Debug.Log($"[CardSlotBehaviour] Initialized slot {slotIndex}: {slotAsset.slotName}");
    }
    
    // === SLOT CONFIGURATION ===
    
    public void SetSlotAsset(CardSlotAsset newAsset)
    {
        slotAsset = newAsset;
        InitializeSlot();
        UpdateVisuals();
    }
    
    public void SetSlotIndex(int index)
    {
        slotIndex = index;
        if (slotNumberText != null)
            slotNumberText.text = (index + 1).ToString();
    }
    
    public void SetEnabled(bool enabled)
    {
        _isEnabled = enabled;
        
        if (disabledOverlay != null)
            disabledOverlay.SetActive(!IsEnabled);
            
        UpdateVisuals();
    }
    
    // === CARD MANAGEMENT ===
    
    public bool TryPlaceCard(Card card)
    {
        if (!CanAcceptCard(card))
        {
            Debug.LogWarning($"[CardSlotBehaviour] Cannot place card {card?.GetCardName()} in slot {slotIndex}");
            return false;
        }
        
        PlaceCard(card);
        return true;
    }
    
    public bool CanAcceptCard(Card card)
    {
        if (!IsEnabled || IsFilled || card == null) return false;
        
        return slotAsset?.CanAcceptCard(card) ?? true;
    }
    
    private void PlaceCard(Card card)
    {
        if (card == null) return;
        
        // Remove from previous location
        RemoveCard(false);
        
        // Set new card
        _occupyingCard = card;
        
        // Position card in slot
        card.transform.SetParent(cardContainer, false);
        var cardRect = card.GetComponent<RectTransform>();
        if (cardRect != null)
        {
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.localScale = Vector3.one * 0.8f; // Scale down to fit in slot
        }
        
        // Process slot effects
        slotAsset?.ProcessEffectsOnCardPlaced(card, this);
        
        // Auto-play if configured
        if (slotAsset?.autoPlayWhenFilled == true)
        {
            _autoPlayCoroutine = StartCoroutine(AutoPlayAfterDelay(slotAsset.autoPlayDelay));
        }
        
        UpdateVisuals();
        PlaySlotEffect();
        
        OnCardPlacedInSlot?.Invoke(this, card);
        OnSlotStateChanged?.Invoke(this);
        
        Debug.Log($"[CardSlotBehaviour] Card {card.GetCardName()} placed in slot {slotIndex}");
    }
    
    public Card RemoveCard(bool triggerEffects = true)
    {
        if (IsEmpty) return null;
        
        Card removedCard = _occupyingCard;
        _occupyingCard = null;
        
        // Stop auto-play if running
        if (_autoPlayCoroutine != null)
        {
            StopCoroutine(_autoPlayCoroutine);
            _autoPlayCoroutine = null;
        }
        
        // Process slot effects
        if (triggerEffects)
            slotAsset?.ProcessEffectsOnCardRemoved(removedCard, this);
        
        UpdateVisuals();
        
        OnCardRemovedFromSlot?.Invoke(this, removedCard);
        OnSlotStateChanged?.Invoke(this);
        
        Debug.Log($"[CardSlotBehaviour] Card {removedCard.GetCardName()} removed from slot {slotIndex}");
        
        return removedCard;
    }
    
    public bool PlayCard()
    {
        if (IsEmpty || !IsEnabled) return false;
        
        Card cardToPlay = _occupyingCard;
        
        // Process slot effects before playing
        slotAsset?.ProcessEffectsOnPlay(cardToPlay, this);
        
        // Remove card from slot if consumeOnPlay is true
        if (slotAsset?.consumeOnPlay != false)
        {
            RemoveCard(false); // Don't trigger removal effects since we're playing
        }
        
        // Play card via SpellcastManager using Extensions
        bool playSuccess = CoreExtensions.TryWithManager<SpellcastManager>(this, sm =>
        {
            var cardList = new System.Collections.Generic.List<Card> { cardToPlay };
            sm.ProcessCardPlay(cardList);
        });
        
        if (playSuccess)
        {
            OnCardPlayedFromSlot?.Invoke(this, cardToPlay);
            PlaySlotEffect();
            
            Debug.Log($"Card {cardToPlay.GetCardName()} played from slot {slotIndex}");
        }
        else
        {
            Debug.LogError($"Failed to play card {cardToPlay.GetCardName()} from slot {slotIndex}");
        }
        
        return playSuccess;
    }
    
    // === VISUAL UPDATES ===
    
    private void UpdateVisuals()
    {
        if (slotImage == null || slotAsset == null) return;
        
        Color targetColor;
        
        if (!IsEnabled)
        {
            targetColor = slotAsset.disabledColor;
        }
        else if (_isHighlighted)
        {
            targetColor = slotAsset.highlightColor;
        }
        else if (IsFilled)
        {
            targetColor = slotAsset.filledSlotColor;
        }
        else
        {
            targetColor = slotAsset.emptySlotColor;
        }
        
        slotImage.color = targetColor;
        
        // Update effect indicator
        if (effectIndicator != null)
        {
            bool hasEffects = slotAsset.slotEffects?.Count > 0;
            effectIndicator.SetActive(hasEffects && IsEnabled);
        }
    }
    
    private void PlaySlotEffect()
    {
        if (slotEffectParticles != null)
        {
            slotEffectParticles.Play();
        }
    }
    
    // === UI EVENT HANDLERS ===
    
    public void OnDrop(PointerEventData eventData)
    {
        if (!IsEnabled) return;
        
        var draggedCard = eventData.pointerDrag?.GetComponent<Card>();
        if (draggedCard != null)
        {
            TryPlaceCard(draggedCard);
        }
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!IsEnabled) return;
        
        _isHighlighted = true;
        UpdateVisuals();
        
        if (hoverEffect != null)
            hoverEffect.SetActive(true);
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        _isHighlighted = false;
        UpdateVisuals();
        
        if (hoverEffect != null)
            hoverEffect.SetActive(false);
    }
    
    // === AUTO-PLAY FUNCTIONALITY ===
    
    private IEnumerator AutoPlayAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (IsFilled && IsEnabled)
        {
            PlayCard();
        }
        
        _autoPlayCoroutine = null;
    }
    
    // === UTILITY METHODS ===
    
    public string GetSlotInfo()
    {
        return $"Slot {slotIndex + 1}: {(IsFilled ? _occupyingCard.GetCardName() : "Empty")} " +
               $"(Enabled: {IsEnabled}, Effects: {slotAsset?.slotEffects?.Count ?? 0})";
    }
    
    // FIXED: Hinzugefügter System.Linq using macht .Any() verfügbar
    public bool HasEffectOfType(SlotEffectType effectType)
    {
        return slotAsset?.slotEffects?.Any(e => e.effectType == effectType) ?? false;
    }
    
    public void ForcePlayCard()
    {
        if (IsFilled)
        {
            PlayCard();
        }
    }
    
    // === PERSISTENCE (for cards that persist between turns) ===
    
    public void OnTurnEnd()
    {
        if (IsFilled && slotAsset != null)
        {
            // Process turn-end effects - FIXED: LINQ verfügbar
            foreach (var effect in slotAsset.slotEffects.Where(e => e.triggerEvent == SlotTriggerEvent.OnTurnEnd))
            {
                effect.ApplyEffect(_occupyingCard, this);
            }
            
            // Remove card if it doesn't persist
            if (!slotAsset.persistCardBetweenTurns)
            {
                RemoveCard();
            }
        }
    }
    
    public void OnTurnStart()
    {
        if (IsFilled && slotAsset != null)
        {
            // Process turn-start effects - FIXED: LINQ verfügbar
            foreach (var effect in slotAsset.slotEffects.Where(e => e.triggerEvent == SlotTriggerEvent.OnTurnStart))
            {
                effect.ApplyEffect(_occupyingCard, this);
            }
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Debug Slot Info")]
    private void DebugSlotInfo()
    {
        Debug.Log($"[CardSlotBehaviour] {GetSlotInfo()}");
        
        if (slotAsset != null)
        {
            Debug.Log($"  Asset: {slotAsset.name}");
            Debug.Log($"  Effects: {slotAsset.slotEffects?.Count ?? 0}");
            Debug.Log($"  Auto-play: {slotAsset.autoPlayWhenFilled}");
            Debug.Log($"  Persist: {slotAsset.persistCardBetweenTurns}");
        }
    }
    
    [ContextMenu("Test Place Random Card")]
    private void TestPlaceCard()
    {
        CoreExtensions.TryWithManager<CardManager>(this, cm =>
        {
            var handCards = cm.GetHandCards();
            if (handCards.Count > 0)
            {
                var randomCard = handCards[Random.Range(0, handCards.Count)];
                TryPlaceCard(randomCard);
            }
        });
    }
    
    [ContextMenu("Force Play Card")]
    private void TestPlayCard()
    {
        ForcePlayCard();
    }
#endif
}