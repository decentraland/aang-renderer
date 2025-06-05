using JetBrains.Annotations;
using UnityEngine;

public class AudioEventReceiver : MonoBehaviour
{
    public AudioSource AudioSource { get; set; }

    [UsedImplicitly]
    public void Play()
    {
        AudioSource.Play();
    }
}