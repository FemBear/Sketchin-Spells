using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "New Health Manager", menuName = "Base/New HealthManager", order = 1)]
public class HealthManagerSO : ScriptableObject
{
    public int MaxHealth = 100;
    public int CurrentHealth = 100;

    [System.NonSerialized]
    public UnityEvent<int> healthChangedEvent;

    public virtual void OnEnable()
    {
        CurrentHealth = MaxHealth;
        if (healthChangedEvent == null)
        {
            healthChangedEvent = new UnityEvent<int>();
        }
    }

    public virtual void TakeDamage(int damage)
    {
        CurrentHealth -= damage;
        healthChangedEvent.Invoke(CurrentHealth);
        Debug.Log("Health: " + CurrentHealth + name);
    }

    public virtual void DamageOverTime(int damage, int duration)
    {
        for (int i = 0; i < duration; i++)
        {
            TakeDamage(damage);
        }
    }

    public virtual void Heal(int amount)
    {
        CurrentHealth += amount;
        if (CurrentHealth > MaxHealth)
        {
            CurrentHealth = MaxHealth;
        }
        healthChangedEvent.Invoke(CurrentHealth);
    }

    public virtual void SetMaxHealth(int amount)
    {
        MaxHealth = amount;
    }

    public virtual int GetCurrentHealth()
    {
        return CurrentHealth;
    }

    public virtual void Reset()
    {
        CurrentHealth = MaxHealth;
        healthChangedEvent.Invoke(CurrentHealth);
    }
}
