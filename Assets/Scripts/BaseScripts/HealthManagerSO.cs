using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(
    fileName = "New Health Manager",
    menuName = "Base/New HealthManager",
    order = 1
)]
public class HealthManagerSO : ScriptableObject
{
    
    public int b_maxHealth = 100;
    public int b_currentHealth = 100;
    [System.NonSerialized]
    public UnityEvent<int> healthChangedEvent;
    public void OnEnable()
    {
        b_currentHealth = b_maxHealth;
        if (healthChangedEvent == null)
        {
            healthChangedEvent = new UnityEvent<int>();
        }
    }

    public void TakeDamage(int damage)
    {
        b_currentHealth -= damage;
        healthChangedEvent.Invoke(b_currentHealth);
        Debug.Log("Health: " + b_currentHealth);
    }

    public void DamageOverTime(int damage, int duration)
    {
        for (int i = 0; i < duration; i++)
        {
            TakeDamage(damage);
        }
    }
    

    public void Heal(int amount)
    {
        b_currentHealth += amount;
        if (b_currentHealth > b_maxHealth)
        {
            b_currentHealth = b_maxHealth;
        }
        healthChangedEvent.Invoke(b_currentHealth);
    }

    public void SetMaxHealth(int amount)
    {
        b_maxHealth = amount;
    }

    public int GetCurrentHealth()
    {
        return b_currentHealth;
    }
}
