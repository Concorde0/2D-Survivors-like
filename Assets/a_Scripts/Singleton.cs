using UnityEngine;
using System;
using Object = UnityEngine.Object;

public class Singleton<T> : MonoBehaviour where T : class
{
    private static T instance;
    private static readonly object syslock = new object();

    public static T Instance
    {
        get
        {
            if (typeof(MonoBehaviour).IsAssignableFrom(typeof(T)))
            {
                if (instance == null)
                {
                    lock (syslock)
                    {
                        if (instance == null)
                        {         
                            var existing = Object.FindObjectOfType(typeof(T));
                            if (existing != null)
                            {
                                instance = existing as T;
                            }
                            else
                            {
                                var go = new GameObject(typeof(T).Name);
                                instance = go.AddComponent(typeof(T)) as T;
                                DontDestroyOnLoad(go);
                            }
                        }
                    }
                }
            }
            else 
            {
                if (instance == null)
                {
                    lock (syslock)
                    {
                        if (instance == null)
                        {
                            instance = Activator.CreateInstance(typeof(T)) as T;
                        }
                    }
                }
            }

            return instance;
        }
    }

    public static bool IsInitialized => instance != null;

    protected virtual void Awake()
    {
        if (typeof(MonoBehaviour).IsAssignableFrom(typeof(T)))
        {
            if (instance == null)
            {
                instance = this as T;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != (this as T))
            {
                Destroy(gameObject);
            }
        }
    }

    protected virtual void OnDestroy()
    {
        if (instance == this as T)
        {
            instance = null;
        }
    }
}
