using UnityEngine;
using UnityEngine.UI;
using TMPro;

// AUTO-FIX Script für Card Prefab
public class CardPrefabFixer : MonoBehaviour
{
    [ContextMenu("Fix Card Prefab Setup")]
    public void FixCardPrefab()
    {
        Debug.Log("=== FIXING CARD PREFAB ===");
        
        // 1. Entferne Canvas und GraphicRaycaster vom Root
        var canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            Debug.Log("Removing Canvas from Card Root");
            DestroyImmediate(canvas);
        }
        
        var raycaster = GetComponent<GraphicRaycaster>();
        if (raycaster != null)
        {
            Debug.Log("Removing GraphicRaycaster from Card Root");
            DestroyImmediate(raycaster);
        }
        
        // 2. Stelle sicher, dass CanvasGroup vorhanden ist
        var canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            Debug.Log("Adding CanvasGroup to Card Root");
            gameObject.AddComponent<CanvasGroup>();
        }
        
        // 3. Finde und konfiguriere Card Background für Raycasting
        var cardBackground = transform.Find("Card Background")?.GetComponent<Image>();
        if (cardBackground != null)
        {
            cardBackground.raycastTarget = true;
            Debug.Log("Card Background: Raycast Target = true ✓");
        }
        else
        {
            Debug.LogWarning("Card Background not found!");
        }
        
        // 4. Deaktiviere Raycast Target auf allen anderen UI Elementen
        var allImages = GetComponentsInChildren<Image>();
        var allTexts = GetComponentsInChildren<TextMeshProUGUI>();
        
        foreach (var img in allImages)
        {
            if (img != cardBackground)
            {
                img.raycastTarget = false;
                Debug.Log($"Disabled raycast on: {img.name}");
            }
        }
        
        foreach (var text in allTexts)
        {
            text.raycastTarget = false;
            Debug.Log($"Disabled raycast on text: {text.name}");
        }
        
        // 5. Prüfe Card Script Referenzen
        var cardScript = GetComponent<Card>();
        if (cardScript != null && cardBackground != null)
        {
            // Private field zuweisen via Reflection (nur für Fix)
            var cardBackgroundField = typeof(Card).GetField("cardBackground", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (cardBackgroundField != null)
            {
                cardBackgroundField.SetValue(cardScript, cardBackground);
                Debug.Log("Card Script: cardBackground reference updated");
            }
        }
        
        Debug.Log("=== CARD PREFAB FIX COMPLETE ===");
        Debug.Log("✓ Canvas removed");
        Debug.Log("✓ GraphicRaycaster removed"); 
        Debug.Log("✓ CanvasGroup ensured");
        Debug.Log("✓ Raycast targets configured");
        Debug.Log("✓ Ready for dragging!");
    }
    
    [ContextMenu("Analyze Card Structure")]
    public void AnalyzeCardStructure()
    {
        Debug.Log("=== CARD STRUCTURE ANALYSIS ===");
        
        // Check Components on Root
        Debug.Log("ROOT COMPONENTS:");
        var components = GetComponents<Component>();
        foreach (var comp in components)
        {
            Debug.Log($"  - {comp.GetType().Name}");
        }
        
        // Check Canvas setup
        var canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            Debug.LogWarning($"  ⚠️ Canvas found! RenderMode: {canvas.renderMode}");
        }
        else
        {
            Debug.Log("  ✓ No Canvas (good!)");
        }
        
        // Check Raycast Targets
        Debug.Log("\nRAYCAST TARGETS:");
        var images = GetComponentsInChildren<Image>();
        var texts = GetComponentsInChildren<TextMeshProUGUI>();
        
        foreach (var img in images)
        {
            string status = img.raycastTarget ? "❌ ENABLED" : "✓ disabled";
            if (img.name == "Card Background" && img.raycastTarget)
                status = "✅ ENABLED (correct)";
            Debug.Log($"  Image '{img.name}': {status}");
        }
        
        foreach (var text in texts)
        {
            string status = text.raycastTarget ? "❌ ENABLED" : "✓ disabled";
            Debug.Log($"  Text '{text.name}': {status}");
        }
    }
}