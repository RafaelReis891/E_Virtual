using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Puzzle E4 — só dials. Sem bandeiras.
/// Classe com nome próprio para não conflitar com o Enigma5Manager do E5.
/// </summary>
public class Enigma4Dials : MonoBehaviour
{
    [Header("Objetos da cena")]
    public Transform dial1;
    public Transform dial2;
    public Animator doorAnimator;
    public GameObject successEffect;

    [Header("Configuração dos discos")]
    [Tooltip("Ângulos de rotação para os números 0 a 9.")]
    public float[] dialAngles = { 0f, 36f, 72f, 108f, 144f, 180f, 216f, 252f, 288f, 324f };

    [Tooltip("Índice correto do dial 1 (ex.: 5 = número 5).")]
    public int targetDial1Index = 5;

    [Tooltip("Índice correto do dial 2 (ex.: 0 = número 0).")]
    public int targetDial2Index = 0;

    [Header("Feedback")]
    public AudioClip clickSound;
    public AudioClip successSound;
    public AudioClip errorSound;
    AudioSource audioSource;

    int currentDial1Index;
    int currentDial2Index;
    bool isSolved;
    Coroutine activeRotation1;
    Coroutine activeRotation2;

    public UnityEvent OnEndGame;
    public UnityEvent OnPassNextScene;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        if (dial1 != null)
            dial1.localEulerAngles = new Vector3(0, dialAngles[0], 0);
        if (dial2 != null)
            dial2.localEulerAngles = new Vector3(0, dialAngles[0], 0);
    }

    void Update()
    {
        if (isSolved) return;

        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                ProcessClick(hit.collider.gameObject);
        }
    }

    void ProcessClick(GameObject clickedObject)
    {
        if (dial1 != null && (clickedObject.transform == dial1 || clickedObject.transform.IsChildOf(dial1)))
        {
            PlaySound(clickSound);
            currentDial1Index = (currentDial1Index + 1) % dialAngles.Length;
            RotateDial(dial1, dialAngles[currentDial1Index], ref activeRotation1);
            CheckWinCondition();
            return;
        }

        if (dial2 != null && (clickedObject.transform == dial2 || clickedObject.transform.IsChildOf(dial2)))
        {
            PlaySound(clickSound);
            currentDial2Index = (currentDial2Index + 1) % dialAngles.Length;
            RotateDial(dial2, dialAngles[currentDial2Index], ref activeRotation2);
            CheckWinCondition();
        }
    }

    void CheckWinCondition()
    {
        if (currentDial1Index == targetDial1Index && currentDial2Index == targetDial2Index)
            SolvePuzzle();
    }

    void SolvePuzzle()
    {
        isSolved = true;
        Debug.Log("E4 resolvido: dials corretos.");

        PlaySound(successSound);

        if (successEffect != null)
            successEffect.SetActive(true);

        if (doorAnimator != null)
            doorAnimator.SetTrigger("Open");

        Invoke(nameof(CallEndGame), 3f);
    }

    void CallEndGame()
    {
        OnEndGame?.Invoke();
        Invoke(nameof(PassNextSceneEvent), 3f);
    }

    void PassNextSceneEvent()
    {
        OnPassNextScene?.Invoke();
    }

    void RotateDial(Transform dial, float targetAngle, ref Coroutine activeCoroutine)
    {
        if (activeCoroutine != null)
            StopCoroutine(activeCoroutine);
        activeCoroutine = StartCoroutine(SmoothRotate(dial, targetAngle, 0.3f));
    }

    IEnumerator SmoothRotate(Transform dial, float targetYAngle, float duration)
    {
        Quaternion startRot = dial.localRotation;
        Quaternion targetRot = Quaternion.Euler(0, targetYAngle, 0);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            dial.localRotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }
        dial.localRotation = targetRot;
    }

    void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip);
    }
}
