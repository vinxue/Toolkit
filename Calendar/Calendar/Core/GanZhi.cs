namespace Calendar.Core
{
    // Pillars of the solar-term (Ganzhi) calendar: the year changes at Lichun and the month at each of the twelve "jie".
    public static class GanZhi
    {
        private const string Stems = "甲乙丙丁戊己庚辛壬癸";
        private const string Branches = "子丑寅卯辰巳午未申酉戌亥";
        private const int LichunTerm = 21;

        // Month-opening terms in calendar order starting from Xiaohan, with the branch of the month each one starts.
        private static readonly (int Term, int Branch)[] MonthStarts =
        [
            (19, 1), (21, 2), (23, 3), (1, 4), (3, 5), (5, 6), (7, 7), (9, 8), (11, 9), (13, 10), (15, 11), (17, 0),
        ];

        public static string Day(DateOnly date) => Pair((date.DayNumber + 15) % 60);

        public static (string Year, string Month) YearAndMonth(DateOnly date)
        {
            int year = date.Year;
            int ganzhiYear = date >= ChineseCalendar.TermDate(year, LichunTerm) ? year : year - 1;
            int yearIndex = ((ganzhiYear - 4) % 60 + 60) % 60;

            // Before Xiaohan the month is still the Zi month that began at the previous Daxue.
            int branch = 0;
            foreach (var (term, monthBranch) in MonthStarts)
            {
                if (ChineseCalendar.TermDate(year, term) > date)
                {
                    break;
                }

                branch = monthBranch;
            }

            // The Yin month's stem follows from the year stem (jia/ji -> bing, yi/geng -> wu, ...).
            int yinStem = (yearIndex % 10 % 5) * 2 + 2;
            int stem = (yinStem + (branch - 2 + 12) % 12) % 10;
            return (Pair(yearIndex), $"{Stems[stem]}{Branches[branch]}");
        }

        // Double hour of the day: Zi covers 23:00-01:00, then every two hours.
        // From 23:00 the Zi hour takes its stem from the next day, so it matches the Zi hour after midnight.
        public static (string Name, string Range) Shichen(DateTime time)
        {
            int hour = time.Hour;
            int branch = (hour + 1) / 2 % 12;

            var day = DateOnly.FromDateTime(time);
            if (hour == 23)
            {
                day = day.AddDays(1);
            }

            // The Zi hour's stem follows the day stem (jia/ji -> jia, yi/geng -> bing, ...).
            int dayStem = (day.DayNumber + 15) % 60 % 10;
            int stem = (dayStem % 5 * 2 + branch) % 10;

            int start = (branch * 2 + 23) % 24;
            return ($"{Stems[stem]}{Branches[branch]}", $"{start:00}:00 \u2013 {(start + 2) % 24:00}:00");
        }

        private static string Pair(int index) => $"{Stems[index % 10]}{Branches[index % 12]}";
    }
}
