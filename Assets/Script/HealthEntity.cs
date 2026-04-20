using UnityEngine;

public class HealthEntity : MonoBehaviour
{
    [SerializeField, Min(1f)] private float maxHealth = 100f;
    [SerializeField, Min(0f)] private float currentHealth = 100f;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public bool IsAlive => currentHealth > 0f;

    private void Awake()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
    }

    public void SetMaxAndCurrent(float newMaxHealth, float newCurrentHealth)
    {
        maxHealth = Mathf.Max(1f, newMaxHealth);
        currentHealth = Mathf.Clamp(newCurrentHealth, 0f, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (!IsAlive)
        {
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - Mathf.Max(0f, amount));
    }

    public void Heal(float amount)
    {
        if (!IsAlive)
        {
            return;
        }

        currentHealth = Mathf.Min(maxHealth, currentHealth + Mathf.Max(0f, amount));
    }
}