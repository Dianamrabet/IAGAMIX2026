using UnityEngine;
using System.Collections;

public class KnifeComboController : MonoBehaviour
{
    public Animator animator;

    public event System.Action OnComboFinished;

    [Header("Debug")]
    public bool enableDebugLogs = true;

    private bool isAttacking = false;

    [ContextMenu("Play Combo 1")]
    public void PlayCombo1() 
    { 
        if (isAttacking) return; // Prevent overlapping combos
        isAttacking = true;
        if (enableDebugLogs) Debug.Log("[KnifeCombo] Starting Combo 1");
        StartCoroutine(PlayCombo(new string[] { "Knife_Light_Melee01", "Knife_Light_Melee02", "Knife_Light_Melee03" })); 
    }

    [ContextMenu("Play Combo 2")]
    public void PlayCombo2() 
    { 
        if (isAttacking) return; // Prevent overlapping combos
        isAttacking = true;
        if (enableDebugLogs) Debug.Log("[KnifeCombo] Starting Combo 2");
        StartCoroutine(PlayCombo(new string[] { "Knife_Light_Melee04", "Knife_Light_Melee05", "Knife_Hard_Melee01" })); 
    }

    [ContextMenu("Play Combo 3")]
    public void PlayCombo3() 
    { 
        if (isAttacking) return; // Prevent overlapping combos
        isAttacking = true;
        if (enableDebugLogs) Debug.Log("[KnifeCombo] Starting Combo 3");
        StartCoroutine(PlayCombo(new string[] { "Knife_Light_Melee06", "Knife_Hard_Melee02", "Knife_Hard_Melee03" })); 
    }

    // New: Randomly select and play a combo
    public void PlayRandomCombo(Transform target)
    {
        Health h = target.GetComponent<Health>();
        if (h != null) h.DoDamage(30);
        int rand = Random.Range(1, 4);
        switch (rand)
        {
            case 1: PlayCombo1(); break;
            case 2: PlayCombo2(); break;
            case 3: PlayCombo3(); break;
        }
        if (enableDebugLogs) Debug.Log($"[KnifeCombo] Starting random Combo {rand}");
    }

    // New: Check if currently attacking
    public bool IsAttacking()
    {
        return isAttacking;
    }

    private IEnumerator PlayCombo(string[] combo)
    {
        foreach (string anim in combo)
        {
            animator.Play(anim);
            yield return new WaitForSeconds(GetAnimationLength(anim));
        }

        // 🔔 Notify listeners and reset flag
        isAttacking = false;
        OnComboFinished?.Invoke();
        if (enableDebugLogs) Debug.Log("[KnifeCombo] Combo finished.");
    }

    private float GetAnimationLength(string animName)
    {
        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        foreach (AnimationClip clip in clips)
        {
            if (clip.name == animName)
                return clip.length;
        }
        return 0.5f; // fallback
    }
}