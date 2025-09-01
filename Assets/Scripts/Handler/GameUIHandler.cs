using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public partial class GameUIHandler : MonoBehaviour
{
    [Header("Core UI")]
    [SerializeField] private TextMeshProUGUI statusText;
    
    // Performance optimization
    private const float UPDATE_INTERVAL = 0.1f;
    private float _lastUpdate = 0f;
    
    // === INITIALIZATION ===
    
    private void Start()
    {
        InitializeUI();
    }
    
    private void InitializeUI()
    {
        SetupStatusText();
    }
    
    private void SetupStatusText()
    {
        if (statusText != null)
        {
            statusText.text = "Select cards to play";
            statusText.color = Color.white;
        }
    }
    
    // === UI UPDATE SYSTEM ===
    
    private void Update()
    {
        if (Time.time - _lastUpdate < UPDATE_INTERVAL) return;
        _lastUpdate = Time.time;
        
        UpdateUI();
    }
    
    private void UpdateUI()
    {
        // Core UI updates here
    }
    
    // === UTILITY METHODS ===
    
    private void ShowMessage(string message, Color color)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = color;
            StartCoroutine(ResetStatusTextDelayed(2f));
        }
    }
    
    private IEnumerator ResetStatusTextDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (statusText != null)
        {
            statusText.text = "Select cards to play";
            statusText.color = Color.white;
        }
    }
    
    // === CLEANUP ===
    
    private void OnDestroy()
    {
        // Cleanup here
    }

#if UNITY_EDITOR
    [ContextMenu("Test UI Handler")]
    private void TestUIHandler()
    {
        Debug.Log($"[GameUIHandler] Status: Active");
    }
#endif
}