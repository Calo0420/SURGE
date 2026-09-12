using System;
using System.Threading.Tasks;
using UnityEngine;

namespace SkillzSDK.Internal.API.Web
{

    public class Utils
    {

        public async static void HandleAsyncTaskToDelegate(Func<Task<string>> asyncFn, Action successCallback, Action<string> failureCallback)
        {
            try
            {
                string response = await asyncFn();
                Debug.Log("SkillzWebSDK Response: " + response);
                successCallback();
            }
            catch(Exception e)
            {
                failureCallback(e.Message);
            }
        }
    }
}
