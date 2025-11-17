using UnityEngine;

public class WeaponSway : MonoBehaviour
{
    [Header("Sway Settings")]
    public float swayAmount = 0.03f;
    public float swaySmooth = 8f;

    [Header("Kick Settings")]
    public float kickAmount = 0.3f;
    public float kickReturnSpeed = 6f;

    private Vector3 initialLocalPos;
    private Vector3 currentKickOffset;
    private Vector3 swayVelocity;

    void Start()
    {
        initialLocalPos = transform.localPosition;
    }

    void Update()
    {
        HandleSway();
        HandleKickback();
    }

    void HandleSway()
    {
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        Vector3 targetSway = new Vector3(-mouseX, -mouseY, 0) * swayAmount;
        transform.localPosition = Vector3.SmoothDamp(
            transform.localPosition,
            initialLocalPos + targetSway + currentKickOffset,
            ref swayVelocity,
            1f / swaySmooth
        );
    }

    void HandleKickback()
    {
        currentKickOffset = Vector3.Lerp(currentKickOffset, Vector3.zero, Time.deltaTime * kickReturnSpeed);
    }

    public void Kick()
    {
        currentKickOffset += new Vector3(0, 0, -kickAmount);
    }
}
