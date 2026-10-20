using ITF_Taekwondo_Gradering.Models.Ranking;

namespace ITF_Taekwondo_Gradering.Logic
{
    public static class RankingClassFilterService
    {
        const string StorageKey = "ranking_enabled_age_classes.v1";
        static readonly object Sync = new();
        static HashSet<RankingAgeClassE>? _enabledClasses;
        static int _changeVersion;

        public static IReadOnlyCollection<RankingAgeClassE> GetEnabledClasses()
        {
            lock (Sync) return LoadEnabledClasses().ToArray();
        }

        static HashSet<RankingAgeClassE> LoadEnabledClasses()
        {
            if (_enabledClasses != null)
                return _enabledClasses;

            try
            {
                var raw = Chagi.Core.Storage.CorePreferences.Current.Get(StorageKey, string.Empty);
                if (!Chagi.Core.Storage.CorePreferences.Current.ContainsKey(StorageKey))
                {
                    _enabledClasses = Enum.GetValues<RankingAgeClassE>().ToHashSet();
                    return _enabledClasses;
                }

                // An explicitly saved empty selection differs from first launch.
                if (string.IsNullOrWhiteSpace(raw))
                    return _enabledClasses = new HashSet<RankingAgeClassE>();

                var parsed = raw
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => Enum.TryParse<RankingAgeClassE>(x, out var value) ? value : (RankingAgeClassE?)null)
                    .Where(x => x.HasValue && Enum.IsDefined(x.Value))
                    .Select(x => x!.Value)
                    .ToHashSet();

                _enabledClasses = parsed.Count > 0
                    ? parsed
                    : Enum.GetValues<RankingAgeClassE>().ToHashSet();
            }
            catch
            {
                _enabledClasses = Enum.GetValues<RankingAgeClassE>().ToHashSet();
            }

            return _enabledClasses;
        }

        public static bool IsEnabled(RankingAgeClassE ageClass)
        {
            lock (Sync) return LoadEnabledClasses().Contains(ageClass);
        }

        public static void SetEnabled(RankingAgeClassE ageClass, bool isEnabled)
        {
            lock (Sync)
            {
                var enabled = LoadEnabledClasses().ToHashSet();
                var changed = isEnabled ? enabled.Add(ageClass) : enabled.Remove(ageClass);
                if (!changed) return;
                Chagi.Core.Storage.CorePreferences.Current.Set(StorageKey, string.Join(",", enabled.OrderBy(x => x).Select(x => x.ToString())));
                _enabledClasses = enabled;
                _changeVersion++;
            }
        }

        public static int GetChangeVersion()
        {
            lock (Sync) return _changeVersion;
        }

        public static string ToDisplayName(RankingAgeClassE ageClass)
        {
            return ageClass switch
            {
                RankingAgeClassE.YngreMiniorer => "Yngre Miniorer",
                RankingAgeClassE.Miniorer => "Miniorer",
                RankingAgeClassE.Kadetter => "Kadetter",
                RankingAgeClassE.YngreJuniorer => "Yngre Juniorer",
                RankingAgeClassE.Juniorer => "Juniorer",
                _ => ageClass.ToString(),
            };
        }

        public static string ToSingularDisplayName(RankingAgeClassE ageClass)
        {
            return ageClass switch
            {
                RankingAgeClassE.YngreMiniorer => "Yngre Minior",
                RankingAgeClassE.Miniorer => "Minior",
                RankingAgeClassE.Kadetter => "Kadett",
                RankingAgeClassE.YngreJuniorer => "Yngre Junior",
                RankingAgeClassE.Juniorer => "Junior",
                _ => ageClass.ToString(),
            };
        }
    }
}
