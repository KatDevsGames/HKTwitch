using System.IO;

namespace HollowTwitch
{
    public static class Logger
    {
        public static void Log(object obj) => File.AppendAllText("CrowdControl.log", obj.ToString());//CrowdControl.Instance.Log(obj);

        public static void LogWarn(object obj) => File.AppendAllText("CrowdControl.log", obj.ToString());// CrowdControl.Instance.LogWarn(obj);

        public static void LogError(object obj) => File.AppendAllText("CrowdControl.log", obj.ToString());// CrowdControl.Instance.LogError(obj);
    }
}