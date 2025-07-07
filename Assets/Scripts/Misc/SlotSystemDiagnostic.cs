using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Diagnostic Script für Card Slot System Debugging
/// USAGE: Als Component zu einem GameObject hinzufügen und im Inspector ausführen
/// </summary>
public class SlotSystemDiagnostic : MonoBehaviour
{
    [Header("Auto-Run Settings")]
    [SerializeField] private bool runOnStart = false;
    [SerializeField] private float autoRunInterval = 5f;
    
    private void Start()
    {
        if (runOnStart)
        {
            InvokeRepeating(nameof(RunFullDiagnostic), 1f, autoRunInterval);
        }
    }
    
    [ContextMenu("Run Full Diagnostic")]
    public void RunFullDiagnostic()
    {
        Debug.Log("=== CARD SLOT SYSTEM DIAGNOSTIC ===");
        
        CheckCardSlotManager();
        CheckSlotBehaviours();
        CheckGameUIHandler();
        CheckSceneSetup();
        
        Debug.Log("=== DIAGNOSTIC COMPLETE ===");
    }
    
    private void CheckCardSlotManager()
    {
        Debug.Log("--- CardSlotManager Check ---");
        
        if (!CardSlotManager.HasInstance)
        {
            Debug.LogError("❌ CardSlotManager: Instance not found!");
            return;
        }
        
        var csm = CardSlotManager.Instance;
        Debug.Log($"✅ CardSlotManager found");
        Debug.Log($"  IsReady: {csm.IsReady}");
        Debug.Log($"  IsEnabled: {csm.IsEnabled}");
        Debug.Log($"  SlotCount: {csm.SlotCount}");
        Debug.Log($"  FilledSlots: {csm.FilledSlotCount}");
        Debug.Log($"  EmptySlots: {csm.EmptySlotCount}");
        Debug.Log($"  CurrentSequence: '{csm.GetSlotLetterSequence()}'");
        Debug.Log($"  CanPlaySequence: {csm.CanPlaySlotSequence()}");
        
        for (int i = 0; i < csm.SlotCount; i++)
        {
            var slot = csm.Slots[i];
            if (slot != null)
            {
                Debug.Log($"    Slot {i + 1}: {(slot.IsFilled ? "FILLED" : "EMPTY")} - {slot.GetSlotInfo()}");
            }
            else
            {
                Debug.LogWarning($"    Slot {i + 1}: NULL REFERENCE");
            }
        }
    }
    
    private void CheckSlotBehaviours()
    {
        Debug.Log("--- CardSlotBehaviour Check ---");
        
        var allSlots = FindObjectsOfType<CardSlotBehaviour>();
        Debug.Log($"Found {allSlots.Length} CardSlotBehaviour components in scene");
        
        foreach (var slot in allSlots)
        {
            Debug.Log($"  Slot {slot.SlotIndex + 1} ({slot.name}):");
            Debug.Log($"    IsEnabled: {slot.IsEnabled}");
            Debug.Log($"    IsEmpty: {slot.IsEmpty}");
            Debug.Log($"    OccupyingCard: {slot.OccupyingCard?.GetCardName() ?? "None"}");
            
            var image = slot.GetComponent<Image>();
            var rectTransform = slot.GetComponent<RectTransform>();
            Debug.Log($"    Has Image: {image != null}");
            Debug.Log($"    Has RectTransform: {rectTransform != null}");
            Debug.Log($"    Size: {rectTransform?.sizeDelta}");
        }
    }
    
    private void CheckGameUIHandler()
    {
        Debug.Log("--- GameUIHandler Check ---");
        
        var uiHandler = FindObjectOfType<GameUIHandler>();
        if (uiHandler == null)
        {
            Debug.LogError("❌ GameUIHandler not found in scene!");
            return;
        }
        
        Debug.Log($"✅ GameUIHandler found: {uiHandler.name}");
        
        var playButton = GetFieldValue<Button>(uiHandler, "playSlotSequenceButton");
        var clearButton = GetFieldValue<Button>(uiHandler, "clearSlotsButton");
        var slotPanel = GetFieldValue<GameObject>(uiHandler, "slotSystemPanel");
        
        Debug.Log($"  PlaySlotButton: {(playButton != null ? "✅" : "❌")} - Interactable: {playButton?.interactable}");
        Debug.Log($"  ClearSlotsButton: {(clearButton != null ? "✅" : "❌")} - Interactable: {clearButton?.interactable}");
        Debug.Log($"  SlotSystemPanel: {(slotPanel != null ? "✅" : "❌")} - Active: {slotPanel?.activeSelf}");
        
        if (playButton != null)
        {
            Debug.Log($"    Play Button Listeners: {playButton.onClick.GetPersistentEventCount()}");
        }
        
        if (clearButton != null)
        {
            Debug.Log($"    Clear Button Listeners: {clearButton.onClick.GetPersistentEventCount()}");
        }
    }
    
