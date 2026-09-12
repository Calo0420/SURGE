using System.Collections;
using System.Collections.Generic;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using AOT;

namespace SkillzSDK.Internal.API.Web
{
    public class JsBridge
    {
        private static Dictionary<string, TaskCompletionSource<string>> taskMap = new Dictionary<string, TaskCompletionSource<string>>();

#if UNITY_WEBGL
        [DllImport("__Internal")]
        private static extern void RunAsyncTask(string taskName, string taskId, string args, Action<string, string, string> cb);
        [DllImport("__Internal")]
        public static extern string RunSyncTask(string taskName, string args = "");
#endif

        public static Task<string> RunTask(string taskName, string args = "")
        {
            Debug.Log("Asynctask Started");

            string taskId = Guid.NewGuid().ToString();
            var task = new TaskCompletionSource<string>();

#if UNITY_WEBGL && !UNITY_EDITOR
                taskMap[taskId] = task;
                RunAsyncTask(taskName, taskId, args, AsyncTaskFinished);
                return task.Task;
#else
            // Return a completed task with a default value when not running in WebGL
            return Task.FromResult(string.Empty);
#endif
        }

        public static string SyncTask(string taskName, string args = "")
        {
            Debug.Log("Synctask Started");
#if UNITY_WEBGL && !UNITY_EDITOR
                return RunSyncTask(taskName, args);
#endif
            return string.Empty;
        }

        [AOT.MonoPInvokeCallback(typeof(Action<string, string, string>))]
        public static void AsyncTaskFinished(string taskId, string message, string error = "")
        {
            Debug.Log("Asynctask Finished " + taskId + " ; " + message);
            if (taskMap.ContainsKey(taskId))
            {
                if (String.IsNullOrEmpty(error))
                {
                    taskMap[taskId].SetResult(message);
                }
                else
                {
                    taskMap[taskId].SetException(new Exception(error));
                }
                taskMap.Remove(taskId);
            }
        }
    }
}
