using UnityEngine;

public class Health : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    public float regenRate = 5f; // Health per second
    public float regenDelay = 3f; // Seconds without damage to start regen

    private float currentHealth;
    private float lastDamageTime;

    void Start()
    {
        currentHealth = maxHealth;
        lastDamageTime = -regenDelay; // Allow immediate regen if no prior damage
    }

    void Update()
    {
        // Regeneration logic
        if (Time.time - lastDamageTime > regenDelay)
        {
            currentHealth += regenRate * Time.deltaTime;
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        }

        // Check for death
        if (currentHealth <= 0f)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Apply damage to the health. Resets regen timer.
    /// </summary>
    /// <param name="damage">Amount of damage to apply</param>
    public void DoDamage(float damage)
    {
        if (damage < 0f) return; // Ignore heals (add separate heal method if needed)

        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        lastDamageTime = Time.time;

        // Optional: Trigger events like OnDamageTaken here
        // e.g., UnityEvent onDamageTaken?.Invoke();
    }

    /// <summary>
    /// Instantly kill the object by setting health to 0.
    /// </summary>
    public void InstaKill()
    {
        currentHealth = 0f;
        lastDamageTime = Time.time; // Reset timer, though irrelevant for dead object
    }

    public float GetCurrentHealth() => currentHealth;
    public float GetMaxHealth() => maxHealth;
    public float GetHealthPercentage() => currentHealth / maxHealth;
}