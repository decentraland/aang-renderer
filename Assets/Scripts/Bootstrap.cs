using UnityEngine;

namespace DefaultNamespace
{
    public class Bootstrap: MonoBehaviour
    {
        private void Awake()
        {
            URLParser.Initialize(Application.absoluteURL);
        }
    }
}