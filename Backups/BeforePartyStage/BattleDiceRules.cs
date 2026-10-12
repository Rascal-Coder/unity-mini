using System;
using System.Linq;

public struct DiceCombination {
    public string Name;
    public float Multiplier;
    public int Sum;
    public DiceCombination(string name, float multiplier, int sum) { Name = name; Multiplier = multiplier; Sum = sum; }
}

// Pure rules, independent of frame rate, animations or presentation.
public static class BattleDiceRules {
    public static DiceCombination Evaluate(int[] dice) {
        if (dice == null || dice.Length != 5 || dice.Any(v => v < 1 || v > 6))
            throw new ArgumentException("Five dice with values 1..6 are required.");
        int sum = dice.Sum();
        var counts = dice.GroupBy(v => v).Select(g => g.Count()).OrderByDescending(v => v).ToArray();
        var unique = dice.Distinct().OrderBy(v => v).ToArray();
        if (counts[0] == 5) return new DiceCombination("五同 · 命运爆发", 4.5f, sum);
        if (counts[0] == 4) return new DiceCombination("四同 · 强袭", 3.2f, sum);
        if (unique.Length == 5 && unique[4] - unique[0] == 4) return new DiceCombination("大顺 · 连击", 3f, sum);
        if (counts[0] == 3 && counts.Length == 2) return new DiceCombination("葫芦 · 共鸣", 2.8f, sum);
        for (int i = 0; i + 3 < unique.Length; i++)
            if (unique[i + 3] - unique[i] == 3) return new DiceCombination("小顺 · 追击", 2.2f, sum);
        if (counts[0] == 3) return new DiceCombination("三同 · 重击", 2f, sum);
        if (counts.Count(c => c == 2) == 2) return new DiceCombination("两对 · 协奏", 1.8f, sum);
        if (counts[0] == 2) return new DiceCombination("一对 · 蓄力", 1.5f, sum);
        return new DiceCombination("散点 · 普攻", 1f, sum);
    }
}
