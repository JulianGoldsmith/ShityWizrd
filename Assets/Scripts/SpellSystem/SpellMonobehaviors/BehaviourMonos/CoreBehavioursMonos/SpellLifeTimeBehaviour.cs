using UnityEngine;

public class SpellLifeTimeBehaviour : SpellBehaviour
{
    public float lifeTime = 5;
    public bool useCastDuration = false; //Use the duration of the cast press (ie for  chanelled spells)

    public void Init(float _lifetime, bool _useCastDuration, SpellTriggerInfo _triggerInfor)
    {
        triggerInfo = _triggerInfor;
        lifeTime = _lifetime;
        useCastDuration = _useCastDuration;
    }

    private void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    private void Update()
    {
        if (useCastDuration)
        {
            if (!triggerInfo.State.isHeld)
            {
                Destroy(gameObject);
            }
        }
    }
}
