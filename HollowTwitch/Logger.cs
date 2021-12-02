namespace HollowTwitch
{
    public static class Logger
    {
        public static void Log(object obj) => CrowdControl.Instance.Log(obj);
        
        public static void LogWarn(object obj) => CrowdControl.Instance.LogWarn(obj);
        
        public static void LogError(object obj) => CrowdControl.Instance.LogError(obj);
    }
}