using UnityEngine;
using UnityEngine.Events;

public class Health : MonoBehaviour
{
    [SerializeField] private int maxHealth = 100;
    public int currentHealth;

    [Tooltip("called when the object takes damage, new health and max health")]
    public UnityEvent<int, int> OnHealthChanged;

    [Tooltip("called on any amount of damage")]
    public UnityEvent OnDamaged;

    [Tooltip("called when health reaches zero")]
    public UnityEvent OnDie;

    private void Awake()
    {
        currentHealth = maxHealth;
    }


    public void TakeDamage(int damageAmount)
    {
        if (currentHealth <= 0) return;

        currentHealth -= damageAmount;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnDamaged?.Invoke();

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
    }

    private void Die()
    {
        Debug.Log($"{name} has died.");
        OnDie?.Invoke();
    }
}
