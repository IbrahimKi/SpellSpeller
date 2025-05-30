using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class HandLayoutManager : MonoBehaviour
{
    [Header("Layout Settings")]
    [SerializeField] private float cardSpacing = 120f;
    [SerializeField] private float arcHeight = 50f;
    [SerializeField] private float maxRotationAngle = 15f;
    
    [Header("Animation")]
    [SerializeField] private float animationDuration = 0.5f;
    [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    private List<RectTransform> cardTransforms = new List<RectTransform>();
    private RectTransform rectTransform;
    private bool isArranging = false;
    
    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }
    
    private void Start()
    {
        DetectCards();
        ArrangeCards();
    }
    
    public void DetectCards()
    {
        cardTransforms.Clear();
        
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.gameObject.activeInHierarchy && child.GetComponent<Card>() != null)
            {
                RectTransform childRect = child.GetComponent<RectTransform>();
                if (childRect != null)
                    cardTransforms.Add(childRect);
            }
        }
    }
    
    public void ArrangeCards()
    {
        if (isArranging || cardTransforms.Count == 0) return;
        
        StartCoroutine(AnimateCardsToPositions());
    }
    
    private IEnumerator AnimateCardsToPositions()
    {
        isArranging = true;
        
        int cardCount = cardTransforms.Count;
        float totalWidth = (cardCount - 1) * cardSpacing;
        float startX = -totalWidth * 0.5f;
        
        List<Vector3> startPositions = new List<Vector3>();
        List<Quaternion> startRotations = new List<Quaternion>();
        List<Vector3> targetPositions = new List<Vector3>();
        List<Quaternion> targetRotations = new List<Quaternion>();
        
        for (int i = 0; i < cardCount; i++)
        {
            if (cardTransforms[i] == null) continue;
            
            startPositions.Add(cardTransforms[i].localPosition);
            startRotations.Add(cardTransforms[i].localRotation);
            
            float normalizedPos = cardCount > 1 ? (float)i / (cardCount - 1) : 0.5f;
            float xPos = startX + (i * cardSpacing);
            float yPos = Mathf.Sin(normalizedPos * Mathf.PI) * arcHeight;
            
            targetPositions.Add(new Vector3(xPos, yPos, 0));
            
            float rotation = (normalizedPos - 0.5f) * maxRotationAngle * 2f;
            targetRotations.Add(Quaternion.Euler(0, 0, rotation));
        }
        
        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float progress = animationCurve.Evaluate(elapsed / animationDuration);
            
            for (int i = 0; i < cardCount; i++)
            {
                if (cardTransforms[i] == null) continue;
                
                cardTransforms[i].localPosition = Vector3.Lerp(startPositions[i], targetPositions[i], progress);
                cardTransforms[i].localRotation = Quaternion.Lerp(startRotations[i], targetRotations[i], progress);
            }
            
            yield return null;
        }
        
        for (int i = 0; i < cardCount; i++)
        {
            if (cardTransforms[i] == null) continue;
            
            cardTransforms[i].localPosition = targetPositions[i];
            cardTransforms[i].localRotation = targetRotations[i];
        }
        
        isArranging = false;
    }
    
    public void UpdateLayout()
    {
        DetectCards();
        ArrangeCards();
    }
    
    public int GetCardCount()
    {
        return cardTransforms.Count;
    }
}