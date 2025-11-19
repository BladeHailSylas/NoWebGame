using System;
using System.Collections.Generic;
using UnityEngine;

namespace Olds.Systems.Intents
{
    public class IntentCollector
    {
        private List<IIntent> _intentCluster = new();
        public static IntentCollector Instance { get; private set; }
        public bool QueueIntent(IIntent intent)
        {
            Debug.Log("Collected garbage");
            try
            {
                if (intent.Type == IntentType.Cast || intent.Type == IntentType.Move) _intentCluster.Add(intent);
                else throw new UndefinedIntentTypeException($"Cannot find such Intent Type: {intent.Type}. Probably typo.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError("Error in QueueIntent: " + ex.Message);
                return false;
            }
        }

        public IntentCollector()
        {
            Debug.Log("Let's collect garbage");
            Instance = this;
        }
        public void TickHandler(ushort tick) //We can't use TickHandler here since this is not a MonoBehaviour
        {
            Debug.Log($"Resolving Garbage at {tick}");
            if (_intentCluster.Count > 0)
            {
            }
            _intentCluster.Clear();
        }
    }

    public class UndefinedIntentTypeException : Exception
    {
        public UndefinedIntentTypeException()
        {
            
        }

        public UndefinedIntentTypeException(string msg) : base(msg)
        {
            
        }
    }
}