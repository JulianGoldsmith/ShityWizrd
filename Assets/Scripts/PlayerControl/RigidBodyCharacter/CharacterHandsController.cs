using System;
using UnityEngine;
using UnityEngine.XR;

public class CharacterHandsController : MonoBehaviour
{
    [Serializable]
    public class Hand
    {

        public Transform rootTransform;

        [HideInInspector] public float currentYaw;
        [HideInInspector] public float currentPitch;
    }

    public Hand leftHand;
    public Hand rightHand;


    [SerializeField] private Transform cameraTransform;

 
    [SerializeField] private float yawSmoothing = 10f;

    [SerializeField] private float pitchSmoothing = 10f;

    public Rigidbody playerRb;

    private void Awake()
    {
        if (cameraTransform == null) cameraTransform = Camera.main.transform;

        Vector3 initialEuler = cameraTransform.eulerAngles;
        leftHand.currentYaw = initialEuler.y;
        leftHand.currentPitch = initialEuler.x;
        rightHand.currentYaw = initialEuler.y;
        rightHand.currentPitch = initialEuler.x;
    }


    public void DriveFromView(float targetPitch, float targetYaw, float dt)
    {
        UpdateHand(leftHand, targetPitch, targetYaw, dt);
        UpdateHand(rightHand, targetPitch, targetYaw, dt);
    }

    private void UpdateHand(Hand hand, float targetPitch, float targetYaw, float dt)
    {
        if (hand.rootTransform == null) return;

        hand.currentYaw = Mathf.LerpAngle(hand.currentYaw, targetYaw, dt * yawSmoothing);
        hand.currentPitch = Mathf.LerpAngle(hand.currentPitch, targetPitch, dt * pitchSmoothing);

        hand.rootTransform.rotation = Quaternion.Euler(hand.currentPitch, hand.currentYaw, 0f);
    }
}