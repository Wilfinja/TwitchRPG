using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A thread-safe class which holds a queue of actions to execute on the next Update() method call in Unity.
/// This is needed because EventSub callbacks happen on background threads, but Unity operations must be on the main thread.
/// </summary>
public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static readonly Queue<Action> _executionQueue = new Queue<Action>();
    private static UnityMainThreadDispatcher _instance = null;

    public static UnityMainThreadDispatcher Instance()
    {
        if (_instance == null)
        {
            throw new Exception("UnityMainThreadDispatcher could not find the UnityMainThreadDispatcher object. Please ensure you have added it to your scene.");
        }

        return _instance;
    }

    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        lock (_executionQueue)
        {
            while (_executionQueue.Count > 0)
            {
                _executionQueue.Dequeue().Invoke();
            }
        }
    }

    /// <summary>
    /// Locks the queue and adds the Action to the queue
    /// </summary>
    /// <param name="action">function that will be executed from the main thread.</param>
    public void Enqueue(Action action)
    {
        if (action == null) return;
        lock (_executionQueue)
        {
            _executionQueue.Enqueue(action);
        }
    }

    /// <summary>
    /// Enqueue a coroutine to be executed on the main thread.
    /// </summary>
    public void EnqueueCoroutine(IEnumerator coroutine)
    {
        lock (_executionQueue)
        {
            _executionQueue.Enqueue(() => StartCoroutine(coroutine));
        }
    }
}
