namespace CalendarApp.Core
{
    // US rule: weeks start on Sunday; the week containing January 1 is week 1.
    public static class WeekNumber
    {
        public static int Of(DateOnly date)
        {
            var sunday = date.AddDays(-(int)date.DayOfWeek);

            if (sunday.AddDays(6) >= new DateOnly(sunday.Year + 1, 1, 1))
            {
                return 1;
            }

            var jan1 = new DateOnly(sunday.Year, 1, 1);
            var firstSunday = jan1.AddDays(-(int)jan1.DayOfWeek);
            return (sunday.DayNumber - firstSunday.DayNumber) / 7 + 1;
        }
    }
}