    private void CheckSceneSetup()
    {
        Debug.Log("--- Scene Setup Check ---");
        
        var canvas = FindObjectOfType<Canvas>();
        var eventSystem = FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
        var graphicRaycaster = FindObjectOfType<GraphicRaycaster>();
        
        Debug.Log($"  Canvas: {(canvas != null ? "✅" : "❌")}");
        Debug.Log($"  EventSystem: {(eventSystem != null ? "✅" : "❌")}");
        Debug.Log($"  GraphicRaycaster: {(graphicRaycaster != null ? "✅" : "❌")}");
        
        bool hasGameManager = GameManager.HasInstance;
        Debug.Log($"  GameManager: {(hasGameManager ? "✅" : "❌")}");
        
        if (hasGameManager)
        {
            Debug.Log($"    IsInitialized: {GameManager.Instance.IsInitialized}");
        }
        
        Debug.Log($"  CardManager: {(CardManager.HasInstance && CardManager.Instance.IsReady ? "✅" : "❌")}");
        Debug.Log($"  SpellcastManager: {(SpellcastManager.HasInstance && SpellcastManager.Instance.IsReady ? "✅" : "❌")}");
        Debug.Log($"  CombatManager: {(CombatManager.HasInstance && CombatManager.Instance.IsReady ? "✅" : "❌")}");
    }
    
    private T GetFieldValue<T>(object obj, string fieldName) where T : class
    {
        try
        {
            var field = obj.GetType().GetField(fieldName, 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            return field?.GetValue(obj) as T;
        }
        catch
        {
            return null;
        }
    }
    
    [ContextMenu("Test Card Placement")]
    public void TestCardPlacement()
    {
        Debug.Log("--- Testing Card Placement ---");
        
        if (!CardManager.HasInstance)
        {
            Debug.LogError("CardManager not available for testing");
            return;
        }
        
        if (!CardSlotManager.HasInstance)
        {
            Debug.LogError("CardSlotManager not available for testing");
            return;
        }
        
        var cardManager = CardManager.Instance;
        var slotManager = CardSlotManager.Instance;
        
        var handCards = cardManager.GetHandCards();
        if (handCards.Count == 0)
        {
            Debug.LogWarning("No cards in hand to test with");
            return;
        }
        
        var testCard = handCards[0];
        Debug.Log($"Testing with card: {testCard.GetCardName()}");
        
        bool placed = slotManager.TryPlaceCardInSlot(testCard);
        Debug.Log($"Placement result: {(placed ? "SUCCESS" : "FAILED")}");
        
        if (placed)
        {
            Debug.Log($"Card is now in slot. Sequence: '{slotManager.GetSlotLetterSequence()}'");
        }
    }
    
    [ContextMenu("Test Button Actions")]
    public void TestButtonActions()
    {
        Debug.Log("--- Testing Button Actions ---");
        
        var uiHandler = FindObjectOfType<GameUIHandler>();
        if (uiHandler == null)
        {
            Debug.LogError("GameUIHandler not found");
            return;
        }
        
        try
        {
            var playMethod = uiHandler.GetType().GetMethod("PlaySlotSequence", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            
            var clearMethod = uiHandler.GetType().GetMethod("ClearAllSlots", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            
            if (playMethod != null)
            {
                Debug.Log("Testing PlaySlotSequence...");
                playMethod.Invoke(uiHandler, null);
            }
            
            if (clearMethod != null)
            {
                Debug.Log("Testing ClearAllSlots...");
                clearMethod.Invoke(uiHandler, null);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Button test failed: {ex.Message}");
        }
    }
    
    [ContextMenu("Force Refresh All")]
    public void ForceRefreshAll()
    {
        Debug.Log("--- Force Refreshing All Systems ---");
        
        if (CardSlotManager.HasInstance)
        {
            var csm = CardSlotManager.Instance;
            csm.SetEnabled(false);
            csm.SetEnabled(true);
            Debug.Log("CardSlotManager refreshed");
        }
        
        var allSlots = FindObjectsOfType<CardSlotBehaviour>();
        foreach (var slot in allSlots)
        {
            slot.ForceRefreshVisuals();
        }
        Debug.Log($"Refreshed {allSlots.Length} slot visuals");
        
        var uiHandler = FindObjectOfType<GameUIHandler>();
        if (uiHandler != null)
        {
            try
            {
                var refreshMethod = uiHandler.GetType().GetMethod("UpdateSlotSystemDisplay", 
                    System.Reflection.BindingFlags.NonPublic | 
                    System.Reflection.BindingFlags.Instance);
                refreshMethod?.Invoke(uiHandler, null);
                Debug.Log("GameUIHandler slot display refreshed");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"UI refresh failed: {ex.Message}");
            }
        }
    }
}

public static class CardSlotBehaviourExtensions
{
    public static void ForceRefreshVisuals(this CardSlotBehaviour slot)
    {
        try
        {
            var method = slot.GetType().GetMethod("UpdateVisuals", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            method?.Invoke(slot, null);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Force refresh failed for slot: {ex.Message}");
        }
    }
}