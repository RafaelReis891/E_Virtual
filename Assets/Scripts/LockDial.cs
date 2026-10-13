using UnityEngine;

/// <summary>
/// Controla um dial do cadeado. Gire com Interact (E) ou com scroll/setas.
/// Coloque em cada cilindro/número (Dial_1, Dial_2, Dial_3).
/// </summary>
public class LockDial : MonoBehaviour
{
    [Header("Config")]
    public int currentDigit = 0;
    public float rotationStep = 36f; // 360 / 10
    public float rotateSpeed = 180f;

    [Header("Visual")]
    public Transform numberRoot; // opcional: objeto filho com os números 0-9

    bool isRotating;
    float targetAngle;

    public int Digit => currentDigit;

    public void RotateNext()
    {
        if (isRotating) return;
        currentDigit = (currentDigit + 1) % 10;
        targetAngle = currentDigit * rotationStep;
        isRotating = true;
    }

    public void RotatePrevious()
    {
        if (isRotating) return;
        currentDigit = (currentDigit - 1 + 10) % 10;
        targetAngle = currentDigit * rotationStep;
        isRotating = true;
    }

    void Update()
    {
        if (!isRotating) return;

        float current = transform.localEulerAngles.y;
        float next = Mathf.MoveTowardsAngle(current, targetAngle, rotateSpeed * Time.deltaTime);
        transform.localEulerAngles = new Vector3(0, next, 0);

        if (Mathf.Abs(Mathf.DeltaAngle(next, targetAngle)) < 0.5f)
        {
            transform.localEulerAngles = new Vector3(0, targetAngle, 0);
            isRotating = false;
        }
    }
}
