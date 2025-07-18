using JetBrains.Annotations;
using UnityEngine;

public class AudioEventReceiver : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;

    [UsedImplicitly]
    public void Play()
    {
        audioSource.Play();
    }
}