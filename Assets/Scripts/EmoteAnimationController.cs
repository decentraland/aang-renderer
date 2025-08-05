using System;
using JetBrains.Annotations;
using UnityEngine;

public class EmoteAnimationController : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;

    public event Action EmoteAnimationEnded;

    [CanBeNull] public Animation EmotePropAnimation { get; set; }
    public AudioClip EmoteAudioClip { get; set; }

    [UsedImplicitly]
    public void EmoteStarted()
    {
        if (EmoteAudioClip != null)
        {
            audioSource.PlayOneShot(EmoteAudioClip);
        }

        if (EmotePropAnimation != null)
        {
            EmotePropAnimation.Play();
        }
    }

    [UsedImplicitly]
    public void EmoteEnded()
    {
        EmoteAnimationEnded?.Invoke();
    }

    public void Reset()
    {
        audioSource.Stop();
        if (EmotePropAnimation != null)
        {
            EmotePropAnimation.Rewind();
            EmotePropAnimation.Sample();
            EmotePropAnimation.Stop();
        }
    }
}