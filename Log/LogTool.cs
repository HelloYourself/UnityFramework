
using System;
using System.Reflection;
using UnityEngine;

public class LogTool 
{
    /// <summary>
    /// 日志等级
    /// </summary>
    public static LogLevel LogLevel;    

    public static void Log(object message)
    {
        Debug.Log(message);
    }

    public static void LogWarning(object message)
    {
        Debug.LogWarning(message);
    }

    public static void LogError(object message)
    {
        Debug.LogError(message);
    }

    public static void NetLog(object message)
    {
        Debug.Log("<color=#2EB000>" + message+ "</color>");
    }

    public static void NetLogError(object message)
    {
        Debug.LogError("<color=#FF0000>" + message + "</color>");
    }

    public static void NetLog(int serial, Type type, object msg,out int errorCode)
    {
        bool showError = false;
        PropertyInfo property = type.GetProperty("ErrCode");
        if (property != null)
        {
            errorCode = (int)property.GetValue(msg);
            showError = errorCode != 0;
        }
        else
        {
            errorCode = 0;
        }
        if (showError)
        {
            LogTool.NetLogError($"[ack={serial}] {type.Name} = {msg.ToString()}");
        }
        else
        {
            LogTool.NetLog($"[ack={serial}] {type.Name} = {msg.ToString()}");
        }
    }
}

public enum LogLevel
{
    Info,
    Warning,
    Error,
    Release,
}