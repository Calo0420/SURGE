using System.Runtime.InteropServices;
using UnityEngine;

namespace SkillzSDK.Internal.API.Web
{
  public class ClangBridge
  {
    [DllImport("__Internal")]
    private static extern void seedRandomWithArray(uint[] seedArray, int seedArrayLength);

    [DllImport("__Internal")]
    private static extern float getRandomFloat();


        public static void SeedRandomWithArray(uint[] seedArray)
        {
            // Fallback if the seedArray is null or empty
            if (seedArray == null || seedArray.Length == 0)
            {
                
                seedArray = new uint[] { (uint)Random.Range(1, int.MaxValue) };
            }

            // Call the native function directly with the array and length
            seedRandomWithArray(seedArray, seedArray.Length);
        }


        public static float GetRandomFloat()
    {
        return getRandomFloat();
    }
  }
}
