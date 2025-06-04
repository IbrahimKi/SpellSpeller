using UnityEngine;
using System.Collections;

/// <summary>
/// Einfacher GameManager der die Initialisierungsreihenfolge koordiniert
/// </summary>
public class SimpleGameManager : MonoBehaviour
{
    [Header("Manager References")]
    [SerializeField] private CardManager cardManager;
    [SerializeField] private DeckManager deckManager;
    [SerializeField] private CombatManager combatManager;
    [SerializeField] private SpellcastManager spellcastManager;
    [SerializeField] private HandLayoutManager handLayoutManager;
    
    [Header("Settings")]
    [SerializeField] private float initializationDelay = 0.1f;
    
    public static bool AllManagersReady { get; private set; }
    
    private void Start()
    {
        StartCoroutine(InitializeManagers());
    }
    
    private IEnumerator InitializeManagers()
    {
        Debug.Log("[GameManager] Starting manager initialization...");
        
        // 1. Find managers if not assigned
        FindManagers();
        
        yield return new WaitForSeconds(initializationDelay);
        
        // 2. Force initialization check
        bool allReady = CheckAllManagersReady();
        
        if (!allReady)
        {
            Debug.LogWarning("[GameManager] Some managers not ready, waiting...");
            yield return new WaitForSeconds(0.5f);
            allReady = CheckAllManagersReady();
        }
        
        AllManagersReady = true;
        Debug.Log("[GameManager] All managers ready!");
        
        // 3. Start combat
        if (combatManager != null)
        {
            combatManager.StartCombat();
        }
    }
    
    private void FindManagers()
    {
        if (cardManager == null) cardManager = FindFirstObjectByType<CardManager>();
        if (deckManager == null) deckManager = FindFirstObjectByType<DeckManager>();
        if (combatManager == null) combatManager = FindFirstObjectByType<CombatManager>();
        if (spellcastManager == null) spellcastManager = FindFirstObjectByType<SpellcastManager>();
        if (handLayoutManager == null) handLayoutManager = FindFirstObjectByType<HandLayoutManager>();
        
        Debug.Log($"[GameManager] Found managers - Card:{cardManager != null}, Deck:{deckManager != null}, Combat:{combatManager != null}");
    }
    
    private bool CheckAllManagersReady()
    {
        bool cardReady = cardManager != null && cardManager.IsInitialized;
        bool deckReady = deckManager != null && deckManager.IsInitialized;
        bool combatReady = combatManager != null;
        
        Debug.Log($"[GameManager] Manager status - Card:{cardReady}, Deck:{deckReady}, Combat:{combatReady}");
        
        return cardReady && deckReady && combatReady;
    }
}