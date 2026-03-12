using UnityEngine;

/// <summary>
/// Plays audio and optional particle effects in response to GrappleRuntimeEvents.
///
/// Add to any scene GameObject and assign AudioClip references in the Inspector.
/// Particle systems are optional — leave unassigned to skip VFX.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class ImpactFeedback : MonoBehaviour
{
    [Header("Audio Clips")]
    [SerializeField] AudioClip hookFireClip;
    [SerializeField] AudioClip hookAttachClip;
    [SerializeField] AudioClip hookMissClip;
    [SerializeField] AudioClip chainReleaseClip;
    [SerializeField] AudioClip monsterHitClip;
    [SerializeField] AudioClip monsterDefeatedClip;

    [Header("Volume")]
    [SerializeField] float fireVolume     = 0.6f;
    [SerializeField] float attachVolume   = 1.0f;
    [SerializeField] float missVolume     = 0.4f;
    [SerializeField] float releaseVolume  = 0.7f;
    [SerializeField] float hitVolume      = 1.0f;
    [SerializeField] float defeatVolume   = 1.0f;

    [Header("Optional VFX")]
    [SerializeField] ParticleSystem attachEffect;  // spawned at attach point
    [SerializeField] ParticleSystem monsterHitEffect;

    AudioSource _audioSource;

    void Awake() => _audioSource = GetComponent<AudioSource>();

    void OnEnable()
    {
        GrappleRuntimeEvents.OnHookFired      += OnHookFired;
        GrappleRuntimeEvents.OnHookAttached   += OnHookAttached;
        GrappleRuntimeEvents.OnHookMissed     += OnHookMissed;
        GrappleRuntimeEvents.OnChainReleased  += OnChainReleased;
        GrappleRuntimeEvents.OnMonsterHit     += OnMonsterHit;
        GrappleRuntimeEvents.OnMonsterDefeated+= OnMonsterDefeated;
    }

    void OnDisable()
    {
        GrappleRuntimeEvents.OnHookFired      -= OnHookFired;
        GrappleRuntimeEvents.OnHookAttached   -= OnHookAttached;
        GrappleRuntimeEvents.OnHookMissed     -= OnHookMissed;
        GrappleRuntimeEvents.OnChainReleased  -= OnChainReleased;
        GrappleRuntimeEvents.OnMonsterHit     -= OnMonsterHit;
        GrappleRuntimeEvents.OnMonsterDefeated-= OnMonsterDefeated;
    }

    void OnHookFired(Hand _)            => Play(hookFireClip,      fireVolume);
    void OnHookMissed(Hand _)           => Play(hookMissClip,      missVolume);
    void OnChainReleased(Hand _)        => Play(chainReleaseClip,  releaseVolume);
    void OnMonsterDefeated()            => Play(monsterDefeatedClip, defeatVolume);

    void OnHookAttached(Hand _, Vector3 worldPos)
    {
        Play(hookAttachClip, attachVolume);
        if (attachEffect != null)
        {
            attachEffect.transform.position = worldPos;
            attachEffect.Play();
        }
    }

    void OnMonsterHit(int _)
    {
        Play(monsterHitClip, hitVolume);
        monsterHitEffect?.Play();
    }

    void Play(AudioClip clip, float volume)
    {
        if (clip == null || _audioSource == null) return;
        _audioSource.PlayOneShot(clip, volume);
    }
}
