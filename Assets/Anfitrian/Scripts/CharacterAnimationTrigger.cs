using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CharacterAnimationTrigger : MonoBehaviour
{
    [Header("Ativação")]
    public bool activateOnTrigger = true;
    public string playerTag = "Player";

    [Header("Animações")]
    public bool playSequence = false;
    public string selectedAnimation;
    public List<string> animationSequence = new List<string>();
    public float delayBetweenAnimations = 0.3f;

    private Animator animator;
    private bool alreadyTriggered = false;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!activateOnTrigger || alreadyTriggered) return;

        if (other.CompareTag(playerTag))
        {
            alreadyTriggered = true;
            StartCoroutine(PlayWithDelay());
        }
    }

    IEnumerator PlayWithDelay()
    {
        // Espera 1 frame para o Animator inicializar
        yield return null;

        if (playSequence && animationSequence.Count > 0)
            StartCoroutine(PlaySequence());
        else if (!string.IsNullOrEmpty(selectedAnimation))
            ForcePlay(selectedAnimation);
    }

    void ForcePlay(string animName)
    {
        if (animator == null) return;

        animator.Play(animName, 0, 0f);
        animator.Update(0f); // força atualização imediata
        Debug.Log("Tocando animação: " + animName);
    }

    IEnumerator PlaySequence()
    {
        foreach (string animName in animationSequence)
        {
            if (string.IsNullOrEmpty(animName)) continue;

            ForcePlay(animName);
            float length = GetAnimationLength(animName);
            yield return new WaitForSeconds(length + delayBetweenAnimations);
        }
    }

    float GetAnimationLength(string animName)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return 1f;

        foreach (var clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip.name == animName)
                return clip.length;
        }
        return 1f;
    }
}