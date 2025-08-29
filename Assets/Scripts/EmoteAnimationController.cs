using System;
using JetBrains.Annotations;
using Loading;
using UnityEngine;

public class EmoteAnimationController : MonoBehaviour
{
    private const string IDLE_CLIP_NAME = "Idle";

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Animation avatarAnimation;

    public bool HasAudio => _emoteAudioClip != null;
    public event Action EmoteAnimationEnded;

    private LoadedEmote? _loadedEmote;
    private Animation _propAnimation;
    private AudioClip _emoteAudioClip;

    public void PlayEmote(LoadedEmote loadedEmote)
    {
        _loadedEmote = loadedEmote;

        avatarAnimation.AddClip(_loadedEmote.Value.Clip, _loadedEmote.Value.Entity.URN);

        // Add prop / audio trigger
        _loadedEmote.Value.Clip.events = Array.Empty<AnimationEvent>();

        var clip = avatarAnimation.GetClip(_loadedEmote.Value.Entity.URN);
        clip.AddEvent(new AnimationEvent
        {
            time = 0,
            functionName = "EmoteStarted"
        });
        clip.AddEvent(new AnimationEvent
        {
            time = clip.length - 0.1f,
            functionName = "EmoteEnded"
        });

        _propAnimation = _loadedEmote.Value.PropAnim;
        _emoteAudioClip = _loadedEmote.Value.Audio;

        ReplayEmote();
    }

    public void ReplayEmote()
    {
        if (!_loadedEmote.HasValue) return;

        // Prop
        if (_propAnimation != null)
        {
            _propAnimation.Rewind();
            _propAnimation.Sample();
            _propAnimation.Play();
        }

        _loadedEmote.Value.Prop?.SetActive(true);

        // Crossfade
        avatarAnimation.Rewind(_loadedEmote.Value.Entity.URN);
        avatarAnimation.Play(_loadedEmote.Value.Entity.URN);

        if (_loadedEmote.Value.Clip.wrapMode != WrapMode.Loop)
        {
            avatarAnimation.CrossFadeQueued(IDLE_CLIP_NAME, 0.3f);
        }
    }

    public void StopEmote(bool withCrossFade = true)
    {
        if (!_loadedEmote.HasValue) return;

        audioSource.Stop();
        if (_propAnimation != null)
        {
            _loadedEmote.Value.Prop?.SetActive(false);
        }

        if (withCrossFade)
        {
            avatarAnimation.CrossFade(IDLE_CLIP_NAME, 0.3f, PlayMode.StopAll);
        }
        else
        {
            avatarAnimation.Play(IDLE_CLIP_NAME, PlayMode.StopAll);
        }
    }

    public void ClearEmote()
    {
        if (!_loadedEmote.HasValue) return;

        StopEmote(false);
        avatarAnimation.RemoveClip(_loadedEmote.Value.Clip);
        _loadedEmote = null;
        _emoteAudioClip = null;
        _propAnimation = null;
        audioSource.clip = null;
    }

    [UsedImplicitly]
    private void EmoteStarted()
    {
        if (_emoteAudioClip != null)
        {
            audioSource.PlayOneShot(_emoteAudioClip);
        }

        if (_propAnimation != null)
        {
            _propAnimation.Rewind();
            _propAnimation.Sample();
            _propAnimation.Play();
        }
    }

    [UsedImplicitly]
    private void EmoteEnded()
    {
        if (_loadedEmote.HasValue && _loadedEmote.Value.Clip.wrapMode != WrapMode.Loop)
        {
            EmoteAnimationEnded?.Invoke();
            _loadedEmote.Value.Prop?.SetActive(false);
        }
    }
}