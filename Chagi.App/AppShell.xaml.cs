namespace ITF_Taekwondo_Gradering
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute("summary", typeof(SummaryPage));
            Routing.RegisterRoute(nameof(CompetitionDetailsPage), typeof(CompetitionDetailsPage));
            Routing.RegisterRoute(nameof(CompetitionMedalLeaderboardPage), typeof(CompetitionMedalLeaderboardPage));
            Routing.RegisterRoute(nameof(DivisionMatchesPage), typeof(DivisionMatchesPage));
            Routing.RegisterRoute(nameof(RankingClassesPage), typeof(RankingClassesPage));
            Routing.RegisterRoute(nameof(CompetitorDetailsPage), typeof(CompetitorDetailsPage));
            Routing.RegisterRoute(nameof(CompetitorSearchPage), typeof(CompetitorSearchPage));
            Routing.RegisterRoute(nameof(AllCompetitorsPage), typeof(AllCompetitorsPage));
            Routing.RegisterRoute(nameof(ClubDetailsPage), typeof(ClubDetailsPage));
            Routing.RegisterRoute(nameof(ClubCompetitionDetailsPage), typeof(ClubCompetitionDetailsPage));
            Routing.RegisterRoute(nameof(StudyPage), typeof(StudyPage));
            Navigated += (_, _) => NavigationUiService.ConfigureBackButton(this);
        }
    }
}
