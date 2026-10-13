using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Puzzle do cadeado numérico.
/// Coloque no objeto raiz do cadeado.
/// Arraste os 3 dials e defina a combinação correta (ex.: 1, 6, 0 ou 5, 0, 0).
/// </summary>
public class CombinationLockPuzzle : MonoBehaviour
{
    [Header("Dials")]
    public LockDial dial1;
    public LockDial dial2;
    public LockDial dial3;

    [Header("Resposta")]
    public int correctDigit1 = 1;
    public int correctDigit2 = 6;
    public int correctDigit3 = 0;

    [Header("Interação")]
    public string interactKey = "e";
    public float interactDistance = 2.5f;
    public LayerMask playerLayer;

    [Header("Feedback")]
    public UnityEvent onUnlocked;
    public AudioSource audioSource;
    public AudioClip clickSound;
    public AudioClip unlockSound;
    public ParticleSystem unlockVFX;
    public Animator lockAnimator; // opcional: abre o cadeado

    bool isUnlocked;
    LockDial currentDial;
    Transform player;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
    }

    void Update()
    {
        if (isUnlocked || player == null) return;

        float dist = Vector3.Distance(transform.position, player.position);
        if (dist > interactDistance) return;

        // Seleciona dial mais próximo do olhar ou cicla com Q/E
        HandleInput();
    }

    void HandleInput()
    {
        // Exemplo simples: teclas 1/2/3 selecionam o dial, setas ou scroll giram
        if (Input.GetKeyDown(KeyCode.Alpha1)) currentDial = dial1;
        if (Input.GetKeyDown(KeyCode.Alpha2)) currentDial = dial2;
        if (Input.GetKeyDown(KeyCode.Alpha3)) currentDial = dial3;

        if (currentDial == null) currentDial = dial1;

        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetAxis("Mouse ScrollWheel") > 0.1f)
        {
            currentDial.RotateNext();
            PlayClick();
            CheckCombination();
        }
        else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetAxis("Mouse ScrollWheel") < -0.1f)
        {
            currentDial.RotatePrevious();
            PlayClick();
            CheckCombination();
        }

        // Alternativa: E gira o dial selecionado
        if (Input.GetKeyDown(KeyCode.E))
        {
            currentDial.RotateNext();
            PlayClick();
            CheckCombination();
        }
    }

    void CheckCombination()
    {
        if (dial1.Digit == correctDigit1 &&
            dial2.Digit == correctDigit2 &&
            dial3.Digit == correctDigit3)
        {
            Unlock();
        }
    }

    void Unlock()
    {
        isUnlocked = true;
        if (unlockSound && audioSource) audioSource.PlayOneShot(unlockSound);
        if (unlockVFX) unlockVFX.Play();
        if (lockAnimator) lockAnimator.SetTrigger("Unlock");
        onUnlocked?.Invoke();
        Debug.Log("Cadeado aberto!");
    }

    void PlayClick()
    {
        if (clickSound && audioSource) audioSource.PlayOneShot(clickSound);
    }

    // Chamado pelo seu PlayerController se preferir raycast
    public void InteractFromPlayer()
    {
        if (isUnlocked) return;
        if (currentDial == null) currentDial = dial1;
        currentDial.RotateNext();
        PlayClick();
        CheckCombination();
    }
}
