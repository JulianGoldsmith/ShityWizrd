using UnityEngine;

public class PickUpable : MonoBehaviour
{
    public HandState heldHandState;


    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

public enum PickUp_Hands
{
    ONE_HAND , TWO_HAND
}
public enum PickUp_Rotation_Method
{
    NONE, 
    Y_AXIS, Z_AXIS, X_AXIS, 
    FREE, 
    FREE_RAYCAST_TO_CENTER
}