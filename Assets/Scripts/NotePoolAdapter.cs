using UnityEngine;
public sealed class NotePoolAdapter : MonoBehaviour
{
    public NoteSpawner spawner; public ObjectPool pool;
    void Awake(){ if(!spawner) spawner = GetComponent<NoteSpawner>(); }
    public GameObject Spawn(Transform parent){ return pool ? pool.Get(parent) : Instantiate(spawner.notePrefab, parent); }
    public void Despawn(GameObject go){ if (pool) pool.Release(go); else Destroy(go); }
}
