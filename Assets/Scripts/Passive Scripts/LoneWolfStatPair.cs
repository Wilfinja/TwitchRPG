using UnityEngine;

[System.Serializable]
public class LoneWolfStatPair
{
    public BoostableStat stat;
    [Range(0f, 1f)]
    [Tooltip("Bonus as a fraction of base stat. 0.2 = +20%.")]
    public float bonusPercent = 0.2f;
}
