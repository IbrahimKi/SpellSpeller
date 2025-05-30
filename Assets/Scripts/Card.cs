using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using UnityEngine.EventSystems;

public enum CardState
{
    Idle,
    Selected,
    Dragging,
    Playing,
    Disabled
}

public class Card : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Card Data")]
    [SerializeField] private CardData cardData;
    [SerializeField] private bool isSelected = false;
    [SerializeField] private bool isInteractable = true;
    
    [Header("Visual Feedback")]
    [SerializeField] private Image cardBackground;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = Color.yellow;
    [SerializeField] private Color hoverColor = Color.gray;
    
    // Events
    public static event System.Action<Card> OnCardSelected;
    public static event System.Action<Card> OnCardDeselected;
    public static event System.Action<Card> OnCardHovered;
    public static event System.Action<Card> OnCardUnhovered;
    
    // Properties
    public CardData CardData => cardData;
    public bool IsSelected => isSelected;
    public bool IsInteractable => isInteractable;
    
    private void Awake()
    {
        if (cardBackground == null)
            cardBackground = GetComponent<Image>();
    }
    
    private void Start()
    {
        UpdateVisuals();
    }
    
    public void SetCardData(CardData data)
    {
        cardData = data;
        UpdateCardDisplay();
    }
    
    private void UpdateCardDisplay()
    {
        // Hier würden Sie die Karteninformationen aktualisieren
        // z.B. Text, Bilder, etc. basierend auf cardData
        if (cardData != null)
        {
            // Beispiel: gameObject.name = cardData.cardName;
        }
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isInteractable) return;
        
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            ToggleSelection();
        }
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isInteractable) return;
        
        if (cardBackground != null && !isSelected)
            cardBackground.color = hoverColor;
            
        OnCardHovered?.Invoke(this);
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isInteractable) return;
        
        UpdateVisuals();
        OnCardUnhovered?.Invoke(this);
    }
    
    public void ToggleSelection()
    {
        if (!isInteractable) return;
        
        if (isSelected)
            Deselect();
        else
            Select();
    }
    
    public void Select()
    {
        if (!isInteractable || isSelected) return;
        
        isSelected = true;
        UpdateVisuals();
        OnCardSelected?.Invoke(this);
    }
    
    public void Deselect()
    {
        if (!isSelected) return;
        
        isSelected = false;
        UpdateVisuals();
        OnCardDeselected?.Invoke(this);
    }
    
    public void ForceDeselect()
    {
        isSelected = false;
        UpdateVisuals();
    }
    
    private void UpdateVisuals()
    {
        if (cardBackground == null) return;
        
        if (isSelected)
            cardBackground.color = selectedColor;
        else
            cardBackground.color = normalColor;
    }
    
    public void SetInteractable(bool interactable)
    {
        isInteractable = interactable;
        
        // Optional: Visual feedback für nicht-interaktive Karten
        if (cardBackground != null)
        {
            var color = cardBackground.color;
            color.a = interactable ? 1f : 0.5f;
            cardBackground.color = color;
        }
    }
    
    // Methoden für CardManager Pool-System
    public void ClearEventSubscriptions()
    {
        // Hier könnten Sie spezifische Event-Subscriptions clearen
        // falls die Card-Instanz eigene Events abonniert hat
    }
    
    public void ResetCardState()
    {
        isSelected = false;
        isInteractable = true;
        cardData = null;
        UpdateVisuals();
        UpdateCardDisplay();
    }
    
    // Debug-Methoden
    [ContextMenu("Select Card")]
    public void DebugSelect()
    {
        Select();
    }
    
    [ContextMenu("Deselect Card")]
    public void DebugDeselect()
    {
        Deselect();
    }
}