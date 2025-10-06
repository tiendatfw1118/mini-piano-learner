using System.Collections.Generic;
using UnityEngine;

public interface IPoolable { void OnSpawned(); void OnDespawned(); }

public sealed class ObjectPool : MonoBehaviour
{
    public GameObject prefab; public int initialSize = 64; public bool expandable = true;
    readonly Queue<GameObject> _q = new Queue<GameObject>();
    void Awake(){ Warm(initialSize); }
    public void Warm(int count){ for(int i=0;i<count;i++){ var go=Instantiate(prefab,transform); go.SetActive(false); _q.Enqueue(go);} }
    public GameObject Get(Transform parent=null)
    {
        GameObject go=null; if (_q.Count>0) go=_q.Dequeue(); else if (expandable) go=Instantiate(prefab);
        if (!go) return null; if (parent) go.transform.SetParent(parent,false); go.SetActive(true);
        go.GetComponent<IPoolable>()?.OnSpawned(); return go;
    }
    public void Release(GameObject go){ go.GetComponent<IPoolable>()?.OnDespawned(); go.SetActive(false); go.transform.SetParent(transform,false); _q.Enqueue(go); }
}
