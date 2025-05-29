using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// UI Handler für das Ausspielen von Karten - sendet Events an den GameManager
/// </summary>
public class CardPlayHandler : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button clearButton;
    
    [Header("Visual Feedback")]
    [SerializeField] private TMPro.TextMeshProUGUI currentLettersDisplay;
    [SerializeField] private TMPro.TextMeshProUGUI statusDisplay;
    
    [Header("Settings")]
    [SerializeField] private bool autoPlayOnSelection = false;
    [SerializeField] private bool showDebugInfo = false;
    
    // Cached selected cards für Performance
    private List<Card> _cachedSelectedCards = new List<Card>();
    private string _cachedLetterSequence = "";
    private bool _isDirty = true;
    
    private void Awake()
    {
        // Setup UI event listeners
        if (playButton != null)
            playButton.onClick.AddListener(PlaySelectedCards);
            
        if (clearButton != null)
            clearButton.onClick.AddListener(ClearSelection);
    }
    
    private void OnEnable()
    {
        // Subscribe to card selection events
        CardManager.OnSelectionChanged += OnSelectionChanged;
        SpellcastManager.OnLetterSequenceUpdated += OnLetterSequenceUpdated;
        SpellcastManager.OnSpellFound += OnSpellFound;
        SpellcastManager.OnSpellNotFound += OnSpellNotFound;
    }
    
    private void OnDisable()
    {
        // Unsubscribe from events
        CardManager.OnSelectionChanged -= OnSelectionChanged;
        SpellcastManager.OnLetterSequenceUpdated -= OnLetterSequenceUpdated;
        SpellcastManager.OnSpellFound -= OnSpellFound;
        SpellcastManager.OnSpellNotFound -= OnSpellNotFound;
    }
    
    private void OnSelectionChanged(List<Card> selectedCards)
    {
        _cachedSelectedCards.Clear();
        _cachedSelectedCards.AddRange(selectedCards);
        _isDirty = true;
        
        UpdateUI();
        
        // Auto-play wenn aktiviert
        if (autoPlayOnSelection && _cachedSelectedCards.Count > 0)
        {
            PlaySelectedCards();
        }
    }
    
    /// <summary>
    /// Spielt alle ausgewählten Karten aus und sendet Letter Values an GameManager
    /// </summary>
    public void PlaySelectedCards()
    {
        if (_cachedSelectedCards.Count == 0)
        {
            if (showDebugInfo)
                Debug.Log("[CardPlayHandler] No cards selected to play");
            return;
        }
        
        // Letter Sequence aus Karten extrahieren (nur bei Bedarf neu berechnen)
        if (_isDirty)
        {
            _cachedLetterSequence = ExtractLetterSequence(_cachedSelectedCards);
            _isDirty = false;
        }
        
        if (string.IsNullOrEmpty(_cachedLetterSequence))
        {
            if (showDebugInfo)
                Debug.Log("[CardPlayHandler] No letters found in selected cards");
            return;
        }
        
        // Event an GameManager senden
        SpellcastManager.Instance?.ProcessCardPlay(_cachedSelectedCards, _cachedLetterSequence);
        
        if (showDebugInfo)
        {
            Debug.Log($"[CardPlayHandler] Played {_cachedSelectedCards.Count} cards with letters: {_cachedLetterSequence}");
        }
    }
    
    /// <summary>
    /// Auswahl zurücksetzen
    /// </summary>
    public void ClearSelection()
    {
        CardManager.Instance?.ClearSelection();
        _cachedSelectedCards.Clear();
        _cachedLetterSequence = "";
        _isDirty = true;
        UpdateUI();
    }
    
    /// <summary>
    /// Extrahiert Letter Sequence aus Karten (performant mit StringBuilder)
    /// </summary>
    private string ExtractLetterSequence(List<Card> cards)
    {
        if (cards == null || cards.Count == 0) return "";
        
        var letterBuilder = new System.Text.StringBuilder();
        
        foreach (var card in cards)
        {
            if (card?.Data?.letterValues != null)
            {
                letterBuilder.Append(card.Data.letterValues);
            }
        }
        
        return letterBuilder.ToString();
    }
    
    /// <summary>
    /// UI Updates basierend auf aktuellem Zustand
    /// </summary>
    private void UpdateUI()
    {
        bool hasCards = _cachedSelectedCards.Count > 0;
        
        // Button States
        if (playButton != null)
        {
            playButton.interactable = hasCards;
        }
        
        if (clearButton != null)
        {
            clearButton.interactable = hasCards;
        }
        
        // Letter Display
        if (currentLettersDisplay != null)
        {
            if (_isDirty && hasCards)
            {
                _cachedLetterSequence = ExtractLetterSequence(_cachedSelectedCards);
                _isDirty = false;
            }
            
            currentLettersDisplay.text = hasCards ? $"Letters: {_cachedLetterSequence}" : "No cards selected";
        }
        
        // Status Display
        if (statusDisplay != null)
        {
            statusDisplay.text = hasCards ? $"{_cachedSelectedCards.Count} card(s) selected" : "Select cards to play";
        }
    }
    
    #region Event Handlers für GameManager Feedback
    
    private void OnLetterSequenceUpdated(string letterSequence)
    {
        if (showDebugInfo)
        {
            Debug.Log($"[CardPlayHandler] Letter sequence updated: {letterSequence}");
        }
        
        // Optional: UI Feedback für Letter Updates
        if (statusDisplay != null)
        {
            statusDisplay.text = $"Processing: {letterSequence}";
        }
    }
    
    private void OnSpellFound(string spellName, string usedLetters)
    {
        if (showDebugInfo)
        {
            Debug.Log($"[CardPlayHandler] Spell found: {spellName} using letters: {usedLetters}");
        }
        
        // Visual Feedback für erfolgreichen Spell
        if (statusDisplay != null)
        {
            statusDisplay.text = $"Spell Cast: {spellName}!";
            statusDisplay.color = Color.green;
        }
        
        // Optional: Erfolgs-Animation oder Effekte triggern
        TriggerSuccessEffect();
    }
    
    private void OnSpellNotFound(string attemptedLetters)
    {
        if (showDebugInfo)
        {
            Debug.Log($"[CardPlayHandler] No spell found for letters: {attemptedLetters}");
        }
        
        // Visual Feedback für fehlgeschlagenen Spell
        if (statusDisplay != null)
        {
            statusDisplay.text = $"No spell found for: {attemptedLetters}";
            statusDisplay.color = Color.red;
        }
        
        // Optional: Fehler-Animation oder Effekte triggern
        TriggerFailureEffect();
    }
    
    #endregion
    
    #region Visual Effects (Optional - können erweitert werden)
    
    private void TriggerSuccessEffect()
    {
        // TODO: Hier können Erfolgs-Effekte implementiert werden:
        // - Partikel-Effekte
        // - Screen-Shake
        // - Sound-Effekte
        // - UI-Animationen
        
        // Beispiel: Status-Text nach kurzer Zeit zurücksetzen
        if (statusDisplay != null)
        {
            Invoke(nameof(ResetStatusDisplay), 2f);
        }
    }
    
    private void TriggerFailureEffect()
    {
        // TODO: Hier können Fehler-Effekte implementiert werden:
        // - Shake-Animation
        // - Rote Farb-Animation
        // - Sound-Effekte
        
        // Beispiel: Status-Text nach kurzer Zeit zurücksetzen
        if (statusDisplay != null)
        {
            Invoke(nameof(ResetStatusDisplay), 1.5f);
        }
    }
    
    private void ResetStatusDisplay()
    {
        if (statusDisplay != null)
        {
            statusDisplay.color = Color.white;
            UpdateUI(); // Zurück zum normalen Status
        }
    }
    
    #endregion
    
    #region Public Methods für externe Verwendung
    
    /// <summary>
    /// Prüft ob aktuell Karten ausgewählt sind
    /// </summary>
    public bool HasSelectedCards => _cachedSelectedCards.Count > 0;
    
    /// <summary>
    /// Aktuelle Letter Sequence abrufen
    /// </summary>
    public string GetCurrentLetterSequence()
    {
        if (_isDirty)
        {
            _cachedLetterSequence = ExtractLetterSequence(_cachedSelectedCards);
            _isDirty = false;
        }
        return _cachedLetterSequence;
    }
    
    /// <summary>
    /// Anzahl ausgewählter Karten
    /// </summary>
    public int SelectedCardCount => _cachedSelectedCards.Count;
    
    #endregion
    
    #region Editor Helpers
    
    #if UNITY_EDITOR
    [ContextMenu("Play Selected Cards")]
    private void EditorPlaySelectedCards()
    {
        PlaySelectedCards();
    }
    
    [ContextMenu("Clear Selection")]
    private void EditorClearSelection()
    {
        ClearSelection();
    }
    
    [ContextMenu("Update UI")]
    private void EditorUpdateUI()
    {
        UpdateUI();
    }
    #endif
    
    #endregion
}