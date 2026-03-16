namespace Garden
{
    public static class Loc
    {
        public static string Get(string key, string fallback)
        {
            return LocalizationService.Instance != null
                ? LocalizationService.Instance.Get(key, fallback)
                : fallback;
        }
    }
}
