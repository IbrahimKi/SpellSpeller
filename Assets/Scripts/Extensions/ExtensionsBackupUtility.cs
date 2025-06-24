using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Backup Script für Extension-Migration
/// USAGE: [MenuItem] im Editor ausführen BEVOR die neuen Extensions implementiert werden
/// </summary>
public static class ExtensionBackupUtility
{
    private const string BACKUP_FOLDER = "Assets/Scripts/Extensions/Backup";
    
    [MenuItem("Tools/Extensions/Create Backup")]
    public static void CreateExtensionBackup()
    {
        Debug.Log("[ExtensionBackup] Starting backup process...");
        
        // Backup-Ordner erstellen
        if (!Directory.Exists(BACKUP_FOLDER))
        {
            Directory.CreateDirectory(BACKUP_FOLDER);
            Debug.Log($"[ExtensionBackup] Created backup folder: {BACKUP_FOLDER}");
        }
        
        // Files zum Backup
        var filesToBackup = new[]
        {
            "Assets/Scripts/Extensions/CardExtensions.cs",
            "Assets/Scripts/Extensions/CombatExtensions.cs", 
            "Assets/Scripts/Extensions/EntityExtensions.cs",
            "Assets/Scripts/Extensions/ManagerExtensions.cs",
            "Assets/Scripts/Extensions/ResourceExtensions.cs",
            "Assets/Scripts/Extensions/SharedEnums.cs"
        };
        
        int backedUp = 0;
        
        foreach (var filePath in filesToBackup)
        {
            if (File.Exists(filePath))
            {
                string fileName = Path.GetFileName(filePath);
                string backupPath = Path.Combine(BACKUP_FOLDER, $"{fileName}.backup");
                
                try
                {
                    File.Copy(filePath, backupPath, true);
                    Debug.Log($"[ExtensionBackup] ✅ Backed up: {fileName}");
                    backedUp++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[ExtensionBackup] ❌ Failed to backup {fileName}: {ex.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"[ExtensionBackup] ⚠️ File not found: {filePath}");
            }
        }
        
        Debug.Log($"[ExtensionBackup] Backup complete! {backedUp} files backed up to {BACKUP_FOLDER}");
        AssetDatabase.Refresh();
    }
    
    [MenuItem("Tools/Extensions/Restore from Backup")]
    public static void RestoreFromBackup()
    {
        if (!Directory.Exists(BACKUP_FOLDER))
        {
            Debug.LogError("[ExtensionBackup] No backup folder found!");
            return;
        }
        
        bool confirmed = EditorUtility.DisplayDialog(
            "Restore Extensions", 
            "This will OVERWRITE current extension files with backup versions. Continue?",
            "Yes, Restore", 
            "Cancel"
        );
        
        if (!confirmed) return;
        
        var backupFiles = Directory.GetFiles(BACKUP_FOLDER, "*.backup");
        int restored = 0;
        
        foreach (var backupFile in backupFiles)
        {
            string fileName = Path.GetFileName(backupFile).Replace(".backup", "");
            string originalPath = $"Assets/Scripts/Extensions/{fileName}";
            
            try
            {
                File.Copy(backupFile, originalPath, true);
                Debug.Log($"[ExtensionBackup] ✅ Restored: {fileName}");
                restored++;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ExtensionBackup] ❌ Failed to restore {fileName}: {ex.Message}");
            }
        }
        
        Debug.Log($"[ExtensionBackup] Restore complete! {restored} files restored.");
        AssetDatabase.Refresh();
    }
    
    [MenuItem("Tools/Extensions/Delete Backup")]
    public static void DeleteBackup()
    {
        if (!Directory.Exists(BACKUP_FOLDER))
        {
            Debug.LogWarning("[ExtensionBackup] No backup folder to delete.");
            return;
        }
        
        bool confirmed = EditorUtility.DisplayDialog(
            "Delete Backup", 
            "This will permanently delete all backup files. Continue?",
            "Yes, Delete", 
            "Cancel"
        );
        
        if (confirmed)
        {
            Directory.Delete(BACKUP_FOLDER, true);
            File.Delete($"{BACKUP_FOLDER}.meta");
            Debug.Log("[ExtensionBackup] Backup folder deleted.");
            AssetDatabase.Refresh();
        }
    }
}