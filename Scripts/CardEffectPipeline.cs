using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public static class CardEffectPipeline
{
    public static async Task<bool> ExecuteAsync(
        IReadOnlyList<CardEffectSpec> effects,
        Func<CardEffectSpec, Task<bool>> executor)
    {
        for (var i = 0; i < effects.Count; i++)
        {
            if (!await executor(effects[i]))
            {
                return false;
            }
        }

        return true;
    }
}
