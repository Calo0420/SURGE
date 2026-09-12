using UnityEngine;
using SkillzSDK.Internal;

namespace SkillzSDK
{
	public sealed class InSDKScenesController : MonoBehaviour
	{
		public void LoadTournamentSelectionScene()
		{
			CoroutineRunner.Instance.RunCoroutine(SDKScenesLoader.Load(SDKScenesLoader.TournamentSelectionScene));
		}
	}
}