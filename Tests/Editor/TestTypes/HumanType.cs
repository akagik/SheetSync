using System;

namespace SheetSync.Tests.TestTypes
{
    [Flags]
    public enum HumanType
    {
        None = 0,
        Player = 1 << 0, // 1
        Enemy = 1 << 1, // 2
        NPC = 1 << 2, // 4
        Ally = 1 << 3, // 8
        Neutral = 1 << 4, // 16
        All = Player | Enemy | NPC | Ally | Neutral // 全てのタイプを含む
    }
}