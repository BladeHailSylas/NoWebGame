using System;
using System.Collections.Generic;
using UnityEngine;

namespace Olds.Systems.Events
{
    public static class EventBus
    {
        private interface ISub
        {
            bool IsAlive { get; }
            bool Matches(Delegate d, object ctx);
            void Invoke(object payload);
            UnityEngine.Object Context { get; }
            Delegate Handler { get; }
        }

        private class Sub<T> : ISub
        {
            private readonly WeakReference<UnityEngine.Object> _ctx;
            public Delegate Handler { get; }
            public UnityEngine.Object Context
            {
                get { _ctx.TryGetTarget(out var o); return o; }
            }

            private readonly Action<T> _action;

            public Sub(Action<T> action, UnityEngine.Object context)
            {
                Handler = action;
                _action = action;
                _ctx = context != null ? new WeakReference<UnityEngine.Object>(context) : null;
            }

            public bool IsAlive
            {
                get
                {
                    if (_ctx == null) return true; 
                    if (!_ctx.TryGetTarget(out var o)) return false;
                    return o != null;
                }
            }

            public bool Matches(Delegate d, object ctx)
            {
                return Equals(Handler, d) && Equals(Context, ctx as UnityEngine.Object);
            }

            public void Invoke(object payload)
            {
                try
                {
                    if (!IsAlive) return;
                    _action((T)payload);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
    
        private static readonly Dictionary<(Type, object), List<ISub>> Map = new();
        private static readonly object Gate = new();
    
        public static void Subscribe<T>(Action<T> handler, UnityEngine.Object context = null, object scope = null)
        {
            if (handler == null) return;
            var key = (typeof(T), scope);
            lock (Gate)
            {
                if (!Map.TryGetValue(key, out var list))
                {
                    list = new List<ISub>(4);
                    Map[key] = list;
                }
                list.Add(new Sub<T>(handler, context));
            }
        }
    
        public static void Unsubscribe<T>(Action<T> handler, UnityEngine.Object context = null, object scope = null)
        {
            var key = (typeof(T), scope);
            lock (Gate)
            {
                if (!Map.TryGetValue(key, out var list)) return;
                for (var i = list.Count - 1; i >= 0; i--)
                {
                    if (list[i].Matches(handler, context))
                        list.RemoveAt(i);
                }
                if (list.Count == 0) Map.Remove(key);
            }
        }
    
        public static void Publish<T>(T evt, object scope = null)
        {
            var key = (typeof(T), scope);
            List<ISub> snapshot = null;

            lock (Gate)
            {
                if (Map.TryGetValue(key, out var list) && list.Count > 0)
                {
                    for (var i = list.Count - 1; i >= 0; i--)
                        if (!list[i].IsAlive) list.RemoveAt(i);
                
                    if (list.Count > 0) snapshot = new List<ISub>(list);
                    if (list.Count == 0) Map.Remove(key);
                }
            }

            if (snapshot == null) return;
        
            foreach (var sub in snapshot)
                sub.Invoke(evt);
        
            lock (Gate)
            {
                if (Map.TryGetValue(key, out var list))
                {
                    for (var i = list.Count - 1; i >= 0; i--)
                        if (!list[i].IsAlive) list.RemoveAt(i);
                    if (list.Count == 0) Map.Remove(key);
                }
            }
        }
    
        public static int Count(Type t, object scope = null)
        {
            lock (Gate)
                return Map.TryGetValue((t, scope), out var list) ? list.Count : 0;
        }
    }
}