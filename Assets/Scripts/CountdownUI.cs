using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the countdown UI for race start.
/// </summary>
public class CountdownUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject countdownPanel;
    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private TextMeshProUGUI readyText;
    
    [Header("Animation Settings")]
    [SerializeField] private float numberScaleFactor = 1.5f;
    [SerializeField] private float numberScaleDuration = 0.5f;
    [SerializeField] private Color readyColor = Color.yellow;
    [SerializeField] private Color countdownColor = Color.red;
    [SerializeField] private Color goColor = Color.green;
    
    private RectTransform countdownTextRect;
    
    private void Awake()
    {
        if (countdownText != null)
        {
            countdownTextRect = countdownText.GetComponent<RectTransform>();
        }
        
        // Hide countdown UI initially
        if (countdownPanel != null)
        {
            countdownPanel.SetActive(false);
        }
    }
    
    /// <summary>
    /// Shows the "Get Ready" text while waiting for AI to spawn.
    /// </summary>
    public void ShowGetReady()
    {
        if (countdownPanel != null)
        {
            countdownPanel.SetActive(true);
        }
        
        if (readyText != null)
        {
            readyText.gameObject.SetActive(true);
            readyText.text = "Get Ready";
            readyText.color = readyColor;
        }
        
        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
        }
    }
    
    /// <summary>
    /// Starts the countdown animation from the specified number to "GO!".
    /// </summary>
    /// <param name="startNumber">The number to start counting down from.</param>
    /// <returns>Coroutine that can be yielded on.</returns>
    public IEnumerator StartCountdown(int startNumber)
    {
        if (countdownPanel == null || countdownText == null)
        {
            Debug.LogError("CountdownUI: Missing UI references!");
            yield break;
        }
        
        countdownPanel.SetActive(true);
        
        if (readyText != null)
        {
            readyText.gameObject.SetActive(false);
        }
        
        countdownText.gameObject.SetActive(true);
        
        // Countdown animation
        for (int i = startNumber; i > 0; i--)
        {
            countdownText.text = i.ToString();
            countdownText.color = countdownColor;
            
            // Scale animation
            if (countdownTextRect != null)
            {
                countdownTextRect.localScale = Vector3.one * numberScaleFactor;
                
                float elapsedTime = 0f;
                while (elapsedTime < numberScaleDuration)
                {
                    float t = elapsedTime / numberScaleDuration;
                    countdownTextRect.localScale = Vector3.Lerp(Vector3.one * numberScaleFactor, Vector3.one, t);
                    elapsedTime += Time.deltaTime;
                    yield return null;
                }
                
                countdownTextRect.localScale = Vector3.one;
            }
            else
            {
                // Simple delay if no RectTransform
                yield return new WaitForSeconds(1f);
            }
        }
        
        // Show "GO!"
        countdownText.text = "GO!";
        countdownText.color = goColor;
        
        if (countdownTextRect != null)
        {
            countdownTextRect.localScale = Vector3.one * numberScaleFactor;
            
            float elapsedTime = 0f;
            while (elapsedTime < numberScaleDuration)
            {
                float t = elapsedTime / numberScaleDuration;
                countdownTextRect.localScale = Vector3.Lerp(Vector3.one * numberScaleFactor, Vector3.one, t);
                elapsedTime += Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(1f);
        }
        
        // Hide countdown UI after a short delay
        yield return new WaitForSeconds(0.5f);
        countdownPanel.SetActive(false);
    }
    
    /// <summary>
    /// Hides the countdown UI.
    /// </summary>
    public void HideCountdown()
    {
        if (countdownPanel != null)
        {
            countdownPanel.SetActive(false);
        }
    }
}
