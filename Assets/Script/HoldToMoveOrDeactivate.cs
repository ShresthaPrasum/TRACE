using UnityEngine;

public class HoldToChange : MonoBehaviour
{
    public enum TriggerBehavior { Move, Activate, Deactivate }

    [Header("Behavior Settings")]
    [SerializeField] private GameObject targetObject;
    [SerializeField] private TriggerBehavior behavior = TriggerBehavior.Move;
    [SerializeField] private Vector3 movementOffset; 
    [SerializeField] private float transitionSpeed = 5f;

    [Header("Visual Settings")]
    [SerializeField] private Color activeColor = Color.white;
    [SerializeField] private Color resetColor = Color.red;

    private Vector3 originalPosition;
    private Vector3 activePosition; 
    private SpriteRenderer switchRenderer;
    private int objectsOnPlate = 0;

    void Start()
    {
        switchRenderer = GetComponent<SpriteRenderer>();

        if (targetObject != null)
        {
            originalPosition = targetObject.transform.localPosition;
            
            
            activePosition = originalPosition + movementOffset;

            if (behavior == TriggerBehavior.Activate)
            {
                targetObject.SetActive(false);
            }
        }
    }

    void Update()
    {
        if (targetObject == null) return;

        bool isTriggered = objectsOnPlate > 0;

        if (isTriggered)
        {
            ApplyActiveState();
        }
        else
        {
            ApplyResetState();
        }
    }

    private void ApplyActiveState()
    {
        switch (behavior)
        {
            case TriggerBehavior.Move:
                targetObject.transform.localPosition = Vector3.Lerp(
                    targetObject.transform.localPosition, 
                    activePosition, 
                    Time.deltaTime * transitionSpeed
                );
                break;

            case TriggerBehavior.Activate:
                targetObject.SetActive(true);
                break;

            case TriggerBehavior.Deactivate:
                targetObject.SetActive(false);
                break;
        }

        if (switchRenderer != null) switchRenderer.color = activeColor;
    }

    private void ApplyResetState()
    {
        switch (behavior)
        {
            case TriggerBehavior.Move:
                targetObject.transform.localPosition = Vector3.Lerp(
                    targetObject.transform.localPosition, 
                    originalPosition, 
                    Time.deltaTime * transitionSpeed
                );
                break;

            case TriggerBehavior.Activate:
                targetObject.SetActive(false);
                break;

            case TriggerBehavior.Deactivate:
                targetObject.SetActive(true);
                break;
        }

        if (switchRenderer != null) switchRenderer.color = resetColor;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        
        if (other.CompareTag("Player") || other.CompareTag("Decoy"))
        {
            objectsOnPlate++;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player") || other.CompareTag("Decoy"))
        {
            objectsOnPlate--;
        }
    }
}