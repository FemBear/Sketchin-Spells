using System;
using System.IO;
using UnityEngine;
using UnityEngine.Events;

public class Player : MonoBehaviour
{
    [SerializeField]
    public PlayerHealthManagerSO Health;

    [SerializeField]
    public ManaManagerSO Mana;
    private SpriteRenderer _spriteRenderer;
    public UnityEvent OnPlayerDeath = new UnityEvent();

    internal string PlayerName = "Player";
    internal string PlayerDescription = "This is you";

    private void Start()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        Health.PlayerhealthChangedEvent.AddListener(Death);
    }

    internal void LoadSprite()
    {
        string path = Path.Combine(
            Application.persistentDataPath,
            "sketches",
            "Player",
            "Player" + ".png"
        );
        if (File.Exists(path))
        {
            byte[] fileData = File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(fileData);
            Sprite sprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0, 0)
            );
            _spriteRenderer.sprite = sprite;
            Debug.Log("Player sprite loaded");
        }
    }

    private void Death(int health)
    {
        if (health <= 0)
        {
            OnPlayerDeath.Invoke();
        }
    }

    public void Reset()
    {
        Health.Reset();
        Mana.Reset();
        _spriteRenderer.sprite = null;
    }

    public void ResetHealthAndMana()
    {
        //create a new instance of the health and mana
        Health = ScriptableObject.CreateInstance<PlayerHealthManagerSO>();
        Mana = ScriptableObject.CreateInstance<ManaManagerSO>();
        Health.OnEnable();
        Mana.OnEnable();
    }
}
