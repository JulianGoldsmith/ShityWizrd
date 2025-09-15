using UnityEngine;

public class EnemyAnimationController : GenericAnimationController
{
    private void Awake()
    {
        if(GetComponent<Animator>() != null) animator = GetComponent<Animator>();
        if (castActionController == null) castActionController = transform.GetComponent<EnemyCastActionController>();

    }
}
