using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class CardPlayTestRunner : MonoBehaviour
{
    [Header("Test Configuration")]
    [SerializeField] private CardManager cardManager;
    [SerializeField] private CardPlayHandler cardPlayHandler;
    
    [Header("Draw Pool Setup")]
    [SerializeField] private int drawPoolSize = 20;
    [SerializeField] private bool useRandomSelection = true;
    [SerializeField] private bool allowDuplicates = true;
    [SerializeField] private bool runOnStart = true;
    
    [Header("Debug Info")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private TMPro.TextMeshProUGUI debugDisplay;
    
    private void Start()
    {
        if (runOnStart)
        {
            // Kleine Verzögerung um sicherzustellen, dass alle Manager initialisiert sind
            Invoke(nameof(InitializeDrawPool), 0.1f);
        }
    }
    
    [ContextMenu("Initialize Draw Pool")]
    public void InitializeDrawPool()
    {
        if (!ValidateComponents())
            return;
        
        // Zugriff auf allCardData über Reflection, da es private ist
        var allCardData = GetAllCardData();
        if (allCardData == null || allCardData.Count == 0)
        {
            LogError("No card data found in CardManager!");
            return;
        }
        
        List<CardData> drawPool = GenerateDrawPool(allCardData);
        AssignDrawPoolToHandler(drawPool);
        
        if (showDebugLogs)
        {
            LogSuccess($"Draw pool initialized with {drawPool.Count} cards");
            DisplayDrawPoolInfo(drawPool);
        }
    }
    
    private bool ValidateComponents()
    {
        if (cardManager == null)
            cardManager = CardManager.Instance;
        
        if (cardPlayHandler == null)
            cardPlayHandler = FindObjectOfType<CardPlayHandler>();
        
        if (cardManager == null)
        {
            LogError("CardManager not found!");
            return false;
        }
        
        if (cardPlayHandler == null)
        {
            LogError("CardPlayHandler not found!");
            return false;
        }
        
        return true;
    }
    
    private List<CardData> GetAllCardData()
    {
        // Verwende Reflection um auf das private allCardData Feld zuzugreifen
        var field = typeof(CardManager).GetField("allCardData", 
            System.Reflection.BindingFlags.NonPublic | 
            System.Reflection.BindingFlags.Instance);
        
        if (field != null)
        {
            return field.GetValue(cardManager) as List<CardData>;
        }
        
        LogError("Could not access allCardData field via reflection!");
        return new List<CardData>();
    }
    
    private List<CardData> GenerateDrawPool(List<CardData> sourceCards)
    {
        List<CardData> drawPool = new List<CardData>();
        
        if (useRandomSelection)
        {
            // Zufällige Auswahl
            for (int i = 0; i < drawPoolSize; i++)
            {
                CardData randomCard = sourceCards[Random.Range(0, sourceCards.Count)];
                
                if (!allowDuplicates && drawPool.Contains(randomCard))
                {
                    // Versuche eine andere Karte zu finden
                    int attempts = 0;
                    while (drawPool.Contains(randomCard) && attempts < sourceCards.Count)
                    {
                        randomCard = sourceCards[Random.Range(0, sourceCards.Count)];
                        attempts++;
                    }
                    
                    if (attempts >= sourceCards.Count && !allowDuplicates)
                    {
                        LogWarning($"Could not find enough unique cards. Stopping at {drawPool.Count} cards.");
                        break;
                    }
                }
                
                drawPool.Add(randomCard);
            }
        }
        else
        {
            // Sequenzielle Auswahl
            int cardCount = allowDuplicates ? drawPoolSize : Mathf.Min(drawPoolSize, sourceCards.Count);
            
            for (int i = 0; i < cardCount; i++)
            {
                CardData card = sourceCards[i % sourceCards.Count];
                if (!allowDuplicates && drawPool.Contains(card))
                    continue;
                    
                drawPool.Add(card);
            }
        }
        
        return drawPool;
    }
    
    private void AssignDrawPoolToHandler(List<CardData> drawPool)
    {
        // Verwende Reflection um auf das private drawPool Feld zuzugreifen
        var field = typeof(CardPlayHandler).GetField("drawPool", 
            System.Reflection.BindingFlags.NonPublic | 
            System.Reflection.BindingFlags.Instance);
        
        if (field != null)
        {
            field.SetValue(cardPlayHandler, drawPool);
            LogSuccess("Draw pool successfully assigned to CardPlayHandler");
        }
        else
        {
            LogError("Could not access drawPool field via reflection!");
        }
    }
    
    private void DisplayDrawPoolInfo(List<CardData> drawPool)
    {
        if (debugDisplay == null) return;
        
        var cardNames = drawPool.Take(10).Select(card => card.name).ToArray();
        string displayText = $"Draw Pool: {drawPool.Count} cards\n";
        displayText += $"First 10: {string.Join(", ", cardNames)}";
        
        if (drawPool.Count > 10)
            displayText += $"\n... and {drawPool.Count - 10} more";
        
        debugDisplay.text = displayText;
    }
    
    // Utility Methoden für verschiedene Test-Szenarien
    [ContextMenu("Quick Test - Small Pool")]
    public void QuickTestSmallPool()
    {
        drawPoolSize = 5;
        useRandomSelection = true;
        allowDuplicates = false;
        InitializeDrawPool();
    }
    
    [ContextMenu("Quick Test - Large Pool")]
    public void QuickTestLargePool()
    {
        drawPoolSize = 50;
        useRandomSelection = true;
        allowDuplicates = true;
        InitializeDrawPool();
    }
    
    [ContextMenu("Quick Test - All Unique")]
    public void QuickTestAllUnique()
    {
        var allCards = GetAllCardData();
        if (allCards != null)
        {
            drawPoolSize = allCards.Count;
            useRandomSelection = false;
            allowDuplicates = false;
            InitializeDrawPool();
        }
    }
    
    // Debug Logging
    private void LogSuccess(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[CardPlayTestRunner] <color=green>{message}</color>");
    }
    
    private void LogWarning(string message)
    {
        if (showDebugLogs)
            Debug.LogWarning($"[CardPlayTestRunner] {message}");
    }
    
    private void LogError(string message)
    {
        if (showDebugLogs)
            Debug.LogError($"[CardPlayTestRunner] {message}");
    }
    
    // Public Properties für Inspector-Überwachung
    public int CurrentDrawPoolSize
    {
        get
        {
            if (cardPlayHandler == null) return 0;
            
            var field = typeof(CardPlayHandler).GetField("drawPool", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            
            if (field?.GetValue(cardPlayHandler) is List<CardData> pool)
                return pool.Count;
            
            return 0;
        }
    }
    
    // Reset-Funktion für Tests
    [ContextMenu("Clear Draw Pool")]
    public void ClearDrawPool()
    {
        if (cardPlayHandler == null) return;
        
        var field = typeof(CardPlayHandler).GetField("drawPool", 
            System.Reflection.BindingFlags.NonPublic | 
            System.Reflection.BindingFlags.Instance);
        
        if (field != null)
        {
            field.SetValue(cardPlayHandler, new List<CardData>());
            LogSuccess("Draw pool cleared");
            
            if (debugDisplay != null)
                debugDisplay.text = "Draw Pool: 0 cards";
        }
    }
}