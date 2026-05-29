using System;
using UnityEngine;

[Serializable]
public class ComboPattern
{
    public string comboName;
    public AttackData[] attacks;

    [Tooltip("Min counter opportunities after this combo finishes.")]
    public int minCounterHits = 1;
    [Tooltip("Max counter opportunities after this combo finishes.")]
    public int maxCounterHits = 3;

    public int RollCounterHits()
        => UnityEngine.Random.Range(minCounterHits, maxCounterHits + 1);
}
