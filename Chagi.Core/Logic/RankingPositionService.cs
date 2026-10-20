namespace ITF_Taekwondo_Gradering.Logic;

public static class RankingPositionService
{
    public static IOrderedEnumerable<T> OrderByScoreAndMedals<T>(
        IEnumerable<T> items,
        Func<T, int> scoreSelector,
        Func<T, int> goldSelector,
        Func<T, int> silverSelector,
        Func<T, int> bronzeSelector)
    {
        return items
            .OrderByDescending(scoreSelector)
            .ThenByDescending(goldSelector)
            .ThenByDescending(silverSelector)
            .ThenByDescending(bronzeSelector);
    }

    public static bool HaveSameScoreAndMedals<T>(
        T left,
        T right,
        Func<T, int> scoreSelector,
        Func<T, int> goldSelector,
        Func<T, int> silverSelector,
        Func<T, int> bronzeSelector)
    {
        return scoreSelector(left) == scoreSelector(right) &&
               goldSelector(left) == goldSelector(right) &&
               silverSelector(left) == silverSelector(right) &&
               bronzeSelector(left) == bronzeSelector(right);
    }

    public static void ApplySharedPositions<T>(
        IList<T> items,
        Func<T, T, bool> sameRank,
        Action<T, int> setPosition,
        Action<T, string>? setPositionLabel = null)
    {
        var currentPosition = 0;

        for (var index = 0; index < items.Count; index++)
        {
            if (index == 0)
            {
                currentPosition = 1;
            }
            else if (!sameRank(items[index], items[index - 1]))
            {
                currentPosition = index + 1;
            }

            setPosition(items[index], currentPosition);
            setPositionLabel?.Invoke(items[index], currentPosition.ToString());
        }
    }
}
