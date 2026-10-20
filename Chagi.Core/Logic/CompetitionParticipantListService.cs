namespace ITF_Taekwondo_Gradering.Logic;

public static class CompetitionParticipantListService
{
    public static CompetitionParticipantList Build(IEnumerable<CompetitionPlacementEntry> results)
    {
        var ordered = results.OrderBy(result => result.Placement > 0 ? result.Placement : int.MaxValue).ToList();
        if (ordered.Count <= 4)
            return new(ordered, []);

        // Keep every medal winner visible, including tied places and both bronzes.
        return new(ordered.Where(result => result.Placement is >= 1 and <= 3).ToList(),
            ordered.Where(result => result.Placement is not (>= 1 and <= 3)).ToList());
    }
}

public sealed record CompetitionParticipantList(
    IReadOnlyList<CompetitionPlacementEntry> Visible,
    IReadOnlyList<CompetitionPlacementEntry> Hidden);
