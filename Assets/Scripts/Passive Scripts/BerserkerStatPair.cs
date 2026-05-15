using UnityEngine;

[System.Serializable]
public class BerserkerStatPair
{
    public BoostableStat stat;
    [Tooltip("Bonus percent of base stat gained per 1% of max HP missing.\n" +
             "e.g. 0.5 = +0.5% STR per 1% HP missing → at 50% HP = +25% STR")]
    public float bonusPercentPerMissingPercent = 0.5f;
}
