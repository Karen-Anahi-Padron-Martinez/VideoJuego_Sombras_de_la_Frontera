using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Envoltorio ligero sobre el Animator de las criaturas (coyotes, perros, serpientes, alacranes y arañas).
/// Los estados del AnimatorController se reproducen directamente desde código (sin transiciones),
/// de modo que cada IA decide exactamente CUÁNDO se ve cada animación:
/// - Play("Walk"): cambia al estado en bucle solo si no se está reproduciendo ya.
/// - PlayOneShot("Bite", "Bark"): reproduce una animación de ataque completa y luego regresa al estado indicado.
/// </summary>
[RequireComponent(typeof(Animator))]
public class CreatureAnimator : MonoBehaviour
{
    private Animator animator;
    private int currentStateHash;
    private int returnStateHash;
    private string returnStateName = "";
    private float oneShotEndTime = -1f;
    private readonly Dictionary<int, float> clipLengths = new Dictionary<int, float>();

    public string CurrentState { get; private set; } = "";
    public bool IsPlayingOneShot => oneShotEndTime > 0f;

    private float Now => (animator != null && animator.updateMode == AnimatorUpdateMode.UnscaledTime) ? Time.unscaledTime : Time.time;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        CacheClipLengths();
    }

    private void CacheClipLengths()
    {
        clipLengths.Clear();
        if (animator == null || animator.runtimeAnimatorController == null) return;

        // Los clips se nombran "<Criatura>_<Estado>" (ej. "Bulldog_Bark")
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip == null) continue;
            int underscore = clip.name.LastIndexOf('_');
            string stateName = underscore >= 0 ? clip.name.Substring(underscore + 1) : clip.name;
            clipLengths[Animator.StringToHash(stateName)] = clip.length;
        }
    }

    public bool HasState(string stateName)
    {
        return animator != null && animator.runtimeAnimatorController != null && animator.HasState(0, Animator.StringToHash(stateName));
    }

    /// <summary>
    /// Reproduce un estado en bucle. Si hay una animación de ataque en curso, el estado queda pendiente para cuando termine.
    /// </summary>
    public void Play(string stateName)
    {
        int hash = Animator.StringToHash(stateName);

        if (IsPlayingOneShot)
        {
            returnStateHash = hash;
            returnStateName = stateName;
            return;
        }

        if (hash == currentStateHash || !HasState(stateName)) return;

        animator.Play(hash, 0, 0f);
        currentStateHash = hash;
        CurrentState = stateName;
    }

    /// <summary>
    /// Reproduce una animación una sola vez (ataque, mordida, captura) y después vuelve a returnState.
    /// </summary>
    public void PlayOneShot(string stateName, string returnState)
    {
        if (!HasState(stateName)) return;

        int hash = Animator.StringToHash(stateName);
        animator.Play(hash, 0, 0f);
        currentStateHash = hash;
        CurrentState = stateName;
        returnStateHash = Animator.StringToHash(returnState);
        returnStateName = returnState;

        float length = clipLengths.TryGetValue(hash, out float l) ? l : 0.5f;
        oneShotEndTime = Now + length;
    }

    /// <summary>
    /// Hace que las animaciones sigan corriendo aunque el juego se congele (Time.timeScale = 0),
    /// para que la mordida o captura que derrotó a Mateo se vea completa.
    /// </summary>
    public void UseUnscaledTime()
    {
        if (animator == null) return;
        float remaining = oneShotEndTime > 0f ? oneShotEndTime - Now : -1f;
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        if (remaining > 0f) oneShotEndTime = Now + remaining;
    }

    private void Update()
    {
        if (oneShotEndTime > 0f && Now >= oneShotEndTime)
        {
            oneShotEndTime = -1f;
            if (returnStateHash != 0 && animator.HasState(0, returnStateHash))
            {
                animator.Play(returnStateHash, 0, 0f);
                currentStateHash = returnStateHash;
                CurrentState = returnStateName;
            }
        }
    }
}
