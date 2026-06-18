// Helpers/AmountInWordsHelper.cs
using Humanizer;

namespace EINVWORLD.Helpers
{
    public static class AmountInWordsHelper
    {
        public static string ToWordsEnglish(decimal amount)
        {
            long ringgit = (long)Math.Floor(amount);
            int sen = (int)((amount - ringgit) * 100);

            if (sen > 0)
                return $"{ringgit.ToWords().Transform(To.TitleCase)} And {sen.ToWords().Transform(To.TitleCase)} Cents Only";

            return $"{ringgit.ToWords().Transform(To.TitleCase)} Only";
        }
    }
}
