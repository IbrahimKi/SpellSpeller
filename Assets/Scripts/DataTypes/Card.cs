using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class Card : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Card Data")]
    [SerializeField] private CardData cardData;
    [SerializeField] private bool isSelected;
    [SerializeField] private bool isInteractable = true;
    
    [Header("Visual")]
    [SerializeField] private Image cardBackground;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = Color.yellow;
    [SerializeField] private Color hoverColor = Color.gray;
    
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI cardNameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI letterValuesText;
    [SerializeField] private Image cardImage;
    
    // Events
    public static System.Action<Card> OnCardSelected;
    public static System.Action<Card> OnCardDeselected;
    
    // Properties
    public CardData CardData => cardData;
    public bool IsSelected => isSelected;
    public bool IsInteractable => isInteractable;
    public CardState CurrentState { get; private set; } = CardState.Idle;
    
    public CardType GetCardType() => cardData?.cardType ?? CardType.Consonant;
    public CardSubType GetCardSubType() => cardData?.CardSubType ?? CardSubType.Basic;
    public int GetTier() => cardData?.tier ?? 1;
    public string GetCardName() => cardData?.cardName ?? "Unknown Card";
    public bool HasTag(string tag) => false; // Placeholder - Tags nicht implementiert
    
    void Awake()
    {
        if (!cardBackground) cardBackground = GetComponent<Image>();
    }
    
    void Start() => UpdateVisuals();
    
    public void SetCardData(CardData data)
    {
        cardData = data;
        if (!data) return;
        
        if (cardNameText) cardNameText.text = data.cardName;
        if (descriptionText) descriptionText.text = data.description;
        if (letterValuesText) letterValuesText.text = data.letterValues;
        if (cardImage && data.cardImage) 
        {
            cardImage.sprite = data.cardImage;
            cardImage.gameObject.SetActive(true);
        }
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isInteractable || eventData.button != PointerEventData.InputButton.Left) return;
        ToggleSelection();
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isInteractable) return;
        UpdateVisuals(true);
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isInteractable) return;
        UpdateVisuals();
    }
    
    void ToggleSelection()
    {
        if (isSelected) Deselect();
        else Select();
    }
    
    public void Select()
    {
        if (!isInteractable || isSelected) return;
        isSelected = true;
        CurrentState = CardState.Selected;
        UpdateVisuals();
        OnCardSelected?.Invoke(this);
    }
    
    public void Deselect()
    {
        if (!isSelected) return;
        isSelected = false;
        CurrentState = CardState.Idle;
        UpdateVisuals();
        OnCardDeselected?.Invoke(this);
    }
    public bool TrySelect()
    {
        if (!isInteractable || isSelected) return false;
        Select();
        return true;
    }

    public bool TryDeselect() 
    {
        if (!isSelected) return false;
        Deselect();
        return true;
    }
    void UpdateVisuals(bool hover = false)
    {
        if (!cardBackground) return;
        cardBackground.color = CurrentState == CardState.Selected ? selectedColor : 
                              hover ? hoverColor : normalColor;
    }
    
    public void SetInteractable(bool interactable)
    {
        isInteractable = interactable;
        CurrentState = interactable ? CardState.Idle : CardState.Disabled;
        if (!interactable) isSelected = false;
        UpdateVisuals();
    }
    
    public void ResetCardState()
    {
        isSelected = false;
        isInteractable = true;
        CurrentState = CardState.Idle;
        cardData = null;
        
        if (cardNameText) cardNameText.text = "";
        if (descriptionText) descriptionText.text = "";
        if (letterValuesText) letterValuesText.text = "";
        if (cardImage) cardImage.gameObject.SetActive(false);
        
        UpdateVisuals();
    }
}