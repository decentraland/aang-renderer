using System.Collections.Generic;
using UnityEngine;

public class AvatarRoot: MonoBehaviour
{
    public readonly Dictionary<string, GameObject> Categories = new();
    
    public void Attach(string category, GameObject go)
    {
        // TODO: Maybe check for existing attachment and destroy it?
        
        go.transform.SetParent(transform, false);
        Categories[category] = go;
    }

    public void Clear()
    {
        foreach (var (_, go) in Categories)
        {
            Destroy(go);
        }
        
        Categories.Clear();
    }
}